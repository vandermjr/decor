using Avalonia.Controls;
using Decor.AvaloniaUI.ViewModels;

namespace Decor.AvaloniaUI.Views;

public partial class CreateUserWindow : Window
{
    public CreateUserWindow()
    {
        InitializeComponent();
        Closing += (_, eventArgs) =>
        {
            if (DataContext is CreateUserViewModel { IsBusy: true })
                eventArgs.Cancel = true;
        };
    }

    public CreateUserWindow(CreateUserViewModel viewModel)
        : this()
    {
        DataContext = viewModel;
        viewModel.CloseRequested += (_, _) => Close();
        CreatedUsername = null;
        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(CreateUserViewModel.CreatedUsername))
                CreatedUsername = viewModel.CreatedUsername;
        };
        _ = viewModel.InitializeAsync();
    }

    public string? CreatedUsername { get; private set; }
}