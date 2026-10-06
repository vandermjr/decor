using Dapper;
using Decor.Core.Common;
using Decor.FluentSqlBuilder;
using Decor.FluentSqlBuilder.Dialects.MariaDB;
using Decor.Infrastructure.Data;
using Decor.Infrastructure.Data.Repositories;
using MySqlConnector;

namespace Decor.Infrastructure.IntegrationTests;

public sealed class SecurityViewsIntegrationTests(MariaDbFixture fixture) : IClassFixture<MariaDbFixture>
{
    [Fact]
    public async Task MaintenanceMigration_SeedsMissingPermissionAndPreservesLaterRevocation()
    {
        await using var connection = new MySqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        await connection.ExecuteAsync("DELETE FROM permissions WHERE PermissionCode = 'DatabaseMaintenance.View';");
        var repository = CreateAdministrationRepository();
        var permissions = await repository.GetPermissionsAsync();
        var permission = permissions.Single(item => item.PermissionCode == DecorPermissions.DatabaseMaintenanceView);
        var roles = await repository.GetRolesAsync();
        var administrator = roles.Single(item => item.RoleName == SystemRoleDefaults.Administrators);
        var supervisor = roles.Single(item => item.RoleName == SystemRoleDefaults.Supervisors);
        var grantCount = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM role_permissions WHERE PermissionID = @PermissionId AND RoleID IN (@AdministratorId, @SupervisorId);",
            new { PermissionId = permission.PermissionID, AdministratorId = administrator.RoleID, SupervisorId = supervisor.RoleID });
        grantCount.Should().Be(2);

