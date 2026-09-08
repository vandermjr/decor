using Dapper;
using Decor.Core.Common;
using Decor.Core.DTOs;
using Decor.Core.Entities;
using Decor.FluentSqlBuilder;
using Decor.FluentSqlBuilder.Dialects.MariaDB;
using Decor.Infrastructure.Data;
using Decor.Infrastructure.Data.Repositories;
using MySqlConnector;

namespace Decor.Infrastructure.IntegrationTests;

public sealed class AdminSecurityCriticalIntegrationTests(MariaDbFixture fixture) : IClassFixture<MariaDbFixture>
{
    private const string SeedAdminUsername = "admin";
    private const string SeedAdminRoleName = SystemRoleDefaults.Administrator;

    [Fact]
    public async Task SetActivePreservingLastAdministratorAsync_WhenOnlyActiveAdministratorIsDeactivated_ThrowsAndKeepsUserActive()
    {
        await using var connection = new MySqlConnection(fixture.ConnectionString);
        await using var isolation = await PrepareIsolationAsync(connection);

        var adminRoleId = await GetOrCreateRoleAsync(connection, SeedAdminRoleName);
        await SetSeedAdminStateAsync(connection, isActive: false, roleName: SeedAdminRoleName);

        var userId = await CreateUserAsync(connection, UniqueName("adm"), "Administrador único");
        await AssignRoleAsync(connection, userId, adminRoleId);

        var repository = CreateUserAdministrationRepository();

        var act = async () => await repository.SetActivePreservingLastAdministratorAsync(userId, false, adminRoleId);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*último Administrador ativo*");

        var isActive = await connection.QuerySingleAsync<int>(
            "SELECT IsActive FROM users WHERE UserID = @UserId;",
            new { UserId = userId });

        isActive.Should().Be(1);

    }

    [Fact]
    public async Task SetActivePreservingLastAdministratorAsync_WhenInactiveAdministratorExists_ThrowsAndDoesNotCountInactiveAdmin()
    {
        await using var connection = new MySqlConnection(fixture.ConnectionString);
        await using var isolation = await PrepareIsolationAsync(connection);

        var adminRoleId = await GetOrCreateRoleAsync(connection, SeedAdminRoleName);
        await SetSeedAdminStateAsync(connection, isActive: false, roleName: SeedAdminRoleName);

        var activeUserId = await CreateUserAsync(connection, UniqueName("adm"), "Admin ativo");
        var inactiveUserId = await CreateUserAsync(connection, UniqueName("adm"), "Admin inativo", isActive: false);
        await AssignRoleAsync(connection, activeUserId, adminRoleId);
        await AssignRoleAsync(connection, inactiveUserId, adminRoleId);

        var repository = CreateUserAdministrationRepository();

        var act = async () => await repository.SetActivePreservingLastAdministratorAsync(activeUserId, false, adminRoleId);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*último Administrador ativo*");

        var activeCount = await connection.QuerySingleAsync<int>(
            "SELECT COUNT(*) FROM users u INNER JOIN user_roles ur ON ur.UserID = u.UserID WHERE ur.RoleID = @RoleId AND u.IsActive = 1;",
            new { RoleId = adminRoleId });

        activeCount.Should().Be(1);

    }

    [Fact]
    public async Task SetActivePreservingLastAdministratorAsync_WhenTwoActiveAdministratorsExist_AllowsDeactivationForOne()
    {
        await using var connection = new MySqlConnection(fixture.ConnectionString);
        await using var isolation = await PrepareIsolationAsync(connection);

        var adminRoleId = await GetOrCreateRoleAsync(connection, SeedAdminRoleName);
        await SetSeedAdminStateAsync(connection, isActive: false, roleName: SeedAdminRoleName);

        var firstUserId = await CreateUserAsync(connection, UniqueName("adm"), "Primeiro admin");
        var secondUserId = await CreateUserAsync(connection, UniqueName("adm"), "Segundo admin");
        await AssignRoleAsync(connection, firstUserId, adminRoleId);
        await AssignRoleAsync(connection, secondUserId, adminRoleId);

        var repository = CreateUserAdministrationRepository();

        var result = await repository.SetActivePreservingLastAdministratorAsync(firstUserId, false, adminRoleId);

        result.Should().BeTrue();

        var activeCount = await connection.QuerySingleAsync<int>(
            "SELECT COUNT(*) FROM users u INNER JOIN user_roles ur ON ur.UserID = u.UserID WHERE ur.RoleID = @RoleId AND u.IsActive = 1;",
            new { RoleId = adminRoleId });

        activeCount.Should().Be(1);
        (await connection.QuerySingleAsync<int>("SELECT IsActive FROM users WHERE UserID = @UserId;", new { UserId = firstUserId })).Should().Be(0);

    }

