using Decor.Application.Services;
using Decor.Core.Common;
using Decor.Core.DTOs;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Repositories;
using Decor.Core.Interfaces.Services;

namespace Decor.Application.Tests;

public sealed class AdministrativeSessionLocalInvalidationTests
{
    [Fact]
    public async Task UserAdministrationService_WhenCurrentUserIsTargeted_SignsOutLocalContext()
    {
        var currentUser = CreateUser(
            userId: 10,
            username: "joao",
            roles: [SystemRoleDefaults.Administrator]);
        var context = new AuthenticatedUserContext();
        context.SignIn(currentUser);

        var service = new UserAdministrationService(
            new FakeUserAdministrationRepository(
                users: [
                    currentUser,
                    CreateUser(userId: 20, username: "maria", roles: ["Operador"])],
                roles: [
                    new AdministrativeRoleDTO(1, SystemRoleDefaults.Administrator, null, 100, true),
                    new AdministrativeRoleDTO(2, "Operador", null, 10, false)
                ],
                permissions: [
                    new AdministrativePermissionDTO(1, DecorPermissions.UsersAssignRoles, null)
                ]),
            new FakePasswordHasher { VerificationResult = true },
            new FakePasswordPolicy(),
            context,
            new AlwaysAllowedAuthorizationService());

        await service.ReplaceRolesAsync(currentUser.UserID, [2]);

        context.IsAuthenticated.Should().BeFalse();
    }

    [Fact]
    public async Task UserAdministrationService_WhenAnotherUserIsTargeted_DoesNotSignOutLocalContext()
    {
        var currentUser = CreateUser(
            userId: 10,
            username: "joao",
            roles: [SystemRoleDefaults.Administrator]);
        var targetUser = CreateUser(userId: 20, username: "maria", roles: ["Operador"]);
        var context = new AuthenticatedUserContext();
        context.SignIn(currentUser);

        var service = new UserAdministrationService(
            new FakeUserAdministrationRepository(
                users: [currentUser, targetUser],
                roles: [
                    new AdministrativeRoleDTO(1, SystemRoleDefaults.Administrator, null, 100, true),
                    new AdministrativeRoleDTO(2, "Operador", null, 10, false)
                ],
                permissions: [
                    new AdministrativePermissionDTO(1, DecorPermissions.UsersAssignRoles, null)
                ]),
            new FakePasswordHasher { VerificationResult = true },
            new FakePasswordPolicy(),
            context,
            new AlwaysAllowedAuthorizationService());

        await service.ReplaceRolesAsync(targetUser.UserID, [1]);

        context.IsAuthenticated.Should().BeTrue();
        context.User.Should().BeSameAs(currentUser);
    }

    [Fact]
    public async Task RoleAdministrationService_WhenCurrentUserHasChangedRole_SignsOutLocalContext()
    {
        var currentUser = CreateUser(
            userId: 10,
            username: "joao",
            roles: ["Operador", SystemRoleDefaults.Administrator]);
        var context = new AuthenticatedUserContext();
        context.SignIn(currentUser);

        var service = new RoleAdministrationService(
            new FakeUserAdministrationRepository(
                users: [currentUser],
                roles: [
                    new AdministrativeRoleDTO(1, "Operador", null, 1, false),
                    new AdministrativeRoleDTO(2, SystemRoleDefaults.Administrator, null, 100, true)
                ],
                permissions: [
                    new AdministrativePermissionDTO(1, DecorPermissions.RolesManagePermissions, null)
                ]),
            new FakeRoleAdministrationRepository(),
            context,
            new AlwaysAllowedAuthorizationService());

        await service.ReplacePermissionsAsync(1, [1]);

        context.IsAuthenticated.Should().BeFalse();
    }

    [Fact]
    public async Task RoleAdministrationService_WhenCurrentUserDoesNotHaveTargetRole_DoesNotSignOutLocalContext()
    {
        var currentUser = CreateUser(
            userId: 10,
            username: "joao",
            roles: ["Financeiro"]);
        var context = new AuthenticatedUserContext();
        context.SignIn(currentUser);

        var service = new RoleAdministrationService(
            new FakeUserAdministrationRepository(
                users: [currentUser],
                roles: [
                    new AdministrativeRoleDTO(1, "Financeiro", null, 30, false),
                    new AdministrativeRoleDTO(2, "Operador", null, 10, false)
                ],
                permissions: [
                    new AdministrativePermissionDTO(1, DecorPermissions.RolesManagePermissions, null)
                ]),
            new FakeRoleAdministrationRepository(),
            context,
            new AlwaysAllowedAuthorizationService());

        await service.ReplacePermissionsAsync(2, [1]);

        context.IsAuthenticated.Should().BeTrue();
        context.User.Should().BeSameAs(currentUser);
    }

