using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Decor.Core.Common;
using Decor.Core.DTOs;
using Decor.Core.Interfaces.Services;
using MySqlConnector;

namespace Decor.AvaloniaUI.ViewModels;

public sealed class EditUserViewModel : IUserFormViewModel
{
    private readonly IUserAdministrationService _userAdministrationService;
    private readonly IAuthorizationService? _authorization;
    private int _userId;
    private string _username = string.Empty;
    private string _displayName = string.Empty;
    private string? _errorMessage;
    private bool _isBusy;
    private bool _isSystemAdministrator;
    private bool _isActive;
    private bool _originalIsActive;

    public EditUserViewModel(IUserAdministrationService userAdministrationService, IAuthorizationService? authorization = null)
    {
        _userAdministrationService = userAdministrationService;
        _authorization = authorization;
        SaveCommand = new RelayCommand(async () => await SaveAsync(), () => CanEditFields);
        CancelCommand = new RelayCommand(() => CloseRequested?.Invoke(this, EventArgs.Empty), () => !IsBusy);
    }

    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand DismissCommand => CancelCommand;
    public string FormTitle => "Editar usuário";
    public string DismissButtonText => "Cancelar";
    public bool IsCompleted => false;
    public bool WasSaved { get; private set; }
    public int UserId => _userId;
    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? CloseRequested;

    public string Username { get => _username; set => SetField(ref _username, value); }
    public string UserCodeDisplay => RecordCodeDisplay.ForExistingRecord(_userId);
    public string DisplayName { get => _displayName; set => SetField(ref _displayName, value); }
    public bool IsActive { get => _isActive; set => SetField(ref _isActive, value); }
    public bool CanChangeActive => CanEditFields && (_authorization?.HasPermission(_originalIsActive ? DecorPermissions.UsersDeactivate : DecorPermissions.UsersActivate) ?? true);
    public string? ErrorMessage { get => _errorMessage; private set { if (SetField(ref _errorMessage, value)) OnPropertyChanged(nameof(HasError)); } }
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool IsBusy { get => _isBusy; private set { if (SetField(ref _isBusy, value)) { RaiseCommandStates(); OnPropertyChanged(nameof(CanEditFields)); OnPropertyChanged(nameof(CanChangeActive)); } } }
    public bool CanEditFields => !IsBusy && !_isSystemAdministrator && (_authorization?.HasPermission(DecorPermissions.UsersEdit) ?? true);

    public void Initialize(AdministrativeUserDTO user)
    {
        WasSaved = false;
        _userId = user.UserID;
        OnPropertyChanged(nameof(UserCodeDisplay));
        _isSystemAdministrator = user.IsSystemAdministrator;
        Username = user.Username;
        DisplayName = user.DisplayName;
        _originalIsActive = user.IsActive;
        IsActive = user.IsActive;
        ErrorMessage = null;
        OnPropertyChanged(nameof(CanEditFields));
        OnPropertyChanged(nameof(CanChangeActive));
        RaiseCommandStates();
    }

    public async Task SaveAsync()
    {
        if (!CanEditFields) return;
        if (IsActive != _originalIsActive && !CanChangeActive)
        {
            ErrorMessage = "Você não possui permissão para alterar o estado deste usuário.";
            return;
        }
        ErrorMessage = null;
        if (string.IsNullOrWhiteSpace(Username) || Username.Trim().Length > 50)
        {
            ErrorMessage = "Informe um username com até 50 caracteres.";
            return;
        }

        if (SystemAccountDefaults.IsAdministrator(Username.Trim()))
        {
            ErrorMessage = "O nome admin é reservado à conta Administrador do sistema.";
            return;
        }

        IsBusy = true;
        try
        {
            await _userAdministrationService.UpdateAsync(_userId, Username.Trim(), Username.Trim());
            if (IsActive != _originalIsActive)
            {
                await _userAdministrationService.SetActiveAsync(_userId, IsActive);
                _originalIsActive = IsActive;
            }
        }
        catch (MySqlException ex) when (ex.Number == 1062)
        {
            ErrorMessage = "Já existe um usuário com este username.";
            return;
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or InvalidOperationException)
        {
            ErrorMessage = exception.Message;
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

        WasSaved = true;
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