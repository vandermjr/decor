using Decor.Application.Services;
using Decor.AvaloniaUI.ViewModels;
using Decor.Core.Common;
using Decor.Core.DTOs;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Repositories;
using Decor.Core.Interfaces.Services;

namespace Decor.Application.Tests;

public sealed class AdminRoleProtectionTests
{
    [Fact]
    public async Task RoleAdministrationService_GetAllPermissionsAsync_WhenAuthorized_ReturnsCatalogInRepositoryOrder()
    {
        var permissions = new[]
        {
            new AdministrativePermissionDTO(2, DecorPermissions.UsersView, "Consultar usuários"),
            new AdministrativePermissionDTO(1, DecorPermissions.ProductsView, "Consultar produtos")
        };
        var context = new AuthenticatedUserContext();
        context.SignIn(CreateUser(SystemRoleDefaults.Administrator));
        var service = new RoleAdministrationService(
            new FakeUserAdministrationRepository([], permissions),
            new TrackingRoleAdministrationRepository(),
            context,
            new SelectiveAuthorizationService(DecorPermissions.RolesView));

        var result = await service.GetAllPermissionsAsync();

        result.Should().BeAssignableTo<IReadOnlyList<AdministrativePermissionDTO>>();
        result.Should().Equal(permissions);
    }

    [Fact]
    public async Task RoleAdministrationService_GetAllPermissionsAsync_WhenMissingRolesView_Throws()
    {
        var context = new AuthenticatedUserContext();
        context.SignIn(CreateUser(SystemRoleDefaults.Administrator));
        var service = new RoleAdministrationService(
            new FakeUserAdministrationRepository([], []),
            new TrackingRoleAdministrationRepository(),
            context,
            new SelectiveAuthorizationService());

        var act = async () => await service.GetAllPermissionsAsync();

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

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

    [Fact]
    public async Task RolesViewModel_SaveAsync_PreservesPermissionsOutsideVisibleContext()
    {
        var role = new AdministrativeRoleDTO(1, "Operador", null, 10, false);
        var allPermissions = new[]
        {
            new AdministrativePermissionDTO(1, DecorPermissions.ProductsView, "Consultar produtos"),
            new AdministrativePermissionDTO(2, DecorPermissions.ProductsCreate, "Cadastrar produtos"),
            new AdministrativePermissionDTO(3, DecorPermissions.UsersView, "Consultar usuários"),
            new AdministrativePermissionDTO(4, DecorPermissions.AccountsPayableView, "Consultar contas a pagar")
        };

        var roleService = new TrackingRolePermissionsService(
            roles: [role],
            allPermissions: allPermissions,
            grantedPermissionIds: [1, 3, 4]);

        var viewModel = new RolesViewModel(roleService);
        await viewModel.InitializeAsync();

        viewModel.SelectedRole = role;
        if (viewModel.Modules.Count == 0)
            throw new InvalidOperationException("Expected modules to be populated.");

        viewModel.SelectedModule = "Cadastros";
        viewModel.SelectedScreen = "Produtos";
        viewModel.Permissions.Select(permission => permission.Permission.PermissionID).Should().BeEquivalentTo([1, 2]);
        viewModel.Permissions.Single(permission => permission.Permission.PermissionID == 1).IsGranted = false;

        var saveMethod = typeof(RolesViewModel).GetMethod("SaveAsync", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        saveMethod.Should().NotBeNull();
        await (Task)saveMethod!.Invoke(viewModel, null)!;

        roleService.LastReplacePermissionIds.Should().BeEquivalentTo(new[] { 3, 4 });
    }

    [Fact]
    public async Task RolesViewModel_LoadPermissionsAsync_DiscardsObsoleteRoleLoad()
    {
        var roleA = new AdministrativeRoleDTO(1, "Role A", null, 10, false);
        var roleB = new AdministrativeRoleDTO(2, "Role B", null, 10, false);
        var productsView = new AdministrativePermissionDTO(1, DecorPermissions.ProductsView, "Consultar produtos");
        var productsCreate = new AdministrativePermissionDTO(2, DecorPermissions.ProductsCreate, "Cadastrar produtos");
        var allPermissions = new[] { productsView, productsCreate };
        var roleAResult = new TaskCompletionSource<IReadOnlyList<AdministrativePermissionDTO>>();
        var roleBResult = new TaskCompletionSource<IReadOnlyList<AdministrativePermissionDTO>>();
        var roleAStarted = new TaskCompletionSource();
        var roleBStarted = new TaskCompletionSource();
        var roleService = new TrackingRolePermissionsService(
            roles: [roleA, roleB],
            allPermissions: allPermissions,
            grantedPermissionIds: [])
        {
            GrantedPermissionsOverride = roleId =>
            {
                if (roleId == roleA.RoleID)
                {
                    roleAStarted.TrySetResult();
                    return roleAResult.Task;
                }
                if (roleId == roleB.RoleID)
                {
                    roleBStarted.TrySetResult();
                    return roleBResult.Task;
                }
                throw new InvalidOperationException($"Unexpected role ID {roleId}.");
            }
        };

        var viewModel = new RolesViewModel(roleService);
        await viewModel.InitializeAsync();
        var roleBPublished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        viewModel.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(viewModel.IsLoading) && !viewModel.IsLoading)
                roleBPublished.TrySetResult();
        };

        var previousContext = SynchronizationContext.Current;
        var queuedContext = new QueuedSynchronizationContext();
        SynchronizationContext.SetSynchronizationContext(queuedContext);
        try
        {
            viewModel.SelectedRole = roleA;
            await roleAStarted.Task;
            viewModel.SelectedRole = roleB;
            await roleBStarted.Task;
            roleBResult.SetResult([productsCreate]);
            queuedContext.RunUntil(() => roleBPublished.Task.IsCompleted);

            viewModel.Permissions.Single(permission => permission.Permission.PermissionID == productsCreate.PermissionID)
                .IsGranted.Should().BeTrue();
            viewModel.Permissions.Single(permission => permission.Permission.PermissionID == productsView.PermissionID)
                .IsGranted.Should().BeFalse();

            roleAResult.SetResult([productsView]);
            queuedContext.RunUntilIdle();

            var saveMethod = typeof(RolesViewModel).GetMethod("SaveAsync", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            saveMethod.Should().NotBeNull();
            await (Task)saveMethod!.Invoke(viewModel, null)!;
            roleService.LastReplacePermissionIds.Should().BeEquivalentTo(new[] { productsCreate.PermissionID });
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previousContext);
        }
    }

