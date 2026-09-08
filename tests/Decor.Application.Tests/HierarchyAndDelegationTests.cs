using Decor.Application.Services;
using Decor.Core.Common;
using Decor.Core.DTOs;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Repositories;
using Decor.Core.Interfaces.Services;

namespace Decor.Application.Tests;

public sealed class HierarchyAndDelegationTests
{
    [Fact]
    public async Task UserAdministrationService_SearchAsync_FiltersUsersBelowOperatorLevel()
    {
        var operatorUser = CreateApplicationUser(
            userId: 1,
            username: "supervisor",
            roles: [new AdministrativeRoleDTO(2, "Supervisor", null, 20, false)],
            permissions: [DecorPermissions.UsersView]);

        var sameLevelUser = CreateAdministrativeUser(
            userId: 10,
            username: "mesmo-nivel",
            roles: [new AdministrativeRoleDTO(2, "Supervisor", null, 20, false)]);

        var lowerLevelUser = CreateAdministrativeUser(
            userId: 11,
            username: "inferior",
            roles: [new AdministrativeRoleDTO(3, "Operador", null, 10, false)]);

        var higherLevelUser = CreateAdministrativeUser(
            userId: 12,
            username: "superior",
            roles: [new AdministrativeRoleDTO(4, "Gerente", null, 30, false)]);

        var service = CreateUserAdministrationService(
            operatorUser,
            users: [sameLevelUser, lowerLevelUser, higherLevelUser],
            roles: [
                new AdministrativeRoleDTO(2, "Supervisor", null, 20, false),
                new AdministrativeRoleDTO(3, "Operador", null, 10, false),
                new AdministrativeRoleDTO(4, "Gerente", null, 30, false)]);

        var result = await service.SearchAsync(null);

        result.Select(u => u.UserID).Should().Equal([11]);
    }

