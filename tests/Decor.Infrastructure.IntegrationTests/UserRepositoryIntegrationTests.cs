using Dapper;
using Decor.FluentSqlBuilder;
using Decor.FluentSqlBuilder.Dialects.MariaDB;
using Decor.Infrastructure.Data;
using Decor.Infrastructure.Data.Repositories;
using MySqlConnector;

namespace Decor.Infrastructure.IntegrationTests;

public sealed class UserRepositoryIntegrationTests(MariaDbFixture fixture) : IClassFixture<MariaDbFixture>
{
    [Fact]
    public async Task GetByUsernameAsync_WithActiveUser_LoadsUserRolesAndDistinctPermissionsFromTheThreeResultSets()
    {
        await using var data = await UserTestData.CreateAsync(fixture.ConnectionString, isActive: true, withRolesAndPermissions: true);
        var repository = CreateRepository();

        var user = await repository.GetByUsernameAsync(data.Username);

        user.Should().NotBeNull();
        user!.UserID.Should().Be(data.UserId);
        user.Username.Should().Be(data.Username);
        user.DisplayName.Should().Be(data.DisplayName);
        user.IsActive.Should().BeTrue();
        user.MustChangePassword.Should().BeFalse();
        user.Roles.Should().BeEquivalentTo(data.RoleNames);
        user.Permissions.Should().BeEquivalentTo(data.PermissionCodes);
        user.Permissions.Should().HaveCount(3, "the permission shared by both roles must be returned once by SELECT DISTINCT");
        typeof(Decor.Core.Entities.ApplicationUser).GetProperty("PasswordHash").Should().BeNull();
    }

    [Fact]
    public async Task GetByUsernameAsync_WithNonexistentUser_ReturnsNull()
    {
        var repository = CreateRepository();

        var user = await repository.GetByUsernameAsync($"missing-{Guid.NewGuid():N}");

        user.Should().BeNull();
    }

    [Fact]
    public async Task GetByUsernameAsync_WithInactiveUser_ReturnsNull()
    {
        await using var data = await UserTestData.CreateAsync(fixture.ConnectionString, isActive: false, withRolesAndPermissions: true);
        var repository = CreateRepository();

        var user = await repository.GetByUsernameAsync(data.Username);

        user.Should().BeNull();
    }

    [Fact]
    public async Task GetByUsernameAsync_WithUserRequiringPasswordChange_LoadsMustChangePassword()
    {
        await using var data = await UserTestData.CreateAsync(fixture.ConnectionString, isActive: true, withRolesAndPermissions: false, mustChangePassword: true);
        var repository = CreateRepository();

        var user = await repository.GetByUsernameAsync(data.Username);

        user.Should().NotBeNull();
        user!.MustChangePassword.Should().BeTrue();
    }

