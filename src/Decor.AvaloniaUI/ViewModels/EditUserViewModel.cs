using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Decor.Core.DTOs;
using Decor.Core.Interfaces.Services;
using MySqlConnector;

namespace Decor.AvaloniaUI.ViewModels;

public sealed class EditUserViewModel : INotifyPropertyChanged
{
    private readonly IUserAdministrationService _userAdministrationService;
    private int _userId;
    private string _username = string.Empty;
    private string _displayName = string.Empty;
    private string? _errorMessage;
    private bool _isBusy;

    public EditUserViewModel(IUserAdministrationService userAdministrationService)
    {
        _userAdministrationService = userAdministrationService;
        SaveCommand = new RelayCommand(async () => await SaveAsync(), () => !IsBusy);
        CancelCommand = new RelayCommand(() => CloseRequested?.Invoke(this, EventArgs.Empty), () => !IsBusy);
    }

    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }
    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? CloseRequested;

    public string Username { get => _username; set => SetField(ref _username, value); }
    public string DisplayName { get => _displayName; set => SetField(ref _displayName, value); }
    public string? ErrorMessage { get => _errorMessage; private set { if (SetField(ref _errorMessage, value)) OnPropertyChanged(nameof(HasError)); } }
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool IsBusy { get => _isBusy; private set { if (SetField(ref _isBusy, value)) { RaiseCommandStates(); OnPropertyChanged(nameof(CanEditFields)); } } }
    public bool CanEditFields => !IsBusy;

    public void Initialize(AdministrativeUserDTO user)
    {
        _userId = user.UserID;
        Username = user.Username;
        DisplayName = user.DisplayName;
        ErrorMessage = null;
    }

    private async Task SaveAsync()
    {
        ErrorMessage = null;
        if (string.IsNullOrWhiteSpace(Username) || Username.Trim().Length > 50)
        {
            ErrorMessage = "Informe um username com até 50 caracteres.";
            return;
        }

        if (string.IsNullOrWhiteSpace(DisplayName) || DisplayName.Trim().Length > 100)
        {
            ErrorMessage = "Informe um display name com até 100 caracteres.";
            return;
        }

        IsBusy = true;
        try
        {
            await _userAdministrationService.UpdateAsync(_userId, Username, DisplayName);
        }
        catch (MySqlException ex) when (ex.Number == 1062)
        {
            ErrorMessage = "Já existe um usuário com este username.";
            return;
        }
        catch (Exception)
        {
            ErrorMessage = "Não foi possível atualizar o usuário.";
            return;
        }
        finally
        {
            IsBusy = false;
        }

        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private void RaiseCommandStates()
    {
        ((RelayCommand)SaveCommand).RaiseCanExecuteChanged();
        ((RelayCommand)CancelCommand).RaiseCanExecuteChanged();
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}