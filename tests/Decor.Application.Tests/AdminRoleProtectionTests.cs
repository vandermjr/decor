using Decor.Application.Services;
using Decor.Core.Common;
using Decor.Core.DTOs;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Repositories;
using Decor.Core.Interfaces.Services;

namespace Decor.Application.Tests;

public sealed class AdminRoleProtectionTests
{
    [Fact]
    public async Task RoleAdministrationService_WhenAdministratorRoleLosesRequiredPermission_Throws()
    {
        var adminRoleId = 1;
        var permissionId = 11;
        var admin = CreateUser(SystemRoleDefaults.Administrator);
        var context = new AuthenticatedUserContext();
        context.SignIn(admin);

        var service = new RoleAdministrationService(
            new FakeUserAdministrationRepository(
                [
                    new AdministrativeRoleDTO(adminRoleId, SystemRoleDefaults.Administrator, null, 200, true)
                ],
                [
                    new AdministrativePermissionDTO(permissionId, DecorPermissions.RolesManagePermissions, null)
                ]),
            new TrackingRoleAdministrationRepository(),
            context,
            new AlwaysAllowedAuthorizationService());

        var act = async () => await service.ReplacePermissionsAsync(adminRoleId, [permissionId]);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*preservar*");
    }

    [Fact]
    public async Task RoleAdministrationService_WhenAdministratorRolePermissionsAreEmpty_Throws()
    {
        var adminRoleId = 1;
        var admin = CreateUser(SystemRoleDefaults.Administrator);
        var context = new AuthenticatedUserContext();
        context.SignIn(admin);

        var service = new RoleAdministrationService(
            new FakeUserAdministrationRepository(
                [new AdministrativeRoleDTO(adminRoleId, SystemRoleDefaults.Administrator, null, 200, true)],
                [
                    new AdministrativePermissionDTO(1, DecorPermissions.UsersView, null),
                    new AdministrativePermissionDTO(2, DecorPermissions.UsersEdit, null),
                    new AdministrativePermissionDTO(3, DecorPermissions.RolesRestoreDefaults, null)
                ]),
            new TrackingRoleAdministrationRepository(),
            context,
            new AlwaysAllowedAuthorizationService());

        var act = async () => await service.ReplacePermissionsAsync(adminRoleId, []);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*preservar*");
    }

    [Fact]
    public async Task RoleAdministrationService_RestoreDefaultsAsync_RestoresAdministratorPermissionsDeterministically()
    {
        var adminRoleId = 1;
        var admin = CreateUser(SystemRoleDefaults.Administrator);
        var context = new AuthenticatedUserContext();
        context.SignIn(admin);

        var allPermissions = new[]
        {
            new AdministrativePermissionDTO(1, DecorPermissions.ProductsView, null),
            new AdministrativePermissionDTO(2, DecorPermissions.ProductsCreate, null),
            new AdministrativePermissionDTO(3, DecorPermissions.ProductsEdit, null),
            new AdministrativePermissionDTO(4, DecorPermissions.BrandsView, null),
            new AdministrativePermissionDTO(5, DecorPermissions.BrandsCreate, null),
            new AdministrativePermissionDTO(6, DecorPermissions.BrandsEdit, null),
            new AdministrativePermissionDTO(7, DecorPermissions.BrandsDelete, null),
            new AdministrativePermissionDTO(8, DecorPermissions.ClassificationsView, null),
            new AdministrativePermissionDTO(9, DecorPermissions.TermDeliveryView, null),
            new AdministrativePermissionDTO(10, DecorPermissions.UsersView, null),
            new AdministrativePermissionDTO(11, DecorPermissions.UsersCreate, null),
            new AdministrativePermissionDTO(12, DecorPermissions.UsersEdit, null),
            new AdministrativePermissionDTO(13, DecorPermissions.UsersActivate, null),
            new AdministrativePermissionDTO(14, DecorPermissions.UsersDeactivate, null),
            new AdministrativePermissionDTO(15, DecorPermissions.UsersAssignRoles, null),
            new AdministrativePermissionDTO(16, DecorPermissions.UsersManagePermissions, null),
            new AdministrativePermissionDTO(17, DecorPermissions.UsersResetPassword, null),
            new AdministrativePermissionDTO(18, DecorPermissions.UsersRestorePermissions, null),
            new AdministrativePermissionDTO(19, DecorPermissions.RolesView, null),
            new AdministrativePermissionDTO(20, DecorPermissions.RolesEdit, null),
            new AdministrativePermissionDTO(21, DecorPermissions.RolesManagePermissions, null),
            new AdministrativePermissionDTO(22, DecorPermissions.RolesRestoreDefaults, null),
            new AdministrativePermissionDTO(23, DecorPermissions.UsersManagePermissions, null)
        };

        var trackingRepo = new TrackingRoleAdministrationRepository();
        var service = new RoleAdministrationService(
            new FakeUserAdministrationRepository(
                [new AdministrativeRoleDTO(adminRoleId, SystemRoleDefaults.Administrator, null, 200, true)],
                allPermissions),
            trackingRepo,
            context,
            new AlwaysAllowedAuthorizationService());

        await service.RestoreDefaultsAsync(adminRoleId);

        var expected = allPermissions
            .Where(p => SystemRoleDefaults.Permissions[SystemRoleDefaults.Administrator].Contains(p.PermissionCode, StringComparer.OrdinalIgnoreCase))
            .Select(p => p.PermissionID)
            .ToArray();

        trackingRepo.LastPermissionIds.Should().Equal(expected);
    }

