using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Decor.AvaloniaUI.Services;
using Decor.AvaloniaUI.ViewModels;
using Decor.Core.Interfaces.Services;

namespace Decor.AvaloniaUI.Views;

public partial class PermissionsView : UserControl
{
    private bool _isGroupsOpen;
    public PermissionsView() => AvaloniaXamlLoader.Load(this);

    public PermissionsView(PermissionsViewModel viewModel, INavigationService navigation,
        IAuthenticatedUserContext context) : this()
    {
        DataContext = viewModel;
        viewModel.UserPermissions.AssignGroupsRequested += async (_, _) =>
        {
            if (_isGroupsOpen || viewModel.UserPermissions.SelectedUser is not { } user || TopLevel.GetTopLevel(this) is not Window owner) return;
            _isGroupsOpen = true;
            try
            {
                var window = navigation.Resolve<EditUserRolesWindow>();
                window.Initialize(user);
                await navigation.ShowDialogAsync(owner, window);
                if (window.WasSaved && context.IsAuthenticated) await viewModel.UserPermissions.ReloadSelectedAsync();
            }
            finally { _isGroupsOpen = false; }
        };
        _ = viewModel.InitializeAsync();
    }
}