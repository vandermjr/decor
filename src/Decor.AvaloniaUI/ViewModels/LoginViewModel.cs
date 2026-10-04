using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Decor.Core.Interfaces.Services;

namespace Decor.AvaloniaUI.ViewModels;

public sealed class LoginViewModel : INotifyPropertyChanged
{
    private readonly IAuthenticationService _authenticationService;
    private string _userIdInput = string.Empty;
    private string _password = string.Empty;
    private string? _errorMessage;
    private bool _canCopyError;
    private bool _isBusy;

    public LoginViewModel(IAuthenticationService authenticationService)
    {
        _authenticationService = authenticationService;
        LoginCommand = new RelayCommand(async () => await LoginAsync(), () => !IsBusy);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event Func<Task>? LoginSucceeded;
    public event EventHandler? PasswordChangeRequired;

    public ICommand LoginCommand { get; }

    public string UserIdInput
    {
        get => _userIdInput;
        set => SetField(ref _userIdInput, value);
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
