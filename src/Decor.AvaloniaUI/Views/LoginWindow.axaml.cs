using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using Decor.AvaloniaUI.ViewModels;
using Decor.AvaloniaUI.Services;
using Decor.Core.Interfaces.Services;
using Microsoft.Extensions.Logging;

namespace Decor.AvaloniaUI.Views;

public partial class LoginWindow : Window
{
    private readonly INavigationService? _navigationService;
    private readonly IThemeService? _themeService;
    private readonly IIconAppearanceService? _iconAppearanceService;
    private readonly IAuthenticatedUserContext? _authenticatedUserContext;
    private readonly ILogger<LoginWindow>? _logger;

    public LoginWindow()
    {
        InitializeComponent();
        Opened += (_, _) => Dispatcher.UIThread.Post(FocusUserId, DispatcherPriority.Input);
    }

    private void FocusUserId()
    {
        if (!UserIdTextBox.IsEffectivelyVisible || !UserIdTextBox.IsEffectivelyEnabled)
            return;

        UserIdTextBox.Focus();
        UserIdTextBox.CaretIndex = UserIdTextBox.Text?.Length ?? 0;
    }

    public LoginWindow(LoginViewModel viewModel, INavigationService navigationService, IThemeService themeService,
        IIconAppearanceService iconAppearanceService, IAuthenticatedUserContext authenticatedUserContext, ILogger<LoginWindow> logger)
        : this()
    {
        DataContext = viewModel;
        _navigationService = navigationService;
        _themeService = themeService;
        _iconAppearanceService = iconAppearanceService;
        _authenticatedUserContext = authenticatedUserContext;
        _logger = logger;
        viewModel.LoginSucceeded += OnLoginSucceededAsync;
        viewModel.PasswordChangeRequired += OnPasswordChangeRequired;
    }

    private async Task OnLoginSucceededAsync()
    {
        try
        {
            await _themeService!.InitializeAsync();
            await _iconAppearanceService!.InitializeAsync();
            var mainWindow = _navigationService!.Resolve<MainWindow>();
            if (global::Avalonia.Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
                throw new InvalidOperationException("A aplicação não está em execução no ciclo de vida desktop esperado.");

            desktop.MainWindow = mainWindow;

            mainWindow.Show();
            Close();
        }
        catch (Exception exception)
        {
            _logger?.LogError(exception, "Falha ao inicializar o Decor após autenticação válida.");
            _authenticatedUserContext?.SignOut();
            if (DataContext is LoginViewModel loginViewModel)
                loginViewModel.ReportInitializationFailure(exception);
        }
    }

    private async void OnPasswordChangeRequired(object? sender, EventArgs e)
    {
        var changePasswordWindow = _navigationService!.Resolve<ChangePasswordWindow>();
        changePasswordWindow.Configure(required: true);
        changePasswordWindow.SetSuccessAction(OnLoginSucceededAsync);
        await _navigationService.ShowDialogAsync(this, changePasswordWindow);
    }

    private async void CopyErrorButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not LoginViewModel { ErrorMessage: { Length: > 0 } errorMessage })
            return;

        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is not null)
            await clipboard.SetTextAsync(errorMessage);
    }
}
