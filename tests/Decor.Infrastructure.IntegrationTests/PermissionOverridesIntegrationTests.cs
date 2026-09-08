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

public sealed class PermissionOverridesIntegrationTests(MariaDbFixture fixture) : IClassFixture<MariaDbFixture>
{
    [Fact]
    public async Task UserRepository_GetByUsernameAsync_WhenRoleGrantIsDeniedByOverride_DoesNotIncludePermission()
    {
        await using var connection = new MySqlConnection(fixture.ConnectionString);
        await using var isolation = await PrepareIsolationAsync(connection);

        var username = UniqueName("user");
        var permissionCode = UniquePermissionCode("perm");
        var roleName = UniqueRoleName("role");

        var permissionId = await GetOrCreatePermissionAsync(connection, permissionCode);
        var roleId = await GetOrCreateRoleAsync(connection, roleName);
        var userId = await CreateUserAsync(connection, username, "Usuário com permissão negada");

        await AssignRoleAsync(connection, userId, roleId);
        await AssignPermissionToRoleAsync(connection, roleId, permissionId);
        await connection.ExecuteAsync(
            "INSERT INTO user_permission_overrides (UserID, PermissionID, IsGranted) VALUES (@UserId, @PermissionId, 0);",
            new { UserId = userId, PermissionId = permissionId });

        var repository = new UserRepository(new DatabaseConnection(fixture.ConnectionString), () => FluentCommandBuilder.Create(new MariaDBDialect()));

        var user = await repository.GetByUsernameAsync(username);

        user.Should().NotBeNull();
        user!.Permissions.Should().NotContain(permissionCode);
    }

    [Fact]
    public async Task UserRepository_GetByUsernameAsync_WhenNoRoleGrantsPermissionButOverrideAllows_DoesIncludePermission()
    {
        await using var connection = new MySqlConnection(fixture.ConnectionString);
        await using var isolation = await PrepareIsolationAsync(connection);

        var username = UniqueName("user");
        var permissionCode = UniquePermissionCode("perm");
        var permissionId = await GetOrCreatePermissionAsync(connection, permissionCode);
        var userId = await CreateUserAsync(connection, username, "Usuário com override positivo");

        await connection.ExecuteAsync(
            "INSERT INTO user_permission_overrides (UserID, PermissionID, IsGranted) VALUES (@UserId, @PermissionId, 1);",
            new { UserId = userId, PermissionId = permissionId });

        var repository = new UserRepository(new DatabaseConnection(fixture.ConnectionString), () => FluentCommandBuilder.Create(new MariaDBDialect()));

        var user = await repository.GetByUsernameAsync(username);

        user.Should().NotBeNull();
        user!.Permissions.Should().Contain(permissionCode);
    }

    [Fact]
    public async Task UserRepository_GetByUsernameAsync_WhenMultipleRolesGrantSamePermissionAndOverrideDenies_PermissionDoesNotAppear()
    {
        await using var connection = new MySqlConnection(fixture.ConnectionString);
        await using var isolation = await PrepareIsolationAsync(connection);

        var username = UniqueName("user");
        var permissionCode = UniquePermissionCode("perm");
        var permissionId = await GetOrCreatePermissionAsync(connection, permissionCode);
        var roleAId = await GetOrCreateRoleAsync(connection, UniqueRoleName("roleA"));
        var roleBId = await GetOrCreateRoleAsync(connection, UniqueRoleName("roleB"));
        var userId = await CreateUserAsync(connection, username, "Usuário com múltiplas roles");

        await AssignRoleAsync(connection, userId, roleAId);
        await AssignRoleAsync(connection, userId, roleBId);
        await AssignPermissionToRoleAsync(connection, roleAId, permissionId);
        await AssignPermissionToRoleAsync(connection, roleBId, permissionId);
        await connection.ExecuteAsync(
            "INSERT INTO user_permission_overrides (UserID, PermissionID, IsGranted) VALUES (@UserId, @PermissionId, 0);",
            new { UserId = userId, PermissionId = permissionId });

        var repository = new UserRepository(new DatabaseConnection(fixture.ConnectionString), () => FluentCommandBuilder.Create(new MariaDBDialect()));

        var user = await repository.GetByUsernameAsync(username);

        user.Should().NotBeNull();
        user!.Permissions.Should().NotContain(permissionCode);
    }

    [Fact]
    public async Task UserPermissionOverrides_PrimaryKeyRejectsDuplicateUserPermissionRows()
    {
        await using var connection = new MySqlConnection(fixture.ConnectionString);
        await using var isolation = await PrepareIsolationAsync(connection);

        var userId = await CreateUserAsync(connection, UniqueName("user"), "Usuário para chave primária");
        var permissionCode = UniquePermissionCode("perm");
        var permissionId = await GetOrCreatePermissionAsync(connection, permissionCode);

        await connection.ExecuteAsync(
            "INSERT INTO user_permission_overrides (UserID, PermissionID, IsGranted) VALUES (@UserId, @PermissionId, 1);",
            new { UserId = userId, PermissionId = permissionId });

        var act = async () => await connection.ExecuteAsync(
            "INSERT INTO user_permission_overrides (UserID, PermissionID, IsGranted) VALUES (@UserId, @PermissionId, 0);",
            new { UserId = userId, PermissionId = permissionId });

        await act.Should().ThrowAsync<MySqlException>();
    }

