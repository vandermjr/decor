using System.Reflection;
using Decor.AvaloniaUI.ViewModels;
using Decor.Core.Common;
using Decor.Core.DTOs;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Services;
using FluentAssertions;

namespace Decor.Application.Tests;

public sealed class UserPermissionsViewModelTests
{
    private const string Inherit = "Herdar dos grupos";
    private const string Allow = "Permitir";
    private const string Deny = "Negar";
    private const string AllowedAccess = "Permitido";
    private const string DeniedAccess = "N\u00e3o permitido";

    [Theory]
    [InlineData(false, Inherit, DeniedAccess)]
    [InlineData(true, Inherit, AllowedAccess)]
    [InlineData(false, Allow, AllowedAccess)]
    [InlineData(true, Allow, AllowedAccess)]
    [InlineData(false, Deny, DeniedAccess)]
    [InlineData(true, Deny, DeniedAccess)]
    public void PermissionOption_AllStates_DistinguishInheritedAndEffectiveAccess(
        bool grantedByGroups, string selection, string expectedAccess)
    {
        var option = new UserPermissionOption
        {
            Permission = new(1, DecorPermissions.ProductsView, null),
            GrantedByGroups = grantedByGroups,
            Selection = selection
        };

        option.InheritedAccess.Should().Be(grantedByGroups ? AllowedAccess : DeniedAccess);
        option.EffectiveAccess.Should().Be(expectedAccess);
        option.Selection.Should().Be(selection);
        UserPermissionOption.Choices.Should().Equal(Inherit, Allow, Deny);
    }

    [Fact]
    public void PermissionOption_DefaultSelection_InheritsFromGroups()
    {
        var option = new UserPermissionOption { Permission = new(1, DecorPermissions.ProductsView, null) };

        option.Selection.Should().Be(Inherit);
        option.EffectiveAccess.Should().Be(DeniedAccess);
    }

    [Theory]
    [InlineData(Inherit, Allow)]
    [InlineData(Allow, Deny)]
    [InlineData(Deny, Inherit)]
    public void PermissionOption_ChangingSelection_NotifiesSelectionAndEffectiveAccess(string initial, string next)
    {
        var option = new UserPermissionOption
        {
            Permission = new(1, DecorPermissions.ProductsView, null),
            Selection = initial
        };
        var notifications = new List<string?>();
        option.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);

        option.Selection = next;