        await connection.ExecuteAsync("DELETE FROM role_permissions WHERE PermissionID = @PermissionId AND RoleID = @RoleId;",
            new { PermissionId = permission.PermissionID, RoleId = supervisor.RoleID });
        await CreateAdministrationRepository().GetPermissionsAsync();
        (await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM role_permissions WHERE PermissionID = @PermissionId AND RoleID = @RoleId;",
            new { PermissionId = permission.PermissionID, RoleId = supervisor.RoleID })).Should().Be(0);
    }

    [Fact]
    public async Task RoleRegistrationRepository_CreatesUpdatesAndRejectsProtectedOrDuplicateGroups()
    {
        var repository = new RoleRegistrationRepository(new DatabaseConnection(fixture.ConnectionString),
            () => FluentCommandBuilder.Create(new MariaDBDialect()));
        var name = $"Group-{Guid.NewGuid():N}";
        var roleId = await repository.CreateAsync(name, "Original", 10);
        roleId.Should().BePositive();
        (await repository.UpdateAsync(roleId, name + "-edit", "Updated", 20)).Should().BeTrue();
        var saved = (await CreateAdministrationRepository().GetRolesAsync()).Single(item => item.RoleID == roleId);
        saved.RoleName.Should().Be(name + "-edit");
        saved.Description.Should().Be("Updated");
        saved.HierarchyLevel.Should().Be(20);
        saved.IsSystemProtected.Should().BeFalse();
        Func<Task> duplicate = () => repository.CreateAsync(saved.RoleName, null, 0);
        await duplicate.Should().ThrowAsync<InvalidOperationException>();

        var protectedRole = (await CreateAdministrationRepository().GetRolesAsync())
            .Single(item => item.RoleName == SystemRoleDefaults.Administrators);
        (await repository.UpdateAsync(protectedRole.RoleID, "Changed", null, 0)).Should().BeFalse();
        (await repository.UpdateAsync(int.MaxValue, "Missing", null, 0)).Should().BeFalse();
    }

    [Fact]
    public async Task RoleRegistrationRepository_UpdatesSubordinateGroupWithLegacyProtection()
    {
        var repository = new RoleRegistrationRepository(new DatabaseConnection(fixture.ConnectionString),
            () => FluentCommandBuilder.Create(new MariaDBDialect()));
        var name = $"Group-{Guid.NewGuid():N}";
        var roleId = await repository.CreateAsync(name, "Original", 30);
        await using var connection = new MySqlConnection(fixture.ConnectionString);
        await connection.ExecuteAsync("UPDATE roles SET IsSystemProtected = 1 WHERE RoleID = @RoleId;", new { RoleId = roleId });

        (await repository.UpdateAsync(roleId, name + "-edit", "Updated", 40)).Should().BeTrue();

        var saved = (await CreateAdministrationRepository().GetRolesAsync()).Single(item => item.RoleID == roleId);
        saved.RoleName.Should().Be(name + "-edit");
        saved.Description.Should().Be("Updated");
        saved.HierarchyLevel.Should().Be(40);
        saved.IsSystemProtected.Should().BeTrue();
    }

    [Fact]
    public async Task RoleRegistrationRepository_UpdatesSupervisorsWithoutChangingLegacyProtection()
    {
        var repository = new RoleRegistrationRepository(new DatabaseConnection(fixture.ConnectionString),
            () => FluentCommandBuilder.Create(new MariaDBDialect()));
        var supervisor = (await CreateAdministrationRepository().GetRolesAsync())
            .Single(item => item.RoleName == SystemRoleDefaults.Supervisors);
        supervisor.IsSystemProtected.Should().BeTrue();
        try
        {
            (await repository.UpdateAsync(supervisor.RoleID, supervisor.RoleName, "Updated", supervisor.HierarchyLevel)).Should().BeTrue();
            var saved = (await CreateAdministrationRepository().GetRolesAsync()).Single(item => item.RoleID == supervisor.RoleID);
            saved.Description.Should().Be("Updated");
            saved.IsSystemProtected.Should().BeTrue();
        }
        finally
        {
            await repository.UpdateAsync(supervisor.RoleID, supervisor.RoleName, supervisor.Description, supervisor.HierarchyLevel);
        }
    }

    private UserAdministrationRepository CreateAdministrationRepository() => new(
        new DatabaseConnection(fixture.ConnectionString), () => FluentCommandBuilder.Create(new MariaDBDialect()));

    [Fact]
    public async Task RestoreMigration_GrantsOnlyAdministratorsAndPreservesRevocation()
    {
        await using var connection = new MySqlConnection(fixture.ConnectionString);
        await connection.ExecuteAsync("DELETE FROM permissions WHERE PermissionCode = 'DatabaseMaintenance.Restore';");
        var permissions = await CreateAdministrationRepository().GetPermissionsAsync();
        var restorePermission = permissions.Single(item => item.PermissionCode == DecorPermissions.DatabaseMaintenanceRestore);
        var grantedRoles = await connection.QueryAsync<string>(
            "SELECT r.RoleName FROM roles r INNER JOIN role_permissions rp ON rp.RoleID = r.RoleID WHERE rp.PermissionID = @PermissionId;",
            new { PermissionId = restorePermission.PermissionID });
        grantedRoles.Should().Equal(SystemRoleDefaults.Administrators);
        await connection.ExecuteAsync("DELETE FROM role_permissions WHERE PermissionID = @PermissionId;",
            new { PermissionId = restorePermission.PermissionID });
        await CreateAdministrationRepository().GetPermissionsAsync();
        (await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM role_permissions WHERE PermissionID = @PermissionId;",
            new { PermissionId = restorePermission.PermissionID })).Should().Be(0);
    }

    [Fact]
    public async Task UserRepository_LoadsMaintenanceGrantInFirstSessionAfterCatalogBootstrap()
    {
        await using var connection = new MySqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        await connection.ExecuteAsync("CREATE TABLE IF NOT EXISTS employees (EmployeeID INT AUTO_INCREMENT PRIMARY KEY, UserID INT NULL, Name VARCHAR(100));");
        var roleId = await connection.ExecuteScalarAsync<int>("SELECT RoleID FROM roles WHERE RoleName = @Name;",
            new { Name = SystemRoleDefaults.Administrators });
        var userId = await connection.QuerySingleAsync<int>(
            "INSERT INTO users (Username, DisplayName, PasswordHash, IsActive) VALUES (@Name, 'Bootstrap', 'unused', 1); SELECT LAST_INSERT_ID();",
            new { Name = $"boot-{Guid.NewGuid():N}" });
        await connection.ExecuteAsync("INSERT INTO user_roles (UserID, RoleID) VALUES (@UserId, @RoleId);", new { UserId = userId, RoleId = roleId });
        await connection.ExecuteAsync("DELETE FROM permissions WHERE PermissionCode = 'DatabaseMaintenance.View';");
        var database = new DatabaseConnection(fixture.ConnectionString);
        Func<FluentCommandBuilder> factory = () => FluentCommandBuilder.Create(new MariaDBDialect());
        var administration = new UserAdministrationRepository(database, factory);
        var repository = new UserRepository(database, factory, administration);

        var user = await repository.GetByUserIdAsync(userId);

        user.Should().NotBeNull();
        user!.Permissions.Should().Contain(DecorPermissions.DatabaseMaintenanceView);
    }
}