    [Fact]
    public async Task RoleAdministrationService_WhenCurrentUserHasMultipleRoles_AndOneOwnedRoleChanges_SignsOutLocalContext()
    {
        var currentUser = CreateUser(
            userId: 10,
            username: "joao",
            roles: ["Financeiro", "Operador"]);
        var context = new AuthenticatedUserContext();
        context.SignIn(currentUser);

        var service = new RoleAdministrationService(
            new FakeUserAdministrationRepository(
                users: [currentUser],
                roles: [
                    new AdministrativeRoleDTO(1, "Financeiro", null, 30, false),
                    new AdministrativeRoleDTO(2, "Operador", null, 40, false)
                ],
                permissions: [
                    new AdministrativePermissionDTO(1, DecorPermissions.RolesManagePermissions, null)
                ]),
            new FakeRoleAdministrationRepository(),
            context,
            new AlwaysAllowedAuthorizationService());

        await service.ReplacePermissionsAsync(1, [1]);

        context.IsAuthenticated.Should().BeFalse();
    }

    [Fact]
    public async Task RoleAdministrationService_WhenCurrentUserHasMultipleRoles_AndAnotherRoleNotOwnedIsChanged_DoesNotSignOutLocalContext()
    {
        var currentUser = CreateUser(
            userId: 10,
            username: "joao",
            roles: ["Financeiro", "Operador"]);
        var context = new AuthenticatedUserContext();
        context.SignIn(currentUser);

        var service = new RoleAdministrationService(
            new FakeUserAdministrationRepository(
                users: [currentUser],
                roles: [
                    new AdministrativeRoleDTO(1, "Financeiro", null, 30, false),
                    new AdministrativeRoleDTO(2, "Catalogo", null, 10, false),
                    new AdministrativeRoleDTO(3, "Operador", null, 40, false)
                ],
                permissions: [
                    new AdministrativePermissionDTO(1, DecorPermissions.RolesManagePermissions, null)
                ]),
            new FakeRoleAdministrationRepository(),
            context,
            new AlwaysAllowedAuthorizationService());

        await service.ReplacePermissionsAsync(2, [1]);

        context.IsAuthenticated.Should().BeTrue();
        context.User.Should().BeSameAs(currentUser);
    }

    [Fact]
    public async Task UserAdministrationService_WhenCurrentUserPermissionOverrideIsChanged_SignsOutLocalContext()
    {
        var currentUser = CreateUser(
            userId: 10,
            username: "joao",
            roles: [SystemRoleDefaults.Administrator]);
        var context = new AuthenticatedUserContext();
        context.SignIn(currentUser);

        var service = new UserAdministrationService(
            new FakeUserAdministrationRepository(
                users: [currentUser],
                roles: [
                    new AdministrativeRoleDTO(1, SystemRoleDefaults.Administrator, null, 100, true)
                ],
                permissions: [
                    new AdministrativePermissionDTO(1, DecorPermissions.UsersManagePermissions, null)
                ]),
            new FakePasswordHasher { VerificationResult = true },
            new FakePasswordPolicy(),
            context,
            new AlwaysAllowedAuthorizationService());

        await service.ReplacePermissionOverridesAsync(currentUser.UserID, [new PermissionOverrideDTO(1, DecorPermissions.UsersManagePermissions, false)]);

        context.IsAuthenticated.Should().BeFalse();
    }