    [Fact]
    public async Task ReplaceRolesPreservingLastAdministratorAsync_WhenOnlyActiveAdministratorWouldLoseAdminRole_ThrowsAndPreservesRole()
    {
        await using var connection = new MySqlConnection(fixture.ConnectionString);
        await using var isolation = await PrepareIsolationAsync(connection);

        var adminRoleId = await GetOrCreateRoleAsync(connection, SeedAdminRoleName);
        var otherRoleId = await GetOrCreateRoleAsync(connection, UniqueRoleName("role"));
        await SetSeedAdminStateAsync(connection, isActive: false, roleName: SeedAdminRoleName);

        var userId = await CreateUserAsync(connection, UniqueName("adm"), "Admin para substituir");
        await AssignRoleAsync(connection, userId, adminRoleId);

        var repository = CreateUserAdministrationRepository();

        var act = async () => await repository.ReplaceRolesPreservingLastAdministratorAsync(userId, [otherRoleId], adminRoleId);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*último Administrador ativo*");

        var roles = (await connection.QueryAsync<int>("SELECT RoleID FROM user_roles WHERE UserID = @UserId ORDER BY RoleID;", new { UserId = userId })).ToArray();

        roles.Should().ContainSingle().Which.Should().Be(adminRoleId);

    }

    [Fact]
    public async Task ReplaceRolesPreservingLastAdministratorAsync_WhenTwoActiveAdministratorsExist_AllowsRemovingAdminRoleFromOne()
    {
        await using var connection = new MySqlConnection(fixture.ConnectionString);
        await using var isolation = await PrepareIsolationAsync(connection);

        var adminRoleId = await GetOrCreateRoleAsync(connection, SeedAdminRoleName);
        var otherRoleId = await GetOrCreateRoleAsync(connection, UniqueRoleName("role"));
        await SetSeedAdminStateAsync(connection, isActive: false, roleName: SeedAdminRoleName);

        var firstUserId = await CreateUserAsync(connection, UniqueName("adm"), "Admin A");
        var secondUserId = await CreateUserAsync(connection, UniqueName("adm"), "Admin B");
        await AssignRoleAsync(connection, firstUserId, adminRoleId);
        await AssignRoleAsync(connection, secondUserId, adminRoleId);

        var repository = CreateUserAdministrationRepository();

        await repository.ReplaceRolesPreservingLastAdministratorAsync(firstUserId, [otherRoleId], adminRoleId);

        var firstRoles = (await connection.QueryAsync<int>("SELECT RoleID FROM user_roles WHERE UserID = @UserId ORDER BY RoleID;", new { UserId = firstUserId })).ToArray();
        var secondRoles = (await connection.QueryAsync<int>("SELECT RoleID FROM user_roles WHERE UserID = @UserId ORDER BY RoleID;", new { UserId = secondUserId })).ToArray();

        firstRoles.Should().ContainSingle().Which.Should().Be(otherRoleId);
        secondRoles.Should().ContainSingle().Which.Should().Be(adminRoleId);

    }

    [Fact]
    public async Task CreateWithRolesAsync_WhenRoleAssignmentFails_RollsBackTheUserCreation()
    {
        await using var connection = new MySqlConnection(fixture.ConnectionString);
        await using var isolation = await PrepareIsolationAsync(connection);

        var repository = CreateUserAdministrationRepository();
        var username = UniqueName("crt");

        var act = async () => await repository.CreateWithRolesAsync(username, "Display", "hash", [999999]);

        await act.Should().ThrowAsync<MySqlException>();

        var count = await connection.QuerySingleAsync<int>("SELECT COUNT(*) FROM users WHERE Username = @Username;", new { Username = username });
        count.Should().Be(0);

    }

