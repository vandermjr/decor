using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Decor.AvaloniaUI.ViewModels;

namespace Decor.AvaloniaUI.Views;

public partial class ChangePasswordWindow : Window
{
    private bool _required;
    private bool _completed;

    public ChangePasswordWindow()
    {
        InitializeComponent();
    }

    public ChangePasswordWindow(ChangePasswordViewModel viewModel)
        : this()
    {
        DataContext = viewModel;
        viewModel.PasswordChanged += OnPasswordChanged;
    }

    public void Configure(bool required)
    {
        _required = required;
        ((ChangePasswordViewModel)DataContext!).Configure(isCurrentPasswordRequired: !required);
    }

    public void SetSuccessAction(Action successAction) => _successAction = successAction;

    private Action? _successAction;

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (_required && !_completed)
            e.Cancel = true;

        base.OnClosing(e);
    }

    private void OnPasswordChanged(object? sender, EventArgs e)
    {
        _completed = true;
        _successAction?.Invoke();
        Close();
    }

    private void PasswordRequirements_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Control control && Resources["PasswordRequirementsFlyout"] is Flyout flyout)
            flyout.ShowAt(control);

        e.Handled = true;
    }

    private async void CopyErrorButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ChangePasswordViewModel { ErrorMessage: { Length: > 0 } errorMessage })
            return;

        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is not null)
            await clipboard.SetTextAsync(errorMessage);
    }
}