    [Theory]
    [InlineData(20)]
    [InlineData(30)]
    public async Task UserAdministrationService_GetByIdAsync_WhenTargetIsSameOrHigherLevel_Throws(int targetLevel)
    {
        var operatorUser = CreateApplicationUser(
            userId: 1,
            username: "supervisor",
            roles: [new AdministrativeRoleDTO(2, "Supervisor", null, 20, false)],
            permissions: [DecorPermissions.UsersView]);

        var targetUser = CreateAdministrativeUser(
            userId: 99,
            username: "alvo",
            roles: [new AdministrativeRoleDTO(5, "NivelAlvo", null, targetLevel, false)]);

        var service = CreateUserAdministrationService(
            operatorUser,
            users: [targetUser],
            roles: [
                new AdministrativeRoleDTO(2, "Supervisor", null, 20, false),
                new AdministrativeRoleDTO(5, "NivelAlvo", null, targetLevel, false)]);

        var act = async () => await service.GetByIdAsync(targetUser.UserID);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task UserAdministrationService_GetByIdAsync_WhenTargetIsLowerLevel_Allows()
    {
        var operatorUser = CreateApplicationUser(
            userId: 1,
            username: "supervisor",
            roles: [new AdministrativeRoleDTO(2, "Supervisor", null, 20, false)],
            permissions: [DecorPermissions.UsersView]);

        var targetUser = CreateAdministrativeUser(
            userId: 81,
            username: "inferior",
            roles: [new AdministrativeRoleDTO(3, "Operador", null, 10, false)]);

        var service = CreateUserAdministrationService(
            operatorUser,
            users: [targetUser],
            roles: [
                new AdministrativeRoleDTO(2, "Supervisor", null, 20, false),
                new AdministrativeRoleDTO(3, "Operador", null, 10, false)]);

        var result = await service.GetByIdAsync(targetUser.UserID);

        result.Should().NotBeNull();
        result!.UserID.Should().Be(targetUser.UserID);
    }

    [Theory]
    [InlineData(20)]
    [InlineData(30)]
    public async Task UserAdministrationService_ReplaceRolesAsync_WhenAssigningEqualOrHigherRole_Throws(int roleLevel)
    {
        var operatorUser = CreateApplicationUser(
            userId: 1,
            username: "supervisor",
            roles: [new AdministrativeRoleDTO(2, "Supervisor", null, 20, false)],
            permissions: [DecorPermissions.UsersAssignRoles]);

        var targetUser = CreateAdministrativeUser(
            userId: 22,
            username: "alvo",
            roles: [new AdministrativeRoleDTO(3, "Operador", null, 10, false)]);

        var service = CreateUserAdministrationService(
            operatorUser,
            users: [targetUser],
            roles: [
                new AdministrativeRoleDTO(2, "Supervisor", null, 20, false),
                new AdministrativeRoleDTO(3, "Operador", null, 10, false),
                new AdministrativeRoleDTO(4, "NivelAlvo", null, roleLevel, false)]);

        var act = async () => await service.ReplaceRolesAsync(targetUser.UserID, [4]);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task UserAdministrationService_ReplaceRolesAsync_WhenAssigningLowerRole_Allows()
    {
        var operatorUser = CreateApplicationUser(
            userId: 1,
            username: "supervisor",
            roles: [new AdministrativeRoleDTO(2, "Supervisor", null, 20, false)],
            permissions: [DecorPermissions.UsersAssignRoles]);

        var targetUser = CreateAdministrativeUser(
            userId: 22,
            username: "alvo",
            roles: [new AdministrativeRoleDTO(3, "Operador", null, 10, false)]);

        var service = CreateUserAdministrationService(
            operatorUser,
            users: [targetUser],
            roles: [
                new AdministrativeRoleDTO(1, SystemRoleDefaults.Administrator, null, 100, true),
                new AdministrativeRoleDTO(2, "Supervisor", null, 20, false),
                new AdministrativeRoleDTO(3, "Operador", null, 10, false)]);

        var act = async () => await service.ReplaceRolesAsync(targetUser.UserID, [3]);

        await act.Should().NotThrowAsync();
    }

    [Theory]
    [InlineData(20)]
    [InlineData(30)]
    public async Task RoleAdministrationService_ReplacePermissionsAsync_WhenTargetRoleIsEqualOrHigher_Throws(int targetRoleLevel)
    {
        var operatorUser = CreateApplicationUser(
            userId: 1,
            username: "supervisor",
            roles: [new AdministrativeRoleDTO(2, "Supervisor", null, 20, false)],
            permissions: [DecorPermissions.RolesManagePermissions]);

        var roleId = 8;
        var role = new AdministrativeRoleDTO(roleId, "Alvo", null, targetRoleLevel, false);
        var roleRepository = new TrackingRoleAdministrationRepository();
        var userRepository = new FakeUserAdministrationRepository(
            users: [],
            roles: [
                new AdministrativeRoleDTO(2, "Supervisor", null, 20, false),
                role],
            permissions: [new AdministrativePermissionDTO(10, DecorPermissions.RolesManagePermissions, null)]);

        var context = new AuthenticatedUserContext();
        context.SignIn(operatorUser);

        var service = new RoleAdministrationService(
            userRepository,
            roleRepository,
            context,
            new StaticAuthorizationService([DecorPermissions.RolesManagePermissions]));

        var act = async () => await service.ReplacePermissionsAsync(roleId, [10]);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task RoleAdministrationService_ReplacePermissionsAsync_WhenTargetRoleIsLower_Allows()
    {
        var operatorUser = CreateApplicationUser(
            userId: 1,
            username: "supervisor",
            roles: [new AdministrativeRoleDTO(2, "Supervisor", null, 20, false)],
            permissions: [DecorPermissions.RolesManagePermissions]);

        var roleId = 9;
        var role = new AdministrativeRoleDTO(roleId, "Operador", null, 10, false);
        var roleRepository = new TrackingRoleAdministrationRepository();
        var userRepository = new FakeUserAdministrationRepository(
            users: [],
            roles: [
                new AdministrativeRoleDTO(2, "Supervisor", null, 20, false),
                role],
            permissions: [new AdministrativePermissionDTO(10, DecorPermissions.RolesManagePermissions, null)]);

        var context = new AuthenticatedUserContext();
        context.SignIn(operatorUser);

        var service = new RoleAdministrationService(
            userRepository,
            roleRepository,
            context,
            new StaticAuthorizationService([DecorPermissions.RolesManagePermissions]));

        var act = async () => await service.ReplacePermissionsAsync(roleId, [10]);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task UserAdministrationService_ReplacePermissionOverridesAsync_WhenGrantingWithoutPermission_Throws()
    {
        var operatorUser = CreateApplicationUser(
            userId: 1,
            username: "supervisor",
            roles: [new AdministrativeRoleDTO(2, "Supervisor", null, 20, false)],
            permissions: [DecorPermissions.UsersManagePermissions]);

        var targetUser = CreateAdministrativeUser(
            userId: 55,
            username: "alvo",
            roles: [new AdministrativeRoleDTO(3, "Operador", null, 10, false)]);

        var service = CreateUserAdministrationService(
            operatorUser,
            users: [targetUser],
            roles: [
                new AdministrativeRoleDTO(2, "Supervisor", null, 20, false),
                new AdministrativeRoleDTO(3, "Operador", null, 10, false)]);

        var act = async () => await service.ReplacePermissionOverridesAsync(
            targetUser.UserID,
            [new PermissionOverrideDTO(7, DecorPermissions.RolesRestoreDefaults, true)]);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    private static UserAdministrationService CreateUserAdministrationService(
        ApplicationUser operatorUser,
        IReadOnlyCollection<AdministrativeUserDTO> users,
        IReadOnlyCollection<AdministrativeRoleDTO> roles)
    {
        var context = new AuthenticatedUserContext();
        context.SignIn(operatorUser);

        return new UserAdministrationService(
            new FakeUserAdministrationRepository(users, roles, []),
            new FakePasswordHasher { VerificationResult = true },
            new FakePasswordPolicy(),
            context,
            new StaticAuthorizationService(operatorUser.Permissions));
    }

    private static ApplicationUser CreateApplicationUser(int userId, string username, IReadOnlyCollection<AdministrativeRoleDTO> roles, IReadOnlyCollection<string> permissions)
    {
        return new ApplicationUser
        {
            UserID = userId,
            Username = username,
            DisplayName = username,
            IsActive = true,
            MustChangePassword = false,
            Roles = roles.Select(r => r.RoleName).ToArray(),
            Permissions = permissions.ToArray()
        };
    }

    private static AdministrativeUserDTO CreateAdministrativeUser(int userId, string username, IReadOnlyCollection<AdministrativeRoleDTO> roles)
    {
        return new AdministrativeUserDTO(userId, username, username, true, roles, []);
    }

    private sealed class StaticAuthorizationService(IReadOnlyCollection<string> permissions) : IAuthorizationService
    {
        public bool HasPermission(string permissionCode) => permissions.Contains(permissionCode, StringComparer.OrdinalIgnoreCase);
        public bool CanView(string resource) => false;
        public bool CanCreate(string resource) => false;
        public bool CanEdit(string resource) => false;
        public bool CanDelete(string resource) => false;
    }

    private sealed class FakeUserAdministrationRepository(
        IReadOnlyCollection<AdministrativeUserDTO> users,
        IReadOnlyCollection<AdministrativeRoleDTO> roles,
        IReadOnlyCollection<AdministrativePermissionDTO> permissions) : IUserAdministrationRepository
    {
        public Task<IReadOnlyList<AdministrativeUserDTO>> SearchAsync(string? search, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AdministrativeUserDTO>>(users.ToArray());

        public Task<AdministrativeUserDTO?> GetByIdAsync(int userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(users.FirstOrDefault(u => u.UserID == userId));

        public Task<int> CreateWithRolesAsync(string username, string displayName, string passwordHash, IReadOnlyCollection<int> roleIds, CancellationToken cancellationToken = default) =>
            Task.FromResult(0);

        public Task<bool> UpdateAsync(int userId, string username, string displayName, CancellationToken cancellationToken = default) => Task.FromResult(true);

        public Task<bool> SetActivePreservingLastAdministratorAsync(int userId, bool isActive, int administratorRoleId, CancellationToken cancellationToken = default) => Task.FromResult(true);

        public Task<bool> UpdateTemporaryPasswordAsync(int userId, string passwordHash, CancellationToken cancellationToken = default) => Task.FromResult(true);

        public Task ReplaceRolesPreservingLastAdministratorAsync(int userId, IReadOnlyCollection<int> roleIds, int administratorRoleId, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task ReplacePermissionOverridesAsync(int userId, IReadOnlyCollection<PermissionOverrideDTO> overrides, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IReadOnlyList<AdministrativeRoleDTO>> GetRolesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AdministrativeRoleDTO>>(roles.ToArray());

        public Task<IReadOnlyList<AdministrativePermissionDTO>> GetPermissionsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AdministrativePermissionDTO>>(permissions.ToArray());
    }

    private sealed class TrackingRoleAdministrationRepository : IRoleAdministrationRepository
    {
        public bool WasCalled { get; private set; }

        public Task<IReadOnlyList<AdministrativePermissionDTO>> GetPermissionsAsync(int roleId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AdministrativePermissionDTO>>([]);

        public Task ReplacePermissionsAsync(int roleId, IReadOnlyCollection<int> permissionIds, CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            return Task.CompletedTask;
        }
    }

    private sealed class FakePasswordHasher : IPasswordHasher
    {
        public bool VerificationResult { get; set; }

        public string Hash(string password) => password;
        public bool Verify(string password, string passwordHash) => VerificationResult;
    }

    private sealed class FakePasswordPolicy : IPasswordPolicy
    {
        public bool IsValid(string? password) => true;
    }
}