    [Fact]
    public async Task UserAdministrationRepository_ReplacePermissionOverridesAsync_ReplacesPreviousStateAndEffectivePermissionsReflectFinalState()
    {
        await using var connection = new MySqlConnection(fixture.ConnectionString);
        await using var isolation = await PrepareIsolationAsync(connection);

        var username = UniqueName("user");
        var firstPermissionCode = UniquePermissionCode("first");
        var secondPermissionCode = UniquePermissionCode("second");

        var firstPermissionId = await GetOrCreatePermissionAsync(connection, firstPermissionCode);
        var secondPermissionId = await GetOrCreatePermissionAsync(connection, secondPermissionCode);
        var userId = await CreateUserAsync(connection, username, "Usuário com substituição de overrides");

        var repository = new UserAdministrationRepository(new DatabaseConnection(fixture.ConnectionString), () => FluentCommandBuilder.Create(new MariaDBDialect()));
        var userRepository = new UserRepository(new DatabaseConnection(fixture.ConnectionString), () => FluentCommandBuilder.Create(new MariaDBDialect()));

        await connection.ExecuteAsync(
            "INSERT INTO user_permission_overrides (UserID, PermissionID, IsGranted) VALUES (@UserId, @PermissionId, 1);",
            new { UserId = userId, PermissionId = firstPermissionId });

        (await userRepository.GetByUsernameAsync(username))!.Permissions.Should().Contain(firstPermissionCode);

        await repository.ReplacePermissionOverridesAsync(
            userId,
            [
                new PermissionOverrideDTO(firstPermissionId, firstPermissionCode, false),
                new PermissionOverrideDTO(secondPermissionId, secondPermissionCode, true)
            ]);

        var persisted = (await connection.QueryAsync<(int PermissionID, int IsGranted)>(
            "SELECT PermissionID, IsGranted FROM user_permission_overrides WHERE UserID = @UserId ORDER BY PermissionID;",
            new { UserId = userId })).ToArray();

        persisted.Should().HaveCount(2);
        persisted.Should().Contain(x => x.PermissionID == firstPermissionId && x.IsGranted == 0);
        persisted.Should().Contain(x => x.PermissionID == secondPermissionId && x.IsGranted == 1);

        var finalUser = await userRepository.GetByUsernameAsync(username);
        finalUser.Should().NotBeNull();
        finalUser!.Permissions.Should().BeEquivalentTo([secondPermissionCode]);
    }

    private async Task<SeedIsolation> PrepareIsolationAsync(MySqlConnection connection)
    {
        await CleanupDistinctTestDataAsync(connection);
        return new SeedIsolation(connection, CleanupDistinctTestDataAsync);
    }

    private async Task CleanupDistinctTestDataAsync(MySqlConnection connection)
    {
        await connection.ExecuteAsync(
            "DELETE FROM user_permission_overrides WHERE UserID IN (SELECT UserID FROM users WHERE Username LIKE @UserLike);",
            new { UserLike = "f2_%" });

        await connection.ExecuteAsync(
            "DELETE FROM user_roles WHERE UserID IN (SELECT UserID FROM users WHERE Username LIKE @UserLike);",
            new { UserLike = "f2_%" });

        await connection.ExecuteAsync(
            "DELETE FROM role_permissions WHERE RoleID IN (SELECT RoleID FROM roles WHERE RoleName LIKE @RoleLike);",
            new { RoleLike = "f2_%" });

        await connection.ExecuteAsync(
            "DELETE FROM users WHERE Username LIKE @UserLike;",
            new { UserLike = "f2_%" });

        await connection.ExecuteAsync(
            "DELETE FROM roles WHERE RoleName LIKE @RoleLike;",
            new { RoleLike = "f2_%" });

        await connection.ExecuteAsync(
            "DELETE FROM permissions WHERE PermissionCode LIKE @PermissionLike;",
            new { PermissionLike = "f2_%" });
    }

    private static string UniqueName(string prefix) => $"f2_{prefix}_{Guid.NewGuid():N}";
    private static string UniqueRoleName(string prefix) => $"f2_{prefix}_{Guid.NewGuid():N}";
    private static string UniquePermissionCode(string prefix) => $"f2_{prefix}_{Guid.NewGuid():N}";

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
            new { RoleName = roleName, Description = "role de teste", HierarchyLevel = 50, IsSystemProtected = 0 });
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

    private async Task AssignPermissionToRoleAsync(MySqlConnection connection, int roleId, int permissionId)
    {
        await connection.ExecuteAsync(
            "INSERT INTO role_permissions (RoleID, PermissionID) VALUES (@RoleId, @PermissionId);",
            new { RoleId = roleId, PermissionId = permissionId });
    }

    private sealed class SeedIsolation(MySqlConnection connection, Func<MySqlConnection, Task> cleanup) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => new(CleanupAsync());

        private async Task CleanupAsync()
        {
            try
            {
                await cleanup(connection);
            }
            catch
            {
                // no-op: the fixture isolates while the transaction is in scope and cleanup is best-effort for test data.
            }
        }
    }
}
