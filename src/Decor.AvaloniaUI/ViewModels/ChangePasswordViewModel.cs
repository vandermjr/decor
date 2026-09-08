using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Decor.Core.Interfaces.Services;

namespace Decor.AvaloniaUI.ViewModels;

public sealed class ChangePasswordViewModel : INotifyPropertyChanged
{
    private readonly IPasswordChangeService _passwordChangeService;
    private string _currentPassword = string.Empty;
    private string _newPassword = string.Empty;
    private string _confirmPassword = string.Empty;
    private string? _errorMessage;
    private bool _canCopyError;
    private bool _isBusy;
    private bool _isCurrentPasswordRequired = true;

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? PasswordChanged;

    public ChangePasswordViewModel(IPasswordChangeService passwordChangeService)
    {
        _passwordChangeService = passwordChangeService;
        ChangePasswordCommand = new RelayCommand(async () => await ChangePasswordAsync(), () => !IsBusy);
    }

    public ICommand ChangePasswordCommand { get; }

    public string CurrentPassword { get => _currentPassword; set => SetField(ref _currentPassword, value); }
    public string NewPassword { get => _newPassword; set => SetField(ref _newPassword, value); }
    public string ConfirmPassword { get => _confirmPassword; set => SetField(ref _confirmPassword, value); }

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
                ((RelayCommand)ChangePasswordCommand).RaiseCanExecuteChanged();
        }
    }

    public bool IsCurrentPasswordRequired { get => _isCurrentPasswordRequired; private set => SetField(ref _isCurrentPasswordRequired, value); }
    public string Title => IsCurrentPasswordRequired ? "Alterar senha" : "Troca de senha necessária";
    public string Description => IsCurrentPasswordRequired ? "Informe sua senha atual e escolha uma nova." : "Para continuar, defina uma nova senha.";

    public void Configure(bool isCurrentPasswordRequired)
    {
        IsCurrentPasswordRequired = isCurrentPasswordRequired;
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Description));
    }

    private async Task ChangePasswordAsync()
    {
        SetError(null, canCopy: false);
        IsBusy = true;
        try
        {
            var result = IsCurrentPasswordRequired
                ? await _passwordChangeService.ChangePasswordAsync(CurrentPassword, NewPassword, ConfirmPassword)
                : await _passwordChangeService.ChangeRequiredPasswordAsync(NewPassword, ConfirmPassword);
            if (!result.Succeeded)
            {
                SetError(result.ErrorMessage ?? "Não foi possível alterar a senha.", result.IsError);
                return;
            }

            CurrentPassword = string.Empty;
            NewPassword = string.Empty;
            ConfirmPassword = string.Empty;
            PasswordChanged?.Invoke(this, EventArgs.Empty);
        }
        catch
        {
            SetError("Não foi possível alterar a senha.", canCopy: true);
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