    [Fact]
    public async Task ReplaceRolesPreservingLastAdministratorAsync_WhenInsertFails_RollsBackPreviousRoles()
    {
        await using var connection = new MySqlConnection(fixture.ConnectionString);
        await using var isolation = await PrepareIsolationAsync(connection);

        var adminRoleId = await GetOrCreateRoleAsync(connection, SeedAdminRoleName);
        var secondAdminUserId = await CreateUserAsync(connection, UniqueName("adm"), "Segundo admin");
        await AssignRoleAsync(connection, secondAdminUserId, adminRoleId);

        var repository = CreateUserAdministrationRepository();
        var validRoleId = await GetOrCreateRoleAsync(connection, UniqueRoleName("role"));
        var userId = await CreateUserAsync(connection, UniqueName("rbr"), "User roles rollback");
        await AssignRoleAsync(connection, userId, validRoleId);

        var act = async () => await repository.ReplaceRolesPreservingLastAdministratorAsync(userId, [999999], adminRoleId);

        await act.Should().ThrowAsync<MySqlException>();

        var roles = (await connection.QueryAsync<int>("SELECT RoleID FROM user_roles WHERE UserID = @UserId ORDER BY RoleID;", new { UserId = userId })).ToArray();
        roles.Should().ContainSingle().Which.Should().Be(validRoleId);

    }

    [Fact]
    public async Task ReplacePermissionOverridesAsync_WhenInsertFails_RollsBackPreviousOverrides()
    {
        await using var connection = new MySqlConnection(fixture.ConnectionString);
        await using var isolation = await PrepareIsolationAsync(connection);

        var repository = CreateUserAdministrationRepository();
        var permissionId = await GetOrCreatePermissionAsync(connection, UniquePermissionName("perm"));
        var userId = await CreateUserAsync(connection, UniqueName("ovr"), "Override rollback");
        await connection.ExecuteAsync(
            "INSERT INTO user_permission_overrides (UserID, PermissionID, IsGranted) VALUES (@UserId, @PermissionId, 1);",
            new { UserId = userId, PermissionId = permissionId });

        var act = async () => await repository.ReplacePermissionOverridesAsync(userId, [new PermissionOverrideDTO(999999, "missing", true)]);

        await act.Should().ThrowAsync<MySqlException>();

        var persisted = (await connection.QueryAsync<(int PermissionID, int IsGranted)>(
            "SELECT PermissionID, IsGranted FROM user_permission_overrides WHERE UserID = @UserId;",
            new { UserId = userId })).ToArray();

        persisted.Should().ContainSingle();
        persisted[0].PermissionID.Should().Be(permissionId);
        persisted[0].IsGranted.Should().Be(1);

    }

    [Fact]
    public async Task RolePermissionsReplace_WhenInsertFails_RollsBackPreviousPermissions()
    {
        await using var connection = new MySqlConnection(fixture.ConnectionString);
        await using var isolation = await PrepareIsolationAsync(connection);

        var roleId = await GetOrCreateRoleAsync(connection, UniqueRoleName("role"));
        var permissionId = await GetOrCreatePermissionAsync(connection, UniquePermissionName("perm"));
        await connection.ExecuteAsync(
            "INSERT INTO role_permissions (RoleID, PermissionID) VALUES (@RoleId, @PermissionId);",
            new { RoleId = roleId, PermissionId = permissionId });

        var repository = new RoleAdministrationRepository(new DatabaseConnection(fixture.ConnectionString), () => FluentCommandBuilder.Create(new MariaDBDialect()));

        var act = async () => await repository.ReplacePermissionsAsync(roleId, [999999]);

        await act.Should().ThrowAsync<MySqlException>();

        var currentCount = await connection.QuerySingleAsync<int>(
            "SELECT COUNT(*) FROM role_permissions WHERE RoleID = @RoleId AND PermissionID = @PermissionId;",
            new { RoleId = roleId, PermissionId = permissionId });

        currentCount.Should().Be(1);

    }

    [Fact]
    public async Task UserPermissionOverrides_CheckConstraint_AllowsZeroAndOne_RejectsTwo()
    {
        await using var connection = new MySqlConnection(fixture.ConnectionString);
        await using var isolation = await PrepareIsolationAsync(connection);

        var userId = await CreateUserAsync(connection, UniqueName("chk"), "Check user");
        var zeroPermissionId = await GetOrCreatePermissionAsync(connection, UniquePermissionName("zero"));
        var onePermissionId = await GetOrCreatePermissionAsync(connection, UniquePermissionName("one"));
        var invalidPermissionId = await GetOrCreatePermissionAsync(connection, UniquePermissionName("bad"));

        await connection.ExecuteAsync(
            "INSERT INTO user_permission_overrides (UserID, PermissionID, IsGranted) VALUES (@UserId, @PermissionId, 0);",
            new { UserId = userId, PermissionId = zeroPermissionId });

        await connection.ExecuteAsync(
            "INSERT INTO user_permission_overrides (UserID, PermissionID, IsGranted) VALUES (@UserId, @PermissionId, 1);",
            new { UserId = userId, PermissionId = onePermissionId });

        var act = async () => await connection.ExecuteAsync(
            "INSERT INTO user_permission_overrides (UserID, PermissionID, IsGranted) VALUES (@UserId, @PermissionId, 2);",
            new { UserId = userId, PermissionId = invalidPermissionId });

        await act.Should().ThrowAsync<MySqlException>();

    }