    private static ApplicationUser CreateUser(string roleName)
    {
        return new ApplicationUser
        {
            UserID = 42,
            Username = "admin-test",
            DisplayName = "Administrador de teste",
            IsActive = true,
            MustChangePassword = false,
            Roles = [roleName],
            Permissions = [DecorPermissions.RolesManagePermissions]
        };
    }

    private sealed class FakeUserAdministrationRepository(
        IReadOnlyCollection<AdministrativeRoleDTO> roles,
        IReadOnlyCollection<AdministrativePermissionDTO> permissions)
        : IUserAdministrationRepository
    {
        public Task<IReadOnlyList<AdministrativeUserDTO>> SearchAsync(string? search, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AdministrativeUserDTO>>([]);
        public Task<AdministrativeUserDTO?> GetByIdAsync(int userId, CancellationToken cancellationToken = default) => Task.FromResult<AdministrativeUserDTO?>(null);
        public Task<int> CreateWithRolesAsync(string username, string displayName, string passwordHash, IReadOnlyCollection<int> roleIds, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<bool> UpdateAsync(int userId, string username, string displayName, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<bool> SetActivePreservingLastAdministratorAsync(int userId, bool isActive, int administratorRoleId, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<bool> UpdateTemporaryPasswordAsync(int userId, string passwordHash, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task ReplaceRolesPreservingLastAdministratorAsync(int userId, IReadOnlyCollection<int> roleIds, int administratorRoleId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ReplacePermissionOverridesAsync(int userId, IReadOnlyCollection<PermissionOverrideDTO> overrides, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<AdministrativeRoleDTO>> GetRolesAsync(CancellationToken cancellationToken = default) => Task.FromResult((IReadOnlyList<AdministrativeRoleDTO>)roles.ToArray());
        public Task<IReadOnlyList<AdministrativePermissionDTO>> GetPermissionsAsync(CancellationToken cancellationToken = default) => Task.FromResult((IReadOnlyList<AdministrativePermissionDTO>)permissions.ToArray());
    }

    private sealed class TrackingRoleAdministrationRepository : IRoleAdministrationRepository
    {
        public IReadOnlyCollection<int> LastPermissionIds { get; private set; } = Array.Empty<int>();

        public Task<IReadOnlyList<AdministrativePermissionDTO>> GetPermissionsAsync(int roleId, CancellationToken cancellationToken = default) =>
            Task.FromResult((IReadOnlyList<AdministrativePermissionDTO>)Array.Empty<AdministrativePermissionDTO>());

        public Task ReplacePermissionsAsync(int roleId, IReadOnlyCollection<int> permissionIds, CancellationToken cancellationToken = default)
        {
            LastPermissionIds = permissionIds.ToArray();
            return Task.CompletedTask;
        }
    }

    private sealed class AlwaysAllowedAuthorizationService : IAuthorizationService
    {
        public bool HasPermission(string permissionCode) => true;
        public bool CanView(string resource) => true;
        public bool CanCreate(string resource) => true;
        public bool CanEdit(string resource) => true;
        public bool CanDelete(string resource) => true;
    }
}
