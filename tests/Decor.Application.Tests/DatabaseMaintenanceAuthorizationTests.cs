using Decor.AvaloniaUI.Services;
using Decor.AvaloniaUI.ViewModels;
using Decor.Core.Common;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Services;

namespace Decor.Application.Tests;

public sealed class DatabaseMaintenanceAuthorizationTests
{
    [Fact]
    public void DatabaseMaintenanceView_IsRegisteredInPermissionCatalog()
    {
        DecorPermissions.DatabaseMaintenanceView.Should().Be("DatabaseMaintenance.View");
    }

    [Theory]
    [InlineData(SystemRoleDefaults.Administrators)]
    [InlineData(SystemRoleDefaults.Supervisors)]
    public void DatabaseMaintenanceView_IsGrantedByDefaultToSystemRole(string roleName)
    {
        SystemRoleDefaults.Permissions[roleName].Should().Contain(DecorPermissions.DatabaseMaintenanceView);
    }

    [Fact]
    public void DatabaseMaintenanceView_IsNotGrantedByDefaultToOtherRoles()
    {
        SystemRoleDefaults.Permissions.Should().NotContainKey("Operador");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ShowDatabaseMaintenanceCommand_ReflectsAuthorization(bool hasPermission)
    {
        var viewModel = new MainViewModel(
            new StubNavigationService(),
            new StubThemeService(),
            new StubAuthorizationService(hasPermission),
            new StubAuthenticatedUserContext());

        viewModel.ShowDatabaseMaintenanceCommand.CanExecute(null).Should().Be(hasPermission);
    }

    [Fact]
    public void MainViewModel_UsesEmployeeNameAsPrimaryPresentationForLinkedUser()
    {
        var userContext = new StubAuthenticatedUserContext(new ApplicationUser
        {
            Username = "jsilva",
            DisplayName = "Conta de Joao",
            EmployeeName = "Joao Carlos da Silva"
        });
        var viewModel = new MainViewModel(
            new StubNavigationService(),
            new StubThemeService(),
            new StubAuthorizationService(hasPermission: false),
            userContext);

        viewModel.UserPresentationName.Should().Be("Joao Carlos da Silva");
        viewModel.UserInitials.Should().Be("JS");
    }

    [Fact]
    public void MainViewModel_AdministratorWithoutEmployeeUsesSpecialAccountPresentation()
    {
        var viewModel = new MainViewModel(
            new StubNavigationService(),
            new StubThemeService(),
            new StubAuthorizationService(hasPermission: false),
            new StubAuthenticatedUserContext(new ApplicationUser
            {
                Username = "admin",
                DisplayName = "Nome alternativo",
                Roles = [SystemRoleDefaults.Administrators]
            }));

        viewModel.UserPresentationName.Should().Be("Administrador");
        viewModel.UserInitials.Should().Be("AD");
    }

    [Fact]
    public void MainViewModel_UserWithoutEmployeeFallsBackToUsernameRatherThanDisplayName()
    {
        var viewModel = new MainViewModel(
            new StubNavigationService(),
            new StubThemeService(),
            new StubAuthorizationService(hasPermission: false),
            new StubAuthenticatedUserContext(new ApplicationUser
            {
                Username = "jsilva",
                DisplayName = "Nome de apresentação"
            }));

        viewModel.UserPresentationName.Should().Be("jsilva");
        viewModel.UserInitials.Should().Be("JS");
    }

    [Fact]
    public void MainViewModel_TopThemeTogglePersistsAndDescribesNextTheme()
    {
        var themeService = new StubThemeService();
        var viewModel = new MainViewModel(
            new StubNavigationService(),
            themeService,
            new StubAuthorizationService(hasPermission: false),
            new StubAuthenticatedUserContext());

        viewModel.ToggleThemeCommand.Execute(null);

        themeService.CurrentTheme.Should().Be(DecorThemeStyle.Dark);
        viewModel.ThemeToolTip.Should().Be("Mudar para tema claro");
    }

    private sealed class StubAuthorizationService(bool hasPermission) : IAuthorizationService
    {
        public bool HasPermission(string permissionCode) => hasPermission && permissionCode == DecorPermissions.DatabaseMaintenanceView;
        public bool CanView(string resource) => false;
        public bool CanCreate(string resource) => false;
        public bool CanEdit(string resource) => false;
        public bool CanDelete(string resource) => false;
    }

    private sealed class StubNavigationService : INavigationService
    {
        public T Resolve<T>() where T : class => throw new InvalidOperationException();
        public Task ShowDialogAsync(Avalonia.Controls.Window owner, Avalonia.Controls.Window dialog) => Task.CompletedTask;
    }

    private sealed class StubThemeService : IThemeService
    {
        public DecorThemeStyle CurrentTheme { get; private set; } = DecorThemeStyle.Light;
        public event Action<DecorThemeStyle>? ThemeChanged
        {
            add { }
            remove { }
        }
        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SetThemeAsync(DecorThemeStyle newTheme)
        {
            CurrentTheme = newTheme;
            return Task.CompletedTask;
        }
    }

    private sealed class StubAuthenticatedUserContext(ApplicationUser? user = null) : IAuthenticatedUserContext
    {
        public event EventHandler? SignedOut
        {
            add { }
            remove { }
        }
        public bool IsAuthenticated => true;
        public ApplicationUser User { get; } = user ?? new() { Username = "test" };
        public void SignIn(ApplicationUser user) { }
        public void SignOut() { }
    }
}