        notifications.Should().Equal(nameof(option.Selection), nameof(option.EffectiveAccess));
    }

    [Theory]
    [InlineData(Allow)]
    [InlineData("invalid")]
    public void PermissionOption_UnchangedOrInvalidSelection_DoesNotNotify(string selection)
    {
        var option = new UserPermissionOption
        {
            Permission = new(1, DecorPermissions.ProductsView, null),
            Selection = Allow
        };
        var notifications = new List<string?>();
        option.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);

        option.Selection = selection;

        option.Selection.Should().Be(Allow);
        notifications.Should().BeEmpty();
    }

    [Fact]
    public async Task InitializeAsync_LoadsUsers_AndSelectionUnionsGrantsFromEveryGroup()
    {
        var fixture = new Fixture();

        await fixture.ViewModel.InitializeAsync();
        fixture.ViewModel.Users.Should().ContainSingle().Which.Should().Be(fixture.Users.User);
        fixture.ViewModel.SaveCommand.CanExecute(null).Should().BeFalse();
        fixture.SelectUser();

        var options = fixture.AllOptions();
        options.Where(option => option.GrantedByGroups).Select(option => option.Permission.PermissionID)
            .Should().BeEquivalentTo(new[] { 1, 2, 3 });
        options.Single(option => option.Permission.PermissionID == 4).GrantedByGroups.Should().BeFalse();
        options.Should().HaveCount(4);
        fixture.Roles.RequestedRoleIds.Should().Equal(10, 20);
        fixture.ViewModel.GroupNames.Should().Be("First group, Second group");
        fixture.ViewModel.IsBusy.Should().BeFalse();
        fixture.ViewModel.SaveCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public void LoadUser_LoadsPositiveNegativeAndInheritedOverrides()
    {
        var fixture = new Fixture();
        fixture.Users.User = fixture.Users.User with
        {
            PermissionOverrides = [new(1, DecorPermissions.ProductsView, false), new(4, DecorPermissions.UsersManagePermissions, true)]
        };

        fixture.SelectUser();

        var options = fixture.AllOptions().ToDictionary(option => option.Permission.PermissionID);
        options[1].Selection.Should().Be(Deny);
        options[1].InheritedAccess.Should().Be(AllowedAccess);
        options[1].EffectiveAccess.Should().Be(DeniedAccess);
        options[4].Selection.Should().Be(Allow);
        options[4].InheritedAccess.Should().Be(DeniedAccess);
        options[4].EffectiveAccess.Should().Be(AllowedAccess);
        options[2].Selection.Should().Be(Inherit);
        options[2].EffectiveAccess.Should().Be(AllowedAccess);
        options[3].Selection.Should().Be(Inherit);
    }

    [Fact]
    public async Task SaveAsync_PreservesSelectionsAcrossModulesAndUnlistedOverrides()
    {
        var fixture = new Fixture();
        var unlistedAllow = new PermissionOverrideDTO(900, "Legacy.Allow", true);
        var unlistedDeny = new PermissionOverrideDTO(901, "Legacy.Deny", false);
        fixture.Users.User = fixture.Users.User with
        {
            PermissionOverrides = [new(3, DecorPermissions.UsersView, true), unlistedAllow, unlistedDeny]
        };
        fixture.SelectUser();
        fixture.ViewModel.SelectedModule = DecorPermissionPresentationCatalog.Describe(DecorPermissions.ProductsView).ModuleName;
        fixture.ViewModel.Permissions.Single(option => option.Permission.PermissionID == 1).Selection = Deny;
        fixture.ViewModel.Permissions.Single(option => option.Permission.PermissionID == 2).Selection = Allow;
        fixture.ViewModel.SelectedModule = DecorPermissionPresentationCatalog.Describe(DecorPermissions.UsersView).ModuleName;
        fixture.ViewModel.Permissions.Single(option => option.Permission.PermissionID == 3).Selection = Inherit;
        fixture.ViewModel.Permissions.Single(option => option.Permission.PermissionID == 4).Selection = Allow;
        fixture.ViewModel.SelectedModule = DecorPermissionPresentationCatalog.Describe(DecorPermissions.ProductsView).ModuleName;
        fixture.ViewModel.Permissions.Single(option => option.Permission.PermissionID == 1).Selection.Should().Be(Deny);

        await InvokeAsync(fixture.ViewModel, "SaveAsync");

        fixture.Users.SavedOverrides.Should().BeEquivalentTo(new PermissionOverrideDTO[]
        {
            new(1, DecorPermissions.ProductsView, false),
            new(2, DecorPermissions.BrandsView, true),
            new(4, DecorPermissions.UsersManagePermissions, true),
            unlistedAllow, unlistedDeny
        });
        fixture.Users.SavedUserIds.Should().Equal(42);
        fixture.ViewModel.StatusMessage.Should().Be("Permiss\u00f5es personalizadas salvas.");
        fixture.ViewModel.IsBusy.Should().BeFalse();
    }

    [Theory]
    [InlineData("user")]
    [InlineData("catalog")]
    [InlineData("grants")]
    public async Task LoadUser_WhenLoadingFails_DisablesSaveAndDoesNotPersist(string failure)
    {
        var fixture = new Fixture();
        fixture.SelectUser();
        fixture.ViewModel.SaveCommand.CanExecute(null).Should().BeTrue();
        fixture.Users.FailGet = failure == "user";
        fixture.Roles.FailCatalog = failure == "catalog";
        fixture.Roles.FailGrants = failure == "grants";

        await fixture.ViewModel.ReloadSelectedAsync();
        await InvokeAsync(fixture.ViewModel, "SaveAsync");

        fixture.ViewModel.SaveCommand.CanExecute(null).Should().BeFalse();
        fixture.ViewModel.StatusMessage.Should().Be("N\u00e3o foi poss\u00edvel carregar as permiss\u00f5es deste usu\u00e1rio.");
        fixture.ViewModel.IsBusy.Should().BeFalse();
        fixture.Users.SavedUserIds.Should().BeEmpty();
    }

    [Theory]
    [InlineData("admin", true)]
    [InlineData("ADMIN", true)]
    [InlineData("test-user", false)]
    public async Task LoadedUser_SpecialAccount_BlocksCustomizationAndChanges(string username, bool isSpecialAccount)
    {
        var fixture = new Fixture();
        fixture.Users.User = fixture.Users.User with { Username = username };
        fixture.ViewModel.SelectedUser = fixture.Users.User with { Username = "search-result" };

        fixture.ViewModel.SelectedUser!.IsSystemAdministrator.Should().Be(isSpecialAccount);
        fixture.AllOptions().Should().OnlyContain(option => option.CanCustomize == !isSpecialAccount);
        fixture.ViewModel.SaveCommand.CanExecute(null).Should().Be(!isSpecialAccount);
        fixture.ViewModel.RestoreCommand.CanExecute(null).Should().Be(!isSpecialAccount);
        fixture.ViewModel.AssignGroupsCommand.CanExecute(null).Should().Be(!isSpecialAccount);

        await InvokeAsync(fixture.ViewModel, "SaveAsync");
        await InvokeAsync(fixture.ViewModel, "RestoreAsync");

        fixture.Users.SavedUserIds.Should().HaveCount(isSpecialAccount ? 0 : 1);
        fixture.Users.RestoredUserIds.Should().HaveCount(isSpecialAccount ? 0 : 1);
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, false)]
    public void Commands_RequireAuthenticationAndAuthorization(bool authenticated, bool authorized, bool expected)
    {
        var fixture = new Fixture();
        fixture.Context.IsAuthenticated = authenticated;
        fixture.Authorization.GrantedPermissions.Clear();
        if (authorized) fixture.Authorization.GrantAll();
        fixture.SelectUser();

        fixture.ViewModel.SaveCommand.CanExecute(null).Should().Be(expected);
        fixture.ViewModel.RestoreCommand.CanExecute(null).Should().Be(expected);
        fixture.ViewModel.AssignGroupsCommand.CanExecute(null).Should().Be(expected);
    }

    [Theory]
    [InlineData(DecorPermissions.UsersManagePermissions)]
    [InlineData(DecorPermissions.UsersRestorePermissions)]
    [InlineData(DecorPermissions.UsersAssignRoles)]
    public void Commands_CheckTheirOwnPermission(string permission)
    {
        var fixture = new Fixture();
        fixture.Authorization.GrantedPermissions.Clear();
        fixture.Authorization.GrantedPermissions.Add(permission);
        fixture.SelectUser();

        fixture.ViewModel.SaveCommand.CanExecute(null).Should().Be(permission == DecorPermissions.UsersManagePermissions);
        fixture.ViewModel.RestoreCommand.CanExecute(null).Should().Be(permission == DecorPermissions.UsersRestorePermissions);
        fixture.ViewModel.AssignGroupsCommand.CanExecute(null).Should().Be(permission == DecorPermissions.UsersAssignRoles);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task SaveAsync_WhenAccessIsRevoked_DoesNothing(bool authenticated, bool authorized)
    {
        var fixture = new Fixture();
        fixture.SelectUser();
        fixture.AllOptions().First().Selection = Deny;
        fixture.Context.IsAuthenticated = authenticated;
        if (!authorized) fixture.Authorization.GrantedPermissions.Clear();
        var originalStatus = fixture.ViewModel.StatusMessage;

        await InvokeAsync(fixture.ViewModel, "SaveAsync");

        fixture.Users.SavedUserIds.Should().BeEmpty();
        fixture.Users.SavedOverrides.Should().BeEmpty();
        fixture.ViewModel.StatusMessage.Should().Be(originalStatus);
        fixture.ViewModel.IsBusy.Should().BeFalse();
    }

    [Fact]
    public async Task RestoreAsync_RemovesExceptionsInServiceAndReloadsInheritedPermissions()
    {
        var fixture = new Fixture();
        fixture.Users.User = fixture.Users.User with
        {
            PermissionOverrides = [new(1, DecorPermissions.ProductsView, false), new(4, DecorPermissions.UsersManagePermissions, true), new(900, "Legacy.Allow", true)]
        };
        fixture.SelectUser();
        var originalOptions = fixture.AllOptions();

        await InvokeAsync(fixture.ViewModel, "RestoreAsync");

        fixture.Users.RestoredUserIds.Should().Equal(42);
        fixture.Users.GetUserIds.Should().Equal(42, 42);
        fixture.ViewModel.SelectedUser!.PermissionOverrides.Should().BeEmpty();
        var reloadedOptions = fixture.AllOptions();
        reloadedOptions.Should().OnlyContain(option => option.Selection == Inherit);
        reloadedOptions[0].Should().NotBeSameAs(originalOptions[0]);
        reloadedOptions.Single(option => option.Permission.PermissionID == 1).EffectiveAccess.Should().Be(AllowedAccess);
        reloadedOptions.Single(option => option.Permission.PermissionID == 4).EffectiveAccess.Should().Be(DeniedAccess);
        fixture.ViewModel.StatusMessage.Should().Be("Permiss\u00f5es herdadas dos grupos restauradas.");
        fixture.ViewModel.IsBusy.Should().BeFalse();
        fixture.ViewModel.RestoreCommand.CanExecute(null).Should().BeTrue();

        await InvokeAsync(fixture.ViewModel, "SaveAsync");

        fixture.Users.SavedOverrides.Should().BeEmpty();
    }

    [Fact]
    public void AssignGroupsCommand_WhenAuthorized_RaisesRequest()
    {
        var fixture = new Fixture();
        fixture.SelectUser();
        var requests = 0;
        fixture.ViewModel.AssignGroupsRequested += (_, _) => requests++;

        fixture.ViewModel.AssignGroupsCommand.Execute(null);

        requests.Should().Be(1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Busy_DuringLoadOrSave_BlocksUserSelectionAndAdditionalSave(bool saving)
    {
        var fixture = new Fixture();
        fixture.SelectUser();
        var selectedUser = fixture.ViewModel.SelectedUser;
        var pending = new TaskCompletionSource<bool>();
        Task operation;
        if (saving)
        {
            fixture.Users.SaveCompletion = pending.Task;
            operation = InvokeAsync(fixture.ViewModel, "SaveAsync");
        }
        else
        {
            fixture.Users.GetCompletion = pending.Task;
            operation = fixture.ViewModel.ReloadSelectedAsync();
        }

        try
        {
            fixture.ViewModel.IsBusy.Should().BeTrue();
            fixture.ViewModel.SaveCommand.CanExecute(null).Should().BeFalse();
            fixture.ViewModel.RestoreCommand.CanExecute(null).Should().BeFalse();
            fixture.ViewModel.AssignGroupsCommand.CanExecute(null).Should().BeFalse();
            fixture.ViewModel.SearchCommand.CanExecute(null).Should().BeFalse();
            fixture.ViewModel.SelectedUser = selectedUser! with { UserID = 99 };
            fixture.ViewModel.SelectedUser.Should().BeSameAs(selectedUser);
            fixture.ViewModel.SelectedUser = null;
            fixture.ViewModel.SelectedUser.Should().BeSameAs(selectedUser);

            await InvokeAsync(fixture.ViewModel, "SaveAsync");

            fixture.Users.SavedUserIds.Should().HaveCount(saving ? 1 : 0);
            fixture.Users.GetUserIds.Should().Equal(saving ? new[] { 42 } : new[] { 42, 42 });
        }
        finally
        {
            pending.SetResult(true);
            await operation;
        }

        fixture.ViewModel.IsBusy.Should().BeFalse();
        fixture.ViewModel.SaveCommand.CanExecute(null).Should().BeTrue();
    }

    private static Task InvokeAsync(UserPermissionsViewModel viewModel, string methodName)
    {
        var method = typeof(UserPermissionsViewModel).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        method.Should().NotBeNull();
        return (Task)method!.Invoke(viewModel, null)!;
    }

    private sealed class Fixture
    {
        public FakeUserAdministrationService Users { get; } = new();
        public FakeRoleAdministrationService Roles { get; } = new();
        public FakeAuthorizationService Authorization { get; } = new();
        public FakeAuthenticatedUserContext Context { get; } = new();
        public UserPermissionsViewModel ViewModel { get; }

        public Fixture() => ViewModel = new(Users, Roles, Authorization, Context);

        public void SelectUser()
        {
            ViewModel.SelectedUser = Users.User;
            ViewModel.IsBusy.Should().BeFalse();
            ViewModel.StatusMessage.Should().BeEmpty();
            ViewModel.Modules.Should().HaveCount(2);
        }

        public UserPermissionOption[] AllOptions()
        {
            var options = new List<UserPermissionOption>();
            foreach (var module in ViewModel.Modules)
            {
                ViewModel.SelectedModule = module;
                options.AddRange(ViewModel.Permissions);
            }
            return options.ToArray();
        }
    }

    private sealed class FakeUserAdministrationService : IUserAdministrationService
    {
        public AdministrativeUserDTO User { get; set; } = new(42, "test-user", "Test user", true,
            [new(10, "First group", null, 1, false), new(20, "Second group", null, 2, false)], []);
        public bool FailGet { get; set; }
        public Task? GetCompletion { get; set; }
        public Task? SaveCompletion { get; set; }
        public List<int> GetUserIds { get; } = [];
        public List<int> SavedUserIds { get; } = [];
        public List<int> RestoredUserIds { get; } = [];
        public IReadOnlyCollection<PermissionOverrideDTO> SavedOverrides { get; private set; } = [];

        public Task<IReadOnlyList<AdministrativeUserDTO>> SearchAsync(string? search, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<AdministrativeUserDTO>>([User]);

        public async Task<AdministrativeUserDTO?> GetByIdAsync(int userId, CancellationToken cancellationToken = default)
        {
            GetUserIds.Add(userId);
            if (GetCompletion is not null) await GetCompletion;
            if (FailGet) throw new InvalidOperationException("Load failed");
            return User;
        }

        public Task ReplacePermissionOverridesAsync(int userId, IReadOnlyCollection<PermissionOverrideDTO> overrides, CancellationToken cancellationToken = default)
        {
            SavedUserIds.Add(userId);
            SavedOverrides = overrides.ToArray();
            return SaveCompletion ?? Task.CompletedTask;
        }

        public Task RestorePermissionsAsync(int userId, CancellationToken cancellationToken = default)
        {
            RestoredUserIds.Add(userId);
            User = User with { PermissionOverrides = [] };
            return Task.CompletedTask;
        }

        public Task<TemporaryPasswordResult> CreateAsync(string username, string displayName, IReadOnlyCollection<int> roleIds, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task UpdateAsync(int userId, string username, string displayName, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SetActiveAsync(int userId, bool isActive, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task ReplaceRolesAsync(int userId, IReadOnlyCollection<int> roleIds, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<TemporaryPasswordResult> ResetPasswordAsync(int userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FakeRoleAdministrationService : IRoleAdministrationService
    {
        private readonly AdministrativePermissionDTO[] _catalog =
        [
            new(1, DecorPermissions.ProductsView, null), new(2, DecorPermissions.BrandsView, null),
            new(3, DecorPermissions.UsersView, null), new(4, DecorPermissions.UsersManagePermissions, null)
        ];
        public bool FailCatalog { get; set; }
        public bool FailGrants { get; set; }
        public List<int> RequestedRoleIds { get; } = [];

        public Task<IReadOnlyList<AdministrativePermissionDTO>> GetAllPermissionsAsync(CancellationToken cancellationToken = default)
            => FailCatalog
                ? Task.FromException<IReadOnlyList<AdministrativePermissionDTO>>(new InvalidOperationException("Catalog failed"))
                : Task.FromResult<IReadOnlyList<AdministrativePermissionDTO>>(_catalog);

        public Task<IReadOnlyList<AdministrativePermissionDTO>> GetPermissionsAsync(int roleId, CancellationToken cancellationToken = default)
        {
            RequestedRoleIds.Add(roleId);
            if (FailGrants) return Task.FromException<IReadOnlyList<AdministrativePermissionDTO>>(new InvalidOperationException("Grants failed"));
            return Task.FromResult<IReadOnlyList<AdministrativePermissionDTO>>(roleId switch
            {
                10 => [_catalog[0], _catalog[1]],
                20 => [_catalog[1], _catalog[2]],
                _ => throw new InvalidOperationException("Unexpected role")
            });
        }

        public Task<IReadOnlyList<AdministrativeRoleDTO>> GetRolesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task ReplacePermissionsAsync(int roleId, IReadOnlyCollection<int> permissionIds, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task RestoreDefaultsAsync(int roleId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FakeAuthorizationService : IAuthorizationService
    {
        public HashSet<string> GrantedPermissions { get; } = [];
        public FakeAuthorizationService() => GrantAll();
        public void GrantAll() => GrantedPermissions.UnionWith(
            [DecorPermissions.UsersManagePermissions, DecorPermissions.UsersRestorePermissions, DecorPermissions.UsersAssignRoles]);
        public bool HasPermission(string permissionCode) => GrantedPermissions.Contains(permissionCode);
        public bool CanView(string resource) => HasPermission($"{resource}.View");
        public bool CanCreate(string resource) => HasPermission($"{resource}.Create");
        public bool CanEdit(string resource) => HasPermission($"{resource}.Edit");
        public bool CanDelete(string resource) => HasPermission($"{resource}.Delete");
    }

    private sealed class FakeAuthenticatedUserContext : IAuthenticatedUserContext
    {
        public event EventHandler? SignedOut;
        public bool IsAuthenticated { get; set; } = true;
        public ApplicationUser? User { get; private set; } = new() { UserID = 7, Username = "operator" };
        public void SignIn(ApplicationUser user)
        {
            User = user;
            IsAuthenticated = true;
        }
        public void SignOut()
        {
            User = null;
            IsAuthenticated = false;
            SignedOut?.Invoke(this, EventArgs.Empty);
        }
    }
}