    [Fact]
    public async Task RolesViewModel_SaveAsync_WhenRoleChanges_DoesNotReloadOrOverwriteSelectedRole()
    {
        var roleA = new AdministrativeRoleDTO(1, "Role A", null, 10, false);
        var roleB = new AdministrativeRoleDTO(2, "Role B", null, 10, false);
        var productsView = new AdministrativePermissionDTO(1, DecorPermissions.ProductsView, "Consultar produtos");
        var productsCreate = new AdministrativePermissionDTO(2, DecorPermissions.ProductsCreate, "Cadastrar produtos");
        var roleService = new TrackingRolePermissionsService(
            roles: [roleA, roleB],
            allPermissions: [productsView, productsCreate],
            grantedPermissionIds: [])
        {
            GrantedPermissionsOverride = roleId => Task.FromResult<IReadOnlyList<AdministrativePermissionDTO>>(
                roleId == roleA.RoleID ? [productsView] : [productsCreate])
        };

        var viewModel = new RolesViewModel(roleService);
        await viewModel.InitializeAsync();
        viewModel.SelectedRole = roleA;
        viewModel.IsLoading.Should().BeFalse();

        var replaceStarted = new TaskCompletionSource();
        var replaceGate = new TaskCompletionSource();
        roleService.ReplacePermissionsOverride = async (_, _) =>
        {
            replaceStarted.TrySetResult();
            await replaceGate.Task;
        };

        var previousContext = SynchronizationContext.Current;
        var queuedContext = new QueuedSynchronizationContext();
        SynchronizationContext.SetSynchronizationContext(queuedContext);
        try
        {
            var saveTask = InvokeSaveAsync(viewModel);
            await replaceStarted.Task;
            roleService.PermissionRequestRoleIds.Should().Equal(roleA.RoleID);

            viewModel.SelectedRole = roleB;
            viewModel.IsLoading.Should().BeFalse();
            roleService.PermissionRequestRoleIds.Should().Equal(roleA.RoleID, roleB.RoleID);
            var roleBVisibleItems = viewModel.Permissions.ToArray();
            roleBVisibleItems.Single(item => item.Permission.PermissionID == productsCreate.PermissionID)
                .IsGranted.Should().BeTrue();

            replaceGate.SetResult();
            queuedContext.RunUntil(() => saveTask.IsCompleted);
            await saveTask;

            roleService.PermissionRequestRoleIds.Should().Equal(roleA.RoleID, roleB.RoleID);
            viewModel.Permissions.Should().Equal(roleBVisibleItems);
            viewModel.Permissions.Single(item => item.Permission.PermissionID == productsCreate.PermissionID)
                .IsGranted.Should().BeTrue();

            var roleBSaveTask = InvokeSaveAsync(viewModel);
            queuedContext.RunUntil(() => roleBSaveTask.IsCompleted);
            await roleBSaveTask;
            roleService.ReplaceCalls[0].RoleId.Should().Be(roleA.RoleID);
            roleService.ReplaceCalls[1].RoleId.Should().Be(roleB.RoleID);
            roleService.ReplaceCalls[1].PermissionIds.Should().BeEquivalentTo(new[] { productsCreate.PermissionID });
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previousContext);
        }
    }

    [Fact]
    public async Task RolesViewModel_SaveAsync_WhenRoleChangesAndReplaceFails_DoesNotPublishOldError()
    {
        var roleA = new AdministrativeRoleDTO(1, "Role A", null, 10, false);
        var roleB = new AdministrativeRoleDTO(2, "Role B", null, 10, false);
        var allPermissions = new[]
        {
            new AdministrativePermissionDTO(1, DecorPermissions.ProductsView, "Consultar produtos"),
            new AdministrativePermissionDTO(2, DecorPermissions.ProductsCreate, "Cadastrar produtos")
        };
        var roleService = new TrackingRolePermissionsService(
            roles: [roleA, roleB],
            allPermissions: allPermissions,
            grantedPermissionIds: [])
        {
            GrantedPermissionsOverride = roleId => Task.FromResult<IReadOnlyList<AdministrativePermissionDTO>>(
                roleId == roleA.RoleID ? [allPermissions[0]] : [allPermissions[1]])
        };

        var viewModel = new RolesViewModel(roleService);
        await viewModel.InitializeAsync();
        viewModel.SelectedRole = roleA;
        var replaceStarted = new TaskCompletionSource();
        var replaceGate = new TaskCompletionSource();
        roleService.ReplacePermissionsOverride = async (_, _) =>
        {
            replaceStarted.TrySetResult();
            await replaceGate.Task;
        };

        var previousContext = SynchronizationContext.Current;
        var queuedContext = new QueuedSynchronizationContext();
        SynchronizationContext.SetSynchronizationContext(queuedContext);
        try
        {
            var saveTask = InvokeSaveAsync(viewModel);
            await replaceStarted.Task;
            viewModel.SelectedRole = roleB;
            replaceGate.SetException(new InvalidOperationException("Configured replace failure."));
            queuedContext.RunUntil(() => saveTask.IsCompleted);
            await saveTask;

            viewModel.ErrorMessage.Should().BeNull();
            viewModel.Permissions.Single(item => item.Permission.PermissionID == allPermissions[1].PermissionID)
                .IsGranted.Should().BeTrue();
            roleService.PermissionRequestRoleIds.Should().Equal(roleA.RoleID, roleB.RoleID);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previousContext);
        }
    }

    [Fact]
    public async Task RolesViewModel_SaveAsync_WhenReplaceFails_PreservesLocalStateWithoutReload()
    {
        var role = new AdministrativeRoleDTO(1, "Operador", null, 10, false);
        var allPermissions = new[]
        {
            new AdministrativePermissionDTO(1, DecorPermissions.ProductsView, "Consultar produtos"),
            new AdministrativePermissionDTO(2, DecorPermissions.ProductsCreate, "Cadastrar produtos")
        };
        var roleService = new TrackingRolePermissionsService(
            roles: [role],
            allPermissions: allPermissions,
            grantedPermissionIds: [1]);

        var viewModel = new RolesViewModel(roleService);
        await viewModel.InitializeAsync();
        viewModel.SelectedRole = role;
        viewModel.IsLoading.Should().BeFalse();
        var viewPermission = viewModel.Permissions.Single(permission => permission.Permission.PermissionID == 1);
        viewPermission.IsGranted = false;
        var permissionReads = roleService.GetPermissionsCallCount;
        var catalogReads = roleService.GetAllPermissionsCallCount;
        roleService.ThrowOnReplace = true;

        var saveMethod = typeof(RolesViewModel).GetMethod("SaveAsync", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        saveMethod.Should().NotBeNull();
        await (Task)saveMethod!.Invoke(viewModel, null)!;

        viewModel.ErrorMessage.Should().Be("Não foi possível salvar as permissões do grupo.");
        viewPermission.IsGranted.Should().BeFalse();
        roleService.GetPermissionsCallCount.Should().Be(permissionReads);
        roleService.GetAllPermissionsCallCount.Should().Be(catalogReads);
    }

    private static Task InvokeSaveAsync(RolesViewModel viewModel)
    {
        var saveMethod = typeof(RolesViewModel).GetMethod("SaveAsync", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        saveMethod.Should().NotBeNull();
        return (Task)saveMethod!.Invoke(viewModel, null)!;
    }

    private sealed class QueuedSynchronizationContext : SynchronizationContext
    {
        private readonly Queue<(SendOrPostCallback Callback, object? State)> _callbacks = new();

        public override void Post(SendOrPostCallback callback, object? state)
            => _callbacks.Enqueue((callback, state));

        public void RunUntil(Func<bool> condition)
        {
            while (!condition())
            {
                if (_callbacks.Count == 0)
                    throw new InvalidOperationException("Expected an asynchronous continuation to be queued.");
                var callback = _callbacks.Dequeue();
                callback.Callback(callback.State);
            }
        }

        public void RunUntilIdle()
        {
            while (_callbacks.Count > 0)
            {
                var callback = _callbacks.Dequeue();
                callback.Callback(callback.State);
            }
        }
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

    private sealed class TrackingRolePermissionsService : IRoleAdministrationService
    {
        private readonly IReadOnlyCollection<AdministrativeRoleDTO> _roles;
        private readonly IReadOnlyCollection<AdministrativePermissionDTO> _allPermissions;
        private readonly IReadOnlyCollection<int> _grantedPermissionIds;

        public TrackingRolePermissionsService(
            IReadOnlyCollection<AdministrativeRoleDTO> roles,
            IReadOnlyCollection<AdministrativePermissionDTO> allPermissions,
            IReadOnlyCollection<int> grantedPermissionIds)
        {
            _roles = roles;
            _allPermissions = allPermissions;
            _grantedPermissionIds = grantedPermissionIds;
        }

        public IReadOnlyCollection<int> LastReplacePermissionIds { get; private set; } = Array.Empty<int>();
        public Func<int, Task<IReadOnlyList<AdministrativePermissionDTO>>>? GrantedPermissionsOverride { get; init; }
        public Func<int, IReadOnlyCollection<int>, Task>? ReplacePermissionsOverride { get; set; }
        public bool ThrowOnReplace { get; set; }
        public int GetPermissionsCallCount { get; private set; }
        public int GetAllPermissionsCallCount { get; private set; }
        public List<int> PermissionRequestRoleIds { get; } = [];
        public List<(int RoleId, int[] PermissionIds)> ReplaceCalls { get; } = [];

        public Task<IReadOnlyList<AdministrativeRoleDTO>> GetRolesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult((IReadOnlyList<AdministrativeRoleDTO>)_roles.ToArray());

        public Task<IReadOnlyList<AdministrativePermissionDTO>> GetAllPermissionsAsync(CancellationToken cancellationToken = default)
        {
            GetAllPermissionsCallCount++;
            return Task.FromResult((IReadOnlyList<AdministrativePermissionDTO>)_allPermissions.ToArray());
        }

        public Task<IReadOnlyList<AdministrativePermissionDTO>> GetPermissionsAsync(int roleId, CancellationToken cancellationToken = default)
        {
            GetPermissionsCallCount++;
            PermissionRequestRoleIds.Add(roleId);
            return GrantedPermissionsOverride?.Invoke(roleId)
                ?? Task.FromResult((IReadOnlyList<AdministrativePermissionDTO>)_allPermissions.Where(p => _grantedPermissionIds.Contains(p.PermissionID)).ToArray());
        }

        public async Task ReplacePermissionsAsync(int roleId, IReadOnlyCollection<int> permissionIds, CancellationToken cancellationToken = default)
        {
            var ids = permissionIds.ToArray();
            ReplaceCalls.Add((roleId, ids));
            if (ThrowOnReplace)
                throw new InvalidOperationException("Configured replace failure.");
            if (ReplacePermissionsOverride is not null)
                await ReplacePermissionsOverride(roleId, ids);
            LastReplacePermissionIds = ids;
        }

        public Task RestoreDefaultsAsync(int roleId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class AlwaysAllowedAuthorizationService : IAuthorizationService
    {
        public bool HasPermission(string permissionCode) => true;
        public bool CanView(string resource) => true;
        public bool CanCreate(string resource) => true;
        public bool CanEdit(string resource) => true;
        public bool CanDelete(string resource) => true;
    }

    private sealed class SelectiveAuthorizationService(params string[] permissions) : IAuthorizationService
    {
        public bool HasPermission(string permissionCode) => permissions.Contains(permissionCode, StringComparer.OrdinalIgnoreCase);
        public bool CanView(string resource) => false;
        public bool CanCreate(string resource) => false;
        public bool CanEdit(string resource) => false;
        public bool CanDelete(string resource) => false;
    }
}
