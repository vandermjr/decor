using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Decor.Core.Interfaces.Services;

namespace Decor.AvaloniaUI.ViewModels;

public sealed class LoginViewModel : INotifyPropertyChanged
{
    private readonly IAuthenticationService _authenticationService;
    private string _username = string.Empty;
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
    public event EventHandler? LoginSucceeded;
    public event EventHandler? PasswordChangeRequired;

    public ICommand LoginCommand { get; }

    public string Username
    {
        get => _username;
        set => SetField(ref _username, value);
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

    private async Task LoginAsync()
    {
        SetError(null, canCopy: false);
        IsBusy = true;
        try
        {
            var result = await _authenticationService.AuthenticateAsync(Username, Password);
            if (!result.Succeeded)
            {
                SetError(result.ErrorMessage ?? "Não foi possível autenticar.", result.IsError);
                return;
            }

            Password = string.Empty;
            if (result.User!.MustChangePassword)
                PasswordChangeRequired?.Invoke(this, EventArgs.Empty);
            else
                LoginSucceeded?.Invoke(this, EventArgs.Empty);
        }
        catch
        {
            SetError("Não foi possível acessar o serviço de autenticação.", canCopy: true);
        }
        finally
        {
            IsBusy = false;
        }
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
