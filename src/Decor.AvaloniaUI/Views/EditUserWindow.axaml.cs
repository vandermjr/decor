using Avalonia.Controls;
using Decor.AvaloniaUI.ViewModels;
using Decor.Core.DTOs;

namespace Decor.AvaloniaUI.Views;

public partial class EditUserWindow : Window
{
    public EditUserWindow()
    {
        InitializeComponent();
        Closing += (_, eventArgs) =>
        {
            if (DataContext is EditUserViewModel { IsBusy: true })
                eventArgs.Cancel = true;
        };
    }

    public EditUserWindow(EditUserViewModel viewModel)
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
        ((EditUserViewModel)DataContext!).Initialize(user);
    }
}