using System.IO.Compression;
using System.Text;
using Dapper;
using Decor.Core.DTOs;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Data;
using Decor.Core.Interfaces.Services;
using Decor.Infrastructure.Data;
using Decor.Infrastructure.Services;
using MySqlConnector;

namespace Decor.Infrastructure.IntegrationTests;

public sealed class DatabaseRestoreIntegrationTests(MariaDbFixture fixture) : IClassFixture<MariaDbFixture>
{
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    public async Task Restore_RequiresAuthenticationAndExplicitPermissionBeforeAnyCalls(bool authenticated, bool allowed)
    {
        var database = new ConnectionSpy(fixture.ConnectionString);
        var backup = new BackupSpy();
        var restore = new DatabaseRestoreService(database, backup,
            new UserContext { IsAuthenticated = authenticated }, new Authorization { Allowed = allowed });

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => restore.RestoreBackupAsync("not-even-a-file.zip"));

        Assert.Equal(0, database.Calls);
        Assert.Equal(0, backup.Calls);
    }

    [Theory]
    [InlineData("empty")]
    [InlineData("multiple")]
    [InlineData("not-sql")]
    [InlineData("nested")]
    [InlineData("traversal")]
    [InlineData("backslash")]
    [InlineData("empty-sql")]
    [InlineData("oversized-sql")]
    [InlineData("oversized-zip")]
    [InlineData("not-zip")]
    public async Task Restore_RejectsInvalidArchivesBeforeConnectionOrSafetyCalls(string kind)
    {
        var directory = CreateDirectory();
        try
        {
            var path = Path.Combine(directory, "invalid.zip");
            if (kind is "oversized-zip" or "not-zip")
            {
                await using var file = File.Create(path);
                file.SetLength(kind == "oversized-zip" ? DatabaseRestoreService.MaximumArchiveBytes + 1 : 20);
            }
            else
            {
                using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
                if (kind != "empty")
                {
                    var name = kind switch
                    {
                        "not-sql" => "dump.txt",
                        "nested" => "nested/dump.sql",
                        "traversal" => "../dump.sql",
                        "backslash" => "..\\dump.sql",
                        _ => "dump.sql"
                    };
                    using (var entry = archive.CreateEntry(name).Open())
                    {
                        if (kind == "oversized-sql")
                        {
                            var chunk = new byte[1024 * 1024];
                            for (var index = 0; index < 65; index++)
                                entry.Write(chunk);
                        }
                        else if (kind != "empty-sql")
                            entry.Write(Encoding.UTF8.GetBytes("not a Decor dump"));
                    }
                    if (kind == "multiple")
                        archive.CreateEntry("second.sql");
                }
            }

            var database = new ConnectionSpy(fixture.ConnectionString);
            var backup = new BackupSpy();
            var restore = CreateService(database, backup);
            await Assert.ThrowsAsync<InvalidDataException>(() => restore.RestoreBackupAsync(path));
            Assert.Equal(0, database.Calls);
            Assert.Equal(0, backup.Calls);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Theory]
    [InlineData("nondecor")]
    [InlineData("header-database")]
    [InlineData("create-database")]
    [InlineData("use-database")]
    [InlineData("missing-footer")]
    public async Task Restore_RejectsInvalidEnvelopeWithoutOpeningConnectionOrCreatingSafety(string kind)
    {
        var directory = CreateDirectory();
        try
        {
            var databaseName = new MySqlConnectionStringBuilder(fixture.ConnectionString).Database;
            var dump = Envelope(databaseName, "");
            dump = kind switch
            {
                "nondecor" => dump.Replace("-- Decor database backup", "-- foreign dump"),
                "header-database" => dump.Replace($"-- Database: `{databaseName}`", "-- Database: `foreign_schema`"),
                "create-database" => dump.Replace($"CREATE DATABASE IF NOT EXISTS `{databaseName}`", "CREATE DATABASE IF NOT EXISTS `foreign_schema`"),
                "use-database" => dump.Replace($"USE `{databaseName}`;", "USE `foreign_schema`;"),
                _ => dump.Replace("SET FOREIGN_KEY_CHECKS=1;", "")
            };
            var path = await WriteArchiveAsync(directory, dump);
            var database = new ConnectionSpy(fixture.ConnectionString);
            var backup = new BackupSpy();
            await Assert.ThrowsAsync<InvalidDataException>(() => CreateService(database, backup).RestoreBackupAsync(path));
            Assert.Equal(0, backup.Calls);
            Assert.Equal(System.Data.ConnectionState.Closed, database.LastConnection!.State);
            await using var connection = new MySqlConnection(fixture.ConnectionString);
            Assert.Equal(0, await connection.QuerySingleAsync<int>(
                "SELECT COUNT(*) FROM information_schema.SCHEMATA WHERE SCHEMA_NAME = 'foreign_schema';"));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task Restore_SqlFailureRetainsSafetyPathAndResetsForeignKeysOnSamePhysicalSession()
    {
        var directory = CreateDirectory();
        var builder = new MySqlConnectionStringBuilder(fixture.ConnectionString)
        {
            ConnectionReset = false,
            MaximumPoolSize = 1,
            ApplicationName = "restore-failure-" + Guid.NewGuid().ToString("N")
        };
        try
        {
            await using var probe = new MySqlConnection(builder.ConnectionString);
            await probe.OpenAsync();
            var serverThread = probe.ServerThread;
            await probe.CloseAsync();
            var dump = Envelope(builder.Database,
                "DROP TABLE IF EXISTS `restore_failure_probe`;\n"
                + "CREATE TABLE `restore_failure_probe` (Id INT PRIMARY KEY);\n"
                + "INSERT INTO `restore_failure_probe` (DO_NOT_LEAK_PASSWORD) VALUES (1);\n");
            var source = await WriteArchiveAsync(directory, dump);
            var backup = new DatabaseBackupService(new DatabaseConnection(fixture.ConnectionString));
            var restore = CreateService(new DatabaseConnection(builder.ConnectionString), backup);

            var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => restore.RestoreBackupAsync(source));

            var safetyPath = Assert.IsType<string>(failure.Data[nameof(DatabaseRestoreResult.SafetyBackupFile)]);
            Assert.True(File.Exists(safetyPath));
            Assert.Contains(safetyPath, failure.Message);
            Assert.DoesNotContain("DO_NOT_LEAK_PASSWORD", failure.ToString());
            Assert.Null(failure.InnerException);
            await probe.OpenAsync();
            Assert.Equal(serverThread, probe.ServerThread);
            Assert.Equal(1, await probe.QuerySingleAsync<int>("SELECT @@SESSION.foreign_key_checks;"));
            await probe.ExecuteAsync("DROP TABLE restore_failure_probe;");
        }
        finally
        {
            MySqlConnection.ClearAllPools();
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task Restore_SafetyBackupFailureDoesNotExecuteDump()
    {
        var directory = CreateDirectory();
        try
        {
            var database = new DatabaseConnection(fixture.ConnectionString);
            var name = new MySqlConnectionStringBuilder(fixture.ConnectionString).Database;
            var source = await WriteArchiveAsync(directory, Envelope(name,
                "DROP TABLE IF EXISTS `restore_should_not_exist`;\nCREATE TABLE `restore_should_not_exist` (Id INT);\n"));
            var backup = new BackupSpy();
            await Assert.ThrowsAsync<InvalidOperationException>(() => CreateService(database, backup).RestoreBackupAsync(source));
            Assert.Equal(1, backup.Calls);
            await using var connection = new MySqlConnection(fixture.ConnectionString);
            Assert.Equal(0, await connection.QuerySingleAsync<int>(
                "SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'restore_should_not_exist';"));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task Restore_CanceledBeforeStartMakesNoCalls()
    {
        var database = new ConnectionSpy(fixture.ConnectionString);
        var backup = new BackupSpy();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CreateService(database, backup).RestoreBackupAsync("unused.zip", cancellation.Token));
        Assert.Equal(0, database.Calls);
        Assert.Equal(0, backup.Calls);
    }

    [Fact]
    public async Task Restore_CanceledAfterSafetyBackupPreservesPathAndDoesNotAlterTables()
    {
        var directory = CreateDirectory();
        using var cancellation = new CancellationTokenSource();
        try
        {
            var database = new DatabaseConnection(fixture.ConnectionString);
            var name = new MySqlConnectionStringBuilder(fixture.ConnectionString).Database;
            var source = await WriteArchiveAsync(directory, Envelope(name,
                "DROP TABLE IF EXISTS `restore_cancel_probe`;\nCREATE TABLE `restore_cancel_probe` (Id INT);\n"));
            var backup = new ObservedBackup(new DatabaseBackupService(database), () =>
            {
                cancellation.Cancel();
                return Task.CompletedTask;
            });
            var failure = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                CreateService(database, backup).RestoreBackupAsync(source, cancellation.Token));
            var safetyPath = Assert.IsType<string>(failure.Data[nameof(DatabaseRestoreResult.SafetyBackupFile)]);
            Assert.True(File.Exists(safetyPath));
            await using var connection = new MySqlConnection(fixture.ConnectionString);
            Assert.Equal(0, await connection.QuerySingleAsync<int>(
                "SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'restore_cancel_probe';"));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task Restore_ConcurrentServiceInstancesAreSerialized()
    {
        var directory = CreateDirectory();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            var firstDatabase = new ConnectionSpy(fixture.ConnectionString);
            var secondDatabase = new ConnectionSpy(fixture.ConnectionString);
            var realBackup = new DatabaseBackupService(new DatabaseConnection(fixture.ConnectionString));
            var heldBackup = new ObservedBackup(realBackup, async () =>
            {
                entered.SetResult();
                await release.Task.WaitAsync(TimeSpan.FromSeconds(30));
            });
            var name = new MySqlConnectionStringBuilder(fixture.ConnectionString).Database;
            var source = await WriteArchiveAsync(directory, Envelope(name, ""));
            var first = CreateService(firstDatabase, heldBackup).RestoreBackupAsync(source);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(30));
            var second = CreateService(secondDatabase, realBackup).RestoreBackupAsync(source);
            var secondWasBlocked = secondDatabase.Calls == 0 && !second.IsCompleted;
            release.SetResult();
            var results = await Task.WhenAll(first, second);

            Assert.True(secondWasBlocked);
            Assert.All(results, result => Assert.True(File.Exists(result.SafetyBackupFile)));
            Assert.NotEqual(results[0].SafetyBackupFile, results[1].SafetyBackupFile);
            Assert.Equal(1, firstDatabase.Calls);
            Assert.Equal(1, secondDatabase.Calls);
        }
        finally
        {
            release.TrySetResult();
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task Restore_IgnoresCancellationOnceDestructiveBatchStarts()
    {
        var directory = CreateDirectory();
        var lockName = "restore-test-" + Guid.NewGuid().ToString("N");
        using var cancellation = new CancellationTokenSource();
        await using var observer = new MySqlConnection(fixture.ConnectionString);
        await observer.OpenAsync();
        Assert.Equal(1, await observer.QuerySingleAsync<int>("SELECT GET_LOCK(@LockName, 10);", new { LockName = lockName }));
        try
        {
            var database = new DatabaseConnection(fixture.ConnectionString);
            var name = new MySqlConnectionStringBuilder(fixture.ConnectionString).Database;
            var source = await WriteArchiveAsync(directory, Envelope(name,
                "DROP TABLE IF EXISTS `restore_running_probe`;\nCREATE TABLE `restore_running_probe` (Id INT);\n"
                + $"SELECT GET_LOCK('{lockName}', 30);\nINSERT INTO `restore_running_probe` VALUES (1);\n"
                + $"SELECT RELEASE_LOCK('{lockName}');\n"));
            var running = CreateService(database, new DatabaseBackupService(database))
                .RestoreBackupAsync(source, cancellation.Token);
            var deadline = DateTime.UtcNow.AddSeconds(20);
            var reachedBatch = false;
            while (DateTime.UtcNow < deadline && !running.IsCompleted)
            {
                reachedBatch = await observer.QuerySingleAsync<int>(
                    "SELECT COUNT(*) FROM information_schema.PROCESSLIST WHERE ID <> CONNECTION_ID() AND INFO LIKE @Pattern;",
                    new { Pattern = "%" + lockName + "%" }) > 0;
                if (reachedBatch)
                    break;
                await Task.Delay(10);
            }
            cancellation.Cancel();
            await observer.ExecuteAsync("SELECT RELEASE_LOCK(@LockName);", new { LockName = lockName });
            var result = await running;

            Assert.True(reachedBatch);
            Assert.True(File.Exists(result.SafetyBackupFile));
            Assert.Equal(1, await observer.QuerySingleAsync<int>("SELECT COUNT(*) FROM restore_running_probe;"));
            await observer.ExecuteAsync("DROP TABLE restore_running_probe;");
        }
        finally
        {
            await observer.ExecuteAsync("SELECT RELEASE_LOCK(@LockName);", new { LockName = lockName });
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task Restore_RoundTripsLegacyDumpAndPreservesPreRestoreSafetyBackup()
    {
        var directory = Path.Combine(Path.GetTempPath(), "decor-restore-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var database = new DatabaseConnection(fixture.ConnectionString);
            var backup = new DatabaseBackupService(database);
            var restore = new DatabaseRestoreService(database, backup, new UserContext(), new Authorization());
            await using var connection = new MySqlConnection(fixture.ConnectionString);
            await connection.ExecuteAsync("CREATE TABLE restore_probe (Id INT PRIMARY KEY, Value LONGTEXT NOT NULL);");
            const string original = "quotes ' double \" slash \\ ;\r\nUSE `another_database`;\nUnicode: \u00e7\u00e3\u00e9";
            await connection.ExecuteAsync("INSERT INTO restore_probe VALUES (1, @Value);", new { Value = original });
            var source = Path.Combine(directory, "source.zip");
            await backup.CreateBackupAsync([source]);
            await connection.ExecuteAsync("UPDATE restore_probe SET Value = 'before restore';");

            var result = await restore.RestoreBackupAsync(source);

            Assert.True(result.RequiresSignOut);
            Assert.Equal(original, await connection.QuerySingleAsync<string>("SELECT Value FROM restore_probe;"));
            Assert.True(File.Exists(result.SafetyBackupFile));
            using (var safety = ZipFile.OpenRead(result.SafetyBackupFile))
            using (var reader = new StreamReader(safety.Entries.Single().Open()))
                Assert.Contains("before restore", await reader.ReadToEndAsync());
            await restore.RestoreBackupAsync(result.SafetyBackupFile);
            Assert.Equal("before restore", await connection.QuerySingleAsync<string>("SELECT Value FROM restore_probe;"));
            await connection.ExecuteAsync("DROP TABLE restore_probe;");
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    private sealed class UserContext : IAuthenticatedUserContext
    {
        public event EventHandler? SignedOut { add { } remove { } }
        public bool IsAuthenticated { get; set; } = true;
        public ApplicationUser? User => null;
        public void SignIn(ApplicationUser user) { }
        public void SignOut() => IsAuthenticated = false;
    }

    private static DatabaseRestoreService CreateService(IDatabaseConnection database, IDatabaseBackupService backup) =>
        new(database, backup, new UserContext(), new Authorization());

    private static string CreateDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "decor-restore-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static string Envelope(string database, string body) =>
        $"-- Decor database backup\n-- Database: `{database}`\n-- Created at: 2026-10-04 10:00:00\n"
        + $"CREATE DATABASE IF NOT EXISTS `{database}` DEFAULT CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci;\n"
        + $"USE `{database}`;\nSET FOREIGN_KEY_CHECKS=0;\n\n{body}SET FOREIGN_KEY_CHECKS=1;\n";

    private static async Task<string> WriteArchiveAsync(string directory, string dump)
    {
        var path = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".zip");
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        await using var writer = new StreamWriter(archive.CreateEntry("dump.sql").Open(), new UTF8Encoding(false));
        await writer.WriteAsync(dump);
        return path;
    }

    private sealed class ConnectionSpy(string connectionString) : IDatabaseConnection
    {
        public int Calls { get; private set; }
        public MySqlConnection? LastConnection { get; private set; }
        public System.Data.IDbConnection CreateConnection()
        {
            Calls++;
            return LastConnection = new MySqlConnection(connectionString);
        }
    }

    private sealed class BackupSpy : IDatabaseBackupService
    {
        public int Calls { get; private set; }
        public Task<DatabaseBackupResult> CreateBackupAsync(IReadOnlyCollection<string> backupFiles,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(new DatabaseBackupResult(DateTime.Now, backupFiles.ToArray()));
        }
    }

    private sealed class ObservedBackup(IDatabaseBackupService inner, Func<Task> afterBackup) : IDatabaseBackupService
    {
        public async Task<DatabaseBackupResult> CreateBackupAsync(IReadOnlyCollection<string> backupFiles,
            CancellationToken cancellationToken = default)
        {
            var result = await inner.CreateBackupAsync(backupFiles, cancellationToken);
            await afterBackup();
            return result;
        }
    }

    private sealed class Authorization : IAuthorizationService
    {
        public bool Allowed { get; set; } = true;
        public bool HasPermission(string permissionCode) => Allowed && permissionCode == "DatabaseMaintenance.Restore";
        public bool CanView(string resource) => false;
        public bool CanCreate(string resource) => false;
        public bool CanEdit(string resource) => false;
        public bool CanDelete(string resource) => false;
    }
}