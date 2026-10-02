using Dapper;
using Decor.Core.Common;
using MySqlConnector;

namespace Decor.Infrastructure.IntegrationTests;

public sealed class ProtectedRoleNamesMigrationIntegrationTests(MariaDbFixture fixture) : IClassFixture<MariaDbFixture>
{
    [Fact]
    public async Task Migration_WhenCanonicalAndLegacyRolesCoexist_MergesAssignmentsAndPermissionsIdempotently()
    {
        await using var connection = new MySqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();

        var administratorRoleId = await connection.QuerySingleAsync<int>(
            "SELECT RoleID FROM roles WHERE RoleName = @RoleName;",
            new { RoleName = SystemRoleDefaults.Administrators });
        var supervisorRoleId = await connection.QuerySingleAsync<int>(
            "SELECT RoleID FROM roles WHERE RoleName = @RoleName;",
            new { RoleName = SystemRoleDefaults.Supervisors });

        var legacyAdministratorId = await InsertLegacyRoleAsync(connection, "Administrador", 250);
        var legacySupervisorId = await InsertLegacyRoleAsync(connection, "Supervisor", 120);
        var administratorUserId = await InsertUserAsync(connection, "am");
        var supervisorUserId = await InsertUserAsync(connection, "sm");

        await connection.ExecuteAsync(
            "INSERT INTO user_roles (UserID, RoleID) VALUES (@UserId, @OldRoleId), (@UserId, @CanonicalRoleId);",
            new { UserId = administratorUserId, OldRoleId = legacyAdministratorId, CanonicalRoleId = administratorRoleId });
        await connection.ExecuteAsync(
            "INSERT INTO user_roles (UserID, RoleID) VALUES (@UserId, @RoleId);",
            new { UserId = supervisorUserId, RoleId = legacySupervisorId });

        await connection.ExecuteAsync(
            "INSERT INTO permissions (PermissionCode, Description) VALUES ('RoleGroups.MigrationProbe', 'Migration test');");
        var probePermissionId = await connection.QuerySingleAsync<int>(
            "SELECT PermissionID FROM permissions WHERE PermissionCode = 'RoleGroups.MigrationProbe';");
        await connection.ExecuteAsync(
            "INSERT INTO role_permissions (RoleID, PermissionID) VALUES (@RoleId, @PermissionId);",
            new { RoleId = legacyAdministratorId, PermissionId = probePermissionId });
        await connection.ExecuteAsync(
            "INSERT INTO role_permissions (RoleID, PermissionID) VALUES (@RoleId, @PermissionId);",
            new { RoleId = legacySupervisorId, PermissionId = probePermissionId });

        var migration = await File.ReadAllTextAsync(Path.Combine(
            AppContext.BaseDirectory,
            "Migrations",
            "20261001_pluralize_protected_permission_groups.sql"));
        await connection.ExecuteAsync(migration);
        await connection.ExecuteAsync(migration);

        (await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM roles WHERE RoleName IN ('Administrador', 'Supervisor');")).Should().Be(0);
        (await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM roles WHERE RoleName = @RoleName;",
            new { RoleName = SystemRoleDefaults.Administrators })).Should().Be(1);
        (await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM roles WHERE RoleName = @RoleName;",
            new { RoleName = SystemRoleDefaults.Supervisors })).Should().Be(1);
        (await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM user_roles WHERE UserID = @UserId AND RoleID = @RoleId;",
            new { UserId = administratorUserId, RoleId = administratorRoleId })).Should().Be(1);
        (await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM user_roles WHERE UserID = @UserId AND RoleID = @RoleId;",
            new { UserId = supervisorUserId, RoleId = supervisorRoleId })).Should().Be(1);
        (await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM role_permissions WHERE RoleID IN (@AdministratorRoleId, @SupervisorRoleId) AND PermissionID = @PermissionId;",
            new { AdministratorRoleId = administratorRoleId, SupervisorRoleId = supervisorRoleId, PermissionId = probePermissionId })).Should().Be(2);

        (await connection.ExecuteScalarAsync<int>(
            "SELECT HierarchyLevel FROM roles WHERE RoleID = @RoleId;",
            new { RoleId = administratorRoleId })).Should().Be(250);
        (await connection.ExecuteScalarAsync<int>(
            "SELECT HierarchyLevel FROM roles WHERE RoleID = @RoleId;",
            new { RoleId = supervisorRoleId })).Should().Be(120);
    }

    private static Task<int> InsertLegacyRoleAsync(MySqlConnection connection, string roleName, int hierarchyLevel)
        => connection.QuerySingleAsync<int>(
            "INSERT INTO roles (RoleName, Description, HierarchyLevel, IsSystemProtected) VALUES (@RoleName, 'Legacy test role', @HierarchyLevel, 1); SELECT LAST_INSERT_ID();",
            new { RoleName = roleName, HierarchyLevel = hierarchyLevel });

    private static Task<int> InsertUserAsync(MySqlConnection connection, string prefix)
        => connection.QuerySingleAsync<int>(
            "INSERT INTO users (Username, DisplayName, PasswordHash, IsActive) VALUES (@Username, @DisplayName, 'unused', 1); SELECT LAST_INSERT_ID();",
            new { Username = $"{prefix}-{Guid.NewGuid():N}", DisplayName = prefix });
}