    private UserAdministrationRepository CreateUserAdministrationRepository() => new(new DatabaseConnection(fixture.ConnectionString), () => FluentCommandBuilder.Create(new MariaDBDialect()));

    private async Task<SeedAdminIsolation> PrepareIsolationAsync(MySqlConnection connection)
    {
        var originalSeedState = await CaptureSeedAdminStateAsync(connection);
        await CleanupDistinctTestDataAsync(connection);
        var adminRoleId = await GetOrCreateRoleAsync(connection, SeedAdminRoleName);
        await RestoreSeedAdminStateAsync(connection, adminRoleId);
        return new SeedAdminIsolation(connection, originalSeedState, CleanupDistinctTestDataAsync);
    }

    private async Task<SeedAdminState> CaptureSeedAdminStateAsync(MySqlConnection connection)
    {
        var seedUserId = await connection.QuerySingleAsync<int>(
            "SELECT UserID FROM users WHERE Username = @Username;",
            new { Username = SeedAdminUsername });
        var isActive = await connection.QuerySingleAsync<bool>(
            "SELECT IsActive FROM users WHERE UserID = @UserId;",
            new { UserId = seedUserId });
        var roleIds = (await connection.QueryAsync<int>(
            "SELECT RoleID FROM user_roles WHERE UserID = @UserId ORDER BY RoleID;",
            new { UserId = seedUserId })).ToArray();

        return new SeedAdminState(seedUserId, isActive, roleIds);
    }

    private async Task RestoreSeedAdminStateAsync(MySqlConnection connection, int adminRoleId)
    {
        await connection.ExecuteAsync(
            "UPDATE users SET IsActive = 1 WHERE Username = @Username;",
            new { Username = SeedAdminUsername });

        await connection.ExecuteAsync(
            "INSERT IGNORE INTO user_roles (UserID, RoleID) SELECT u.UserID, @RoleId FROM users u WHERE u.Username = @Username;",
            new { Username = SeedAdminUsername, RoleId = adminRoleId });
    }

    private async Task SetSeedAdminStateAsync(MySqlConnection connection, bool isActive, string roleName)
    {
        await connection.ExecuteAsync(
            "UPDATE users SET IsActive = @IsActive WHERE Username = @Username;",
            new { IsActive = isActive ? 1 : 0, Username = SeedAdminUsername });

        var adminRoleId = await GetOrCreateRoleAsync(connection, roleName);
        if (isActive)
        {
            await connection.ExecuteAsync(
                "INSERT IGNORE INTO user_roles (UserID, RoleID) SELECT u.UserID, @RoleId FROM users u WHERE u.Username = @Username;",
                new { Username = SeedAdminUsername, RoleId = adminRoleId });
        }
        else
        {
            await connection.ExecuteAsync(
                "DELETE FROM user_roles WHERE UserID = (SELECT UserID FROM users WHERE Username = @Username) AND RoleID = @RoleId;",
                new { Username = SeedAdminUsername, RoleId = adminRoleId });
        }
    }

    private async Task CleanupDistinctTestDataAsync(MySqlConnection connection)
    {
        await connection.ExecuteAsync(
            "DELETE FROM user_permission_overrides WHERE UserID IN (SELECT UserID FROM users WHERE Username LIKE @UserLike);",
            new { UserLike = "f1_%" });

        await connection.ExecuteAsync(
            "DELETE FROM user_roles WHERE UserID IN (SELECT UserID FROM users WHERE Username LIKE @UserLike);",
            new { UserLike = "f1_%" });

        await connection.ExecuteAsync(
            "DELETE FROM users WHERE Username LIKE @UserLike;",
            new { UserLike = "f1_%" });

        await connection.ExecuteAsync(
            "DELETE FROM role_permissions WHERE RoleID IN (SELECT RoleID FROM roles WHERE RoleName LIKE @RoleLike);",
            new { RoleLike = "f1_%" });

        await connection.ExecuteAsync(
            "DELETE FROM roles WHERE RoleName LIKE @RoleLike;",
            new { RoleLike = "f1_%" });

        await connection.ExecuteAsync(
            "DELETE FROM permissions WHERE PermissionCode LIKE @PermLike;",
            new { PermLike = "f1_%" });
    }

