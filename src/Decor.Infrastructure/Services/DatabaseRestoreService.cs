using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using Decor.Core.DTOs;
using Decor.Core.Common;
using Decor.Core.Interfaces.Data;
using Decor.Core.Interfaces.Services;
using MySqlConnector;

namespace Decor.Infrastructure.Services;

public sealed class DatabaseRestoreService(
    IDatabaseConnection databaseConnection,
    IDatabaseBackupService backupService,
    IAuthenticatedUserContext authenticatedUserContext,
    IAuthorizationService authorizationService) : IDatabaseRestoreService
{
    public const long MaximumSqlBytes = 64 * 1024 * 1024;
    public const long MaximumArchiveBytes = 64 * 1024 * 1024;
    private static readonly SemaphoreSlim RestoreGate = new(1, 1);

    public async Task<DatabaseRestoreResult> RestoreBackupAsync(string backupFile, CancellationToken cancellationToken = default)
    {
        EnsureAuthorized();
        ArgumentException.ThrowIfNullOrWhiteSpace(backupFile);
        await RestoreGate.WaitAsync(cancellationToken);
        try
        {
            EnsureAuthorized();
            var dump = await ReadDumpAsync(backupFile, cancellationToken);
            using var rawConnection = databaseConnection.CreateConnection();
            if (rawConnection is not MySqlConnection connection)
                throw new InvalidOperationException("A restauracao exige uma conexao MySqlConnector.");

            var configuredDatabase = connection.Database;
            var sql = ValidateDump(dump, configuredDatabase);
            await connection.OpenAsync(cancellationToken);
            using (var schemaCommand = new MySqlCommand("SELECT DATABASE();", connection))
            {
                var actualDatabase = await schemaCommand.ExecuteScalarAsync(cancellationToken) as string;
                if (!string.Equals(actualDatabase, configuredDatabase, StringComparison.Ordinal))
                    throw new InvalidDataException("O banco conectado difere do destino configurado.");
            }

            var safetyFile = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(backupFile))!,
                "restore-safety", $"decor-before-restore-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}.zip");
            await backupService.CreateBackupAsync([safetyFile], cancellationToken);
            if (!File.Exists(safetyFile) || new FileInfo(safetyFile).Length == 0)
                throw new InvalidOperationException("O backup de seguranca nao foi criado; restauracao abortada.");

            var restoreSucceeded = false;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                EnsureAuthorized();
                using var modeCommand = new MySqlCommand("SELECT @@SESSION.sql_mode;", connection);
                var sqlMode = (string)(await modeCommand.ExecuteScalarAsync(CancellationToken.None))!;
                using var configureCommand = new MySqlCommand("SET SESSION sql_mode = @mode;", connection);
                configureCommand.Parameters.AddWithValue("@mode", string.Join(",", sqlMode.Split(',')
                    .Where(mode => !mode.Equals("NO_BACKSLASH_ESCAPES", StringComparison.OrdinalIgnoreCase))));
                await configureCommand.ExecuteNonQueryAsync(CancellationToken.None);

                cancellationToken.ThrowIfCancellationRequested();
                EnsureAuthorized();
                using var restoreCommand = new MySqlCommand(sql, connection) { CommandTimeout = 0 };
                await restoreCommand.ExecuteNonQueryAsync(CancellationToken.None);
                restoreSucceeded = true;
            }
            catch (OperationCanceledException)
            {
                var canceled = new OperationCanceledException(
                    $"Restauracao cancelada antes de alterar tabelas. Backup de seguranca: {safetyFile}", cancellationToken);
                canceled.Data[nameof(DatabaseRestoreResult.SafetyBackupFile)] = safetyFile;
                throw canceled;
            }
            catch (Exception)
            {
                throw RestoreFailure(safetyFile);
            }
            finally
            {
                try
                {
                    using var resetCommand = new MySqlCommand("SET FOREIGN_KEY_CHECKS=1;", connection);
                    await resetCommand.ExecuteNonQueryAsync(CancellationToken.None);
                }
                catch (Exception)
                {
                    MySqlConnection.ClearPool(connection);
                    if (restoreSucceeded)
                        throw RestoreFailure(safetyFile);
                }
            }

            return new DatabaseRestoreResult(DateTime.Now, safetyFile);
        }
        finally
        {
            RestoreGate.Release();
        }
    }

    private void EnsureAuthorized()
    {
        if (!authenticatedUserContext.IsAuthenticated || !authorizationService.HasPermission(DecorPermissions.DatabaseMaintenanceRestore))
            throw new UnauthorizedAccessException("A restauracao exige autenticacao e permissao explicita.");
    }

    private static InvalidOperationException RestoreFailure(string safetyFile)
    {
        var failure = new InvalidOperationException(
            $"Falha na restauracao; o banco pode estar parcialmente alterado. Encerre as sessoes. Backup de seguranca: {safetyFile}");
        failure.Data[nameof(DatabaseRestoreResult.SafetyBackupFile)] = safetyFile;
        return failure;
    }

    private static async Task<string> ReadDumpAsync(string backupFile, CancellationToken cancellationToken)
    {
        await using var file = new FileStream(backupFile, FileMode.Open, FileAccess.Read, FileShare.Read,
            81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
        if (file.Length > MaximumArchiveBytes)
            throw new InvalidDataException("O ZIP excede o limite permitido.");
        using var archive = new ZipArchive(file, ZipArchiveMode.Read);
        if (archive.Entries.Count != 1)
            throw new InvalidDataException("O ZIP deve conter somente um arquivo SQL.");
        var entry = archive.Entries[0];
        if (!entry.Name.EndsWith(".sql", StringComparison.OrdinalIgnoreCase)
            || entry.FullName != entry.Name || entry.FullName.Contains('\\')
            || entry.Length == 0 || entry.Length > MaximumSqlBytes
            || entry.CompressedLength == 0 || entry.Length / entry.CompressedLength > 1000)
            throw new InvalidDataException("Entrada SQL invalida ou excede os limites de descompressao.");

        await using var source = entry.Open();
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int count;
        while ((count = await source.ReadAsync(chunk, cancellationToken)) != 0)
        {
            if (buffer.Length + count > MaximumSqlBytes)
                throw new InvalidDataException("O SQL excede o limite permitido.");
            buffer.Write(chunk, 0, count);
        }
        if (buffer.Length != entry.Length)
            throw new InvalidDataException("O tamanho da entrada SQL e inconsistente.");
        return new UTF8Encoding(false, true).GetString(buffer.GetBuffer(), 0, checked((int)buffer.Length));
    }

    private static string ValidateDump(string dump, string database)
    {
        if (string.IsNullOrWhiteSpace(database))
            throw new InvalidDataException("A conexao deve configurar um banco de destino.");
        using var reader = new StringReader(dump);
        var identifier = "`" + database.Replace("`", "``", StringComparison.Ordinal) + "`";
        if (reader.ReadLine() != "-- Decor database backup"
            || reader.ReadLine() != $"-- Database: {identifier}")
            throw new InvalidDataException("Backup nao pertence ao Decor ou a outro banco de dados.");
        var created = reader.ReadLine();
        if (created is null || !created.StartsWith("-- Created at: ", StringComparison.Ordinal)
            || !DateTime.TryParseExact(created[15..], "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out _))
            throw new InvalidDataException("Cabecalho do backup invalido.");
        var create = reader.ReadLine();
        if (create is null || !Regex.IsMatch(create,
                "^CREATE DATABASE IF NOT EXISTS " + Regex.Escape(identifier)
                + " DEFAULT CHARACTER SET [a-zA-Z0-9_]+ COLLATE [a-zA-Z0-9_]+;$",
                RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1))
            || reader.ReadLine() != $"USE {identifier};"
            || reader.ReadLine() != "SET FOREIGN_KEY_CHECKS=0;")
            throw new InvalidDataException("Destino ou preambulo do backup invalido.");
        var body = reader.ReadToEnd();
        if (!body.TrimEnd().EndsWith("SET FOREIGN_KEY_CHECKS=1;", StringComparison.Ordinal)
            || !(body.TrimStart().StartsWith("DROP TABLE IF EXISTS `", StringComparison.Ordinal)
                || body.Trim() == "SET FOREIGN_KEY_CHECKS=1;"))
            throw new InvalidDataException("Formato do dump Decor invalido.");
        return "SET FOREIGN_KEY_CHECKS=0;\n" + body;
    }
}