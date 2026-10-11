using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Decor.Core.Common;
using Decor.Core.Interfaces.Services;

namespace Decor.AvaloniaUI.ViewModels;

public sealed class LoginViewModel : INotifyPropertyChanged
{
    private readonly IAuthenticationService _authenticationService;
    private readonly IUserSettingsService? _userSettingsService;
    private readonly IDatabaseHealthService? _databaseHealthService;
    private CancellationTokenSource? _themePreviewCancellation;
    private string _userIdInput = string.Empty;
    private string _password = string.Empty;
    private string? _errorMessage;
    private bool _canCopyError;
    private bool _isBusy;
    private bool _isDatabaseOnline;

    public LoginViewModel(IAuthenticationService authenticationService, IUserSettingsService? userSettingsService = null,
        IDatabaseHealthService? databaseHealthService = null)
    {
        _authenticationService = authenticationService;
        _userSettingsService = userSettingsService;
        _databaseHealthService = databaseHealthService;
        LoginCommand = new RelayCommand(async () => await LoginAsync(), () => !IsBusy);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event Func<Task>? LoginSucceeded;
    public event EventHandler? PasswordChangeRequired;
    public event Action<DecorThemeStyle>? ThemePreviewChanged;

    public ICommand LoginCommand { get; }
    public string SystemVersion => typeof(LoginViewModel).Assembly.GetName().Version?.ToString() ?? "Não informada";
    public bool IsDatabaseOnline { get => _isDatabaseOnline; private set => SetField(ref _isDatabaseOnline, value); }
    public string DatabaseStatusText => IsDatabaseOnline ? "Online" : "Offline";
    public string DatabaseConnectionDescription => _databaseHealthService?.ConnectionDescription
        ?? "Banco: não informado | Conexão: não informada";

    public string UserIdInput
    {
        get => _userIdInput;
        set
        {
            if (SetField(ref _userIdInput, value)) QueueThemePreview(value);
        }
    }

    public string Password
    {
        get => _password;
        set => SetField(ref _password, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (SetField(ref _errorMessage, value))
                OnPropertyChanged(nameof(HasError));
        }
    }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool CanCopyError { get => _canCopyError; private set => SetField(ref _canCopyError, value); }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetField(ref _isBusy, value))
                ((RelayCommand)LoginCommand).RaiseCanExecuteChanged();
        }
    }

    public async Task LoginAsync()
    {
        SetError(null, canCopy: false);
        if (!int.TryParse(UserIdInput.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var userId) || userId <= 0)
        {
            SetError("Informe um código de usuário válido.", canCopy: false);
            return;
        }

        IsBusy = true;
        try
        {
            AuthenticationResult result;
            try
            {
                result = await _authenticationService.AuthenticateAsync(userId, Password);
            }
            catch
            {
                SetError("Não foi possível acessar o serviço de autenticação.", canCopy: true);
                return;
            }

            if (!result.Succeeded)
            {
                SetError(result.ErrorMessage ?? "Não foi possível autenticar.", result.IsError);
                return;
            }

            Password = string.Empty;
            if (result.User!.MustChangePassword)
                PasswordChangeRequired?.Invoke(this, EventArgs.Empty);
            else
            {
                try
                {
                    if (LoginSucceeded is { } loginSucceeded)
                        await loginSucceeded();
                }
                catch (Exception exception)
                {
                    ReportInitializationFailure(exception);
                }
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task RefreshDatabaseStatusAsync(CancellationToken cancellationToken = default)
    {
        IsDatabaseOnline = false;
        if (_databaseHealthService is not null)
        {
            try { IsDatabaseOnline = await _databaseHealthService.IsOnlineAsync(cancellationToken); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch { IsDatabaseOnline = false; }
        }
        OnPropertyChanged(nameof(DatabaseStatusText));
    }

    public void StopThemePreview() => Interlocked.Exchange(ref _themePreviewCancellation, null)?.Cancel();

    private void QueueThemePreview(string input)
    {
        var previous = Interlocked.Exchange(ref _themePreviewCancellation, null);
        previous?.Cancel();
        previous?.Dispose();

        if (_userSettingsService is null)
            return;
        if (!int.TryParse(input.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var userId) || userId <= 0)
        {
            ThemePreviewChanged?.Invoke(DecorDefaults.Theme);
            return;
        }

        var cancellation = new CancellationTokenSource();
        Interlocked.Exchange(ref _themePreviewCancellation, cancellation);
        _ = PreviewThemeAsync(userId, cancellation);
    }

    private async Task PreviewThemeAsync(int userId, CancellationTokenSource cancellation)
    {
        try
        {
            await Task.Delay(300, cancellation.Token);
            var theme = await _userSettingsService!.GetThemeForUserAsync(userId, cancellation.Token);
            if (!cancellation.IsCancellationRequested)
                ThemePreviewChanged?.Invoke(theme);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        catch { }
        finally
        {
            if (ReferenceEquals(Interlocked.CompareExchange(ref _themePreviewCancellation, null, cancellation), cancellation))
                cancellation.Dispose();
        }
    }

    public void ReportInitializationFailure(Exception exception)
    {
        SetError($"O login foi validado, mas não foi possível iniciar o Decor. Detalhe: {exception.Message}", canCopy: true);
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void SetError(string? message, bool canCopy)
    {
        ErrorMessage = message;
        CanCopyError = canCopy && !string.IsNullOrWhiteSpace(message);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