    [Fact]
    public async Task UserAdministrationService_WhenAnotherUserPermissionOverrideIsChanged_DoesNotSignOutLocalContext()
    {
        var currentUser = CreateUser(
            userId: 10,
            username: "joao",
            roles: [SystemRoleDefaults.Administrator]);
        var targetUser = CreateUser(userId: 20, username: "maria", roles: ["Operador"]);
        var context = new AuthenticatedUserContext();
        context.SignIn(currentUser);

        var service = new UserAdministrationService(
            new FakeUserAdministrationRepository(
                users: [currentUser, targetUser],
                roles: [
                    new AdministrativeRoleDTO(1, SystemRoleDefaults.Administrator, null, 100, true),
                    new AdministrativeRoleDTO(2, "Operador", null, 10, false)
                ],
                permissions: [
                    new AdministrativePermissionDTO(1, DecorPermissions.UsersManagePermissions, null)
                ]),
            new FakePasswordHasher { VerificationResult = true },
            new FakePasswordPolicy(),
            context,
            new AlwaysAllowedAuthorizationService());

        await service.ReplacePermissionOverridesAsync(targetUser.UserID, [new PermissionOverrideDTO(1, DecorPermissions.UsersManagePermissions, false)]);

        context.IsAuthenticated.Should().BeTrue();
        context.User.Should().BeSameAs(currentUser);
    }

    private static ApplicationUser CreateUser(int userId, string username, params string[] roles) => new()
    {
        UserID = userId,
        Username = username,
        DisplayName = username,
        IsActive = true,
        MustChangePassword = false,
        Roles = roles,
        Permissions = [DecorPermissions.UsersAssignRoles, DecorPermissions.RolesManagePermissions]
    };

    private sealed class AlwaysAllowedAuthorizationService : IAuthorizationService
    {
        public bool HasPermission(string permissionCode) => true;
        public bool CanView(string resource) => true;
        public bool CanCreate(string resource) => true;
        public bool CanEdit(string resource) => true;
        public bool CanDelete(string resource) => true;
    }

    private sealed class FakePasswordPolicy : IPasswordPolicy
    {
        public bool IsValid(string? password) => true;
    }

    private sealed class FakeUserAdministrationRepository(
        IReadOnlyCollection<ApplicationUser> users,
        IReadOnlyCollection<AdministrativeRoleDTO> roles,
        IReadOnlyCollection<AdministrativePermissionDTO> permissions)
        : IUserAdministrationRepository
    {
        public Task<IReadOnlyList<AdministrativeUserDTO>> SearchAsync(string? search, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AdministrativeUserDTO>>(Array.Empty<AdministrativeUserDTO>());

        public Task<AdministrativeUserDTO?> GetByIdAsync(int userId, CancellationToken cancellationToken = default)
        {
            var user = users.SingleOrDefault(u => u.UserID == userId);
            if (user is null)
                return Task.FromResult<AdministrativeUserDTO?>(null);

            return Task.FromResult<AdministrativeUserDTO?>(new AdministrativeUserDTO(
                user.UserID,
                user.Username,
                user.DisplayName,
                user.IsActive,
                user.Roles.Select((name, index) => new AdministrativeRoleDTO(index + 1, name, null, 10, false)).ToArray(),
                []));
        }

        public Task<int> CreateWithRolesAsync(string username, string displayName, string passwordHash, IReadOnlyCollection<int> roleIds, CancellationToken cancellationToken = default) =>
            Task.FromResult(0);

        public Task<bool> UpdateAsync(int userId, string username, string displayName, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task<bool> SetActivePreservingLastAdministratorAsync(int userId, bool isActive, int administratorRoleId, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task<bool> UpdateTemporaryPasswordAsync(int userId, string passwordHash, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task ReplaceRolesPreservingLastAdministratorAsync(int userId, IReadOnlyCollection<int> roleIds, int administratorRoleId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task ReplacePermissionOverridesAsync(int userId, IReadOnlyCollection<PermissionOverrideDTO> overrides, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<AdministrativeRoleDTO>> GetRolesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult((IReadOnlyList<AdministrativeRoleDTO>)roles.ToArray());

        public Task<IReadOnlyList<AdministrativePermissionDTO>> GetPermissionsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult((IReadOnlyList<AdministrativePermissionDTO>)permissions.ToArray());
    }

    private sealed class FakeRoleAdministrationRepository : IRoleAdministrationRepository
    {
        public Task<IReadOnlyList<AdministrativePermissionDTO>> GetPermissionsAsync(int roleId, CancellationToken cancellationToken = default) =>
            Task.FromResult((IReadOnlyList<AdministrativePermissionDTO>)new[]
            {
                new AdministrativePermissionDTO(1, DecorPermissions.RolesManagePermissions, null)
            });

        public Task ReplacePermissionsAsync(int roleId, IReadOnlyCollection<int> permissionIds, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