    [Fact]
    public async Task MustChangePasswordMigration_MarksAdminWithUnchangedInitialCredential()
    {
        await using var connection = new MySqlConnection(fixture.ConnectionString);

        var mustChangePassword = await connection.QuerySingleAsync<bool>(
            "SELECT MustChangePassword FROM users WHERE Username = 'admin';");

        mustChangePassword.Should().BeTrue();
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    public async Task GetPasswordHashByUsernameAsync_ReturnsHashOnlyForExistingActiveUser(bool exists, bool isActive)
    {
        await using var data = exists
            ? await UserTestData.CreateAsync(fixture.ConnectionString, isActive, withRolesAndPermissions: false)
            : null;
        var username = data?.Username ?? $"missing-{Guid.NewGuid():N}";
        var repository = CreateRepository();

        var passwordHash = await repository.GetPasswordHashByUsernameAsync(username);

        passwordHash.Should().Be(exists && isActive ? data!.PasswordHash : null);
    }

    [Fact]
    public async Task UpdatePasswordAsync_UpdatesHashAndClearsMandatoryPasswordChange()
    {
        await using var data = await UserTestData.CreateAsync(fixture.ConnectionString, isActive: true, withRolesAndPermissions: false, mustChangePassword: true);
        var repository = CreateRepository();
        var newHash = $"new-hash-{Guid.NewGuid():N}";

        var updated = await repository.UpdatePasswordAsync(data.UserId, newHash, mustChangePassword: false);

        updated.Should().BeTrue();
        (await repository.GetPasswordHashByUsernameAsync(data.Username)).Should().Be(newHash);
        (await repository.GetByUsernameAsync(data.Username))!.MustChangePassword.Should().BeFalse();
    }

    [Fact]
    public async Task UpdatePasswordAsync_ForInactiveUser_DoesNotUpdate()
    {
        await using var data = await UserTestData.CreateAsync(fixture.ConnectionString, isActive: false, withRolesAndPermissions: false);
        var repository = CreateRepository();

        var updated = await repository.UpdatePasswordAsync(data.UserId, "new-hash", mustChangePassword: false);

        updated.Should().BeFalse();
    }

    private UserRepository CreateRepository() => new(new DatabaseConnection(fixture.ConnectionString), () => FluentCommandBuilder.Create(new MariaDBDialect()));

    private sealed class UserTestData : IAsyncDisposable
    {
        private readonly string _connectionString;

        private UserTestData(string connectionString, string suffix, int userId, string username, string displayName, string passwordHash, IReadOnlyCollection<string> roleNames, IReadOnlyCollection<string> permissionCodes)
        {
            _connectionString = connectionString;
            Suffix = suffix;
            UserId = userId;
            Username = username;
            DisplayName = displayName;
            PasswordHash = passwordHash;
            RoleNames = roleNames;
            PermissionCodes = permissionCodes;
        }

        public string Suffix { get; }
        public int UserId { get; }
        public string Username { get; }
        public string DisplayName { get; }
        public string PasswordHash { get; }
        public IReadOnlyCollection<string> RoleNames { get; }
        public IReadOnlyCollection<string> PermissionCodes { get; }

        public static async Task<UserTestData> CreateAsync(string connectionString, bool isActive, bool withRolesAndPermissions, bool mustChangePassword = false)
        {
            var suffix = Guid.NewGuid().ToString("N");
            var username = $"user_{suffix}";
            var displayName = $"Integration {suffix}";
            var passwordHash = $"hash-{suffix}";
            var roleNames = withRolesAndPermissions
                ? new[] { $"role_catalog_{suffix}", $"role_sales_{suffix}" }
                : Array.Empty<string>();
            var permissionCodes = withRolesAndPermissions
                ? new[] { $"catalog.view.{suffix}", $"sales.view.{suffix}", $"shared.view.{suffix}" }
                : Array.Empty<string>();

            await using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();
            await using var transaction = await connection.BeginTransactionAsync();

            var userId = await connection.QuerySingleAsync<int>(
                """
                INSERT INTO users (Username, DisplayName, PasswordHash, IsActive, MustChangePassword)
                VALUES (@Username, @DisplayName, @PasswordHash, @IsActive, @MustChangePassword);
                SELECT LAST_INSERT_ID();
                """,
                new { Username = username, DisplayName = displayName, PasswordHash = passwordHash, IsActive = isActive, MustChangePassword = mustChangePassword },
                transaction);

            if (withRolesAndPermissions)
            {
                foreach (var roleName in roleNames)
                {
                    var roleId = await connection.QuerySingleAsync<int>(
                        "INSERT INTO roles (RoleName) VALUES (@RoleName); SELECT LAST_INSERT_ID();",
                        new { RoleName = roleName },
                        transaction);
                    await connection.ExecuteAsync(
                        "INSERT INTO user_roles (UserID, RoleID) VALUES (@UserId, @RoleId);",
                        new { UserId = userId, RoleId = roleId },
                        transaction);
                }

                foreach (var permissionCode in permissionCodes)
                {
                    await connection.ExecuteAsync(
                        "INSERT INTO permissions (PermissionCode) VALUES (@PermissionCode);",
                        new { PermissionCode = permissionCode },
                        transaction);
                }

                var permissions = (await connection.QueryAsync<(int PermissionId, string PermissionCode)>(
                    "SELECT PermissionID, PermissionCode FROM permissions WHERE PermissionCode IN @PermissionCodes;",
                    new { PermissionCodes = permissionCodes }, transaction)).ToDictionary(permission => permission.PermissionCode, permission => permission.PermissionId);
                var roles = (await connection.QueryAsync<(int RoleId, string RoleName)>(
                    "SELECT RoleID, RoleName FROM roles WHERE RoleName IN @RoleNames;",
                    new { RoleNames = roleNames }, transaction)).ToDictionary(role => role.RoleName, role => role.RoleId);

                await connection.ExecuteAsync(
                    "INSERT INTO role_permissions (RoleID, PermissionID) VALUES (@RoleId, @PermissionId);",
                    new[]
                    {
                        new { RoleId = roles[roleNames[0]], PermissionId = permissions[permissionCodes[0]] },
                        new { RoleId = roles[roleNames[0]], PermissionId = permissions[permissionCodes[2]] },
                        new { RoleId = roles[roleNames[1]], PermissionId = permissions[permissionCodes[1]] },
                        new { RoleId = roles[roleNames[1]], PermissionId = permissions[permissionCodes[2]] }
                    },
                    transaction);
            }

            await transaction.CommitAsync();
            return new UserTestData(connectionString, suffix, userId, username, displayName, passwordHash, roleNames, permissionCodes);
        }

        public async ValueTask DisposeAsync()
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            await connection.ExecuteAsync(
                "DELETE FROM users WHERE UserID = @UserId; DELETE FROM roles WHERE RoleName LIKE @RolePrefix; DELETE FROM permissions WHERE PermissionCode LIKE @PermissionPrefix;",
                new { UserId, RolePrefix = $"%{Suffix}", PermissionPrefix = $"%{Suffix}" });
        }
    }
}