    private static string UniqueName(string prefix, int maxLength = 20)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var candidate = $"f1_{prefix}_{suffix}";
        return candidate.Length > maxLength ? candidate[..maxLength] : candidate;
    }

    private static string UniqueRoleName(string prefix) => UniqueName(prefix, 40);
    private static string UniquePermissionName(string prefix) => UniqueName(prefix, 50);

    private async Task<int> GetOrCreateRoleAsync(MySqlConnection connection, string roleName)
    {
        var existingRoleId = await connection.QuerySingleOrDefaultAsync<int?>(
            "SELECT RoleID FROM roles WHERE RoleName = @RoleName LIMIT 1;",
            new { RoleName = roleName });

        if (existingRoleId.HasValue)
        {
            return existingRoleId.Value;
        }

        return await connection.QuerySingleAsync<int>(
            "INSERT INTO roles (RoleName, Description, HierarchyLevel, IsSystemProtected) VALUES (@RoleName, @Description, @HierarchyLevel, @IsSystemProtected); SELECT LAST_INSERT_ID();",
            new
            {
                RoleName = roleName,
                Description = "role de teste",
                HierarchyLevel = 50,
                IsSystemProtected = 0
            });
    }

    private async Task<int> GetOrCreatePermissionAsync(MySqlConnection connection, string permissionCode)
    {
        var existingPermissionId = await connection.QuerySingleOrDefaultAsync<int?>(
            "SELECT PermissionID FROM permissions WHERE PermissionCode = @PermissionCode LIMIT 1;",
            new { PermissionCode = permissionCode });

        if (existingPermissionId.HasValue)
        {
            return existingPermissionId.Value;
        }

        return await connection.QuerySingleAsync<int>(
            "INSERT INTO permissions (PermissionCode, Description) VALUES (@PermissionCode, @Description); SELECT LAST_INSERT_ID();",
            new { PermissionCode = permissionCode, Description = "permissão de teste" });
    }

    private async Task<int> CreateUserAsync(MySqlConnection connection, string username, string displayName, bool isActive = true)
    {
        return await connection.QuerySingleAsync<int>(
            "INSERT INTO users (Username, DisplayName, PasswordHash, IsActive, MustChangePassword) VALUES (@Username, @DisplayName, @PasswordHash, @IsActive, 0); SELECT LAST_INSERT_ID();",
            new { Username = username, DisplayName = displayName, PasswordHash = $"hash-{Guid.NewGuid():N}", IsActive = isActive ? 1 : 0 });
    }

    private async Task AssignRoleAsync(MySqlConnection connection, int userId, int roleId)
    {
        await connection.ExecuteAsync(
            "INSERT INTO user_roles (UserID, RoleID) VALUES (@UserId, @RoleId);",
            new { UserId = userId, RoleId = roleId });
    }

    private sealed class SeedAdminIsolation(
        MySqlConnection connection,
        SeedAdminState originalSeedState,
        Func<MySqlConnection, Task> cleanupTestData)
        : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => new(RestoreAndCleanupAsync());

        private async Task RestoreAndCleanupAsync()
        {
            try
            {
                await connection.ExecuteAsync(
                    "UPDATE users SET IsActive = @IsActive WHERE UserID = @UserId;",
                    new { UserId = originalSeedState.UserId, IsActive = originalSeedState.IsActive });
                await connection.ExecuteAsync(
                    "DELETE FROM user_roles WHERE UserID = @UserId;",
                    new { UserId = originalSeedState.UserId });
                if (originalSeedState.RoleIds.Length > 0)
                {
                    await connection.ExecuteAsync(
                        "INSERT INTO user_roles (UserID, RoleID) VALUES (@UserId, @RoleId);",
                        originalSeedState.RoleIds.Select(roleId => new { UserId = originalSeedState.UserId, RoleId = roleId }));
                }
            }
            finally
            {
                await cleanupTestData(connection);
            }
        }
    }

    private sealed record SeedAdminState(int UserId, bool IsActive, int[] RoleIds);
}
