using Avalonia.Controls;
using Decor.AvaloniaUI.ViewModels;
using Decor.Core.DTOs;

namespace Decor.AvaloniaUI.Views;

public partial class EditUserRolesWindow : Window
{
    public EditUserRolesWindow()
    {
        InitializeComponent();
        Closing += (_, eventArgs) =>
        {
            if (DataContext is EditUserRolesViewModel { IsBusy: true })
                eventArgs.Cancel = true;
        };
    }

    public EditUserRolesWindow(EditUserRolesViewModel viewModel)
        : this()
    {
        DataContext = viewModel;
        viewModel.CloseRequested += (_, _) =>
        {
            WasSaved = true;
            Close();
        };
    }

    public bool WasSaved { get; private set; }

    public void Initialize(AdministrativeUserDTO user)
    {
        ((EditUserRolesViewModel)DataContext!).Initialize(user);
    }
}