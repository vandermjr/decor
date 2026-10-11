using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Decor.Core.Common;
using Decor.Core.DTOs;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Services;
using MySqlConnector;

namespace Decor.AvaloniaUI.ViewModels;

public sealed class CreateUserRoleOption
{
    public required AdministrativeRoleDTO Role { get; init; }
    public bool IsSelected { get; set; }
}

public sealed class CreateUserViewModel : IUserFormViewModel
{
    private readonly IUserAdministrationService _userAdministrationService;
    private readonly IRoleAdministrationService _roleAdministrationService;
    private readonly IAuthorizationService? _authorization;
    private string _username = string.Empty;
    private string _displayName = string.Empty;
    private string? _errorMessage;
    private string? _successMessage;
    private string? _temporaryPassword;
    private bool _isBusy;
    private bool _isCompleted;
    private string? _createdUsername;

    public CreateUserViewModel(
        IUserAdministrationService userAdministrationService,
        IRoleAdministrationService roleAdministrationService,
        IAuthorizationService? authorization = null)
    {
        _userAdministrationService = userAdministrationService;
        _roleAdministrationService = roleAdministrationService;
        _authorization = authorization;
        CreateCommand = new RelayCommand(async () => await CreateAsync(), () => CanEditFields);
        CancelCommand = new RelayCommand(() => CloseRequested?.Invoke(this, EventArgs.Empty), () => !IsBusy && !IsCompleted);
        FinishCommand = new RelayCommand(() => CloseRequested?.Invoke(this, EventArgs.Empty), () => IsCompleted);
        DismissCommand = new RelayCommand(() =>
        {
            if (!IsBusy) CloseRequested?.Invoke(this, EventArgs.Empty);
        }, () => !IsBusy);
    }

    public ObservableCollection<CreateUserRoleOption> Roles { get; } = [];
    public ICommand CreateCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand FinishCommand { get; }
    public ICommand SaveCommand => CreateCommand;
    public ICommand DismissCommand { get; }
    public string FormTitle => "Novo usuário";
    public string DismissButtonText => IsCompleted ? "Concluir" : "Cancelar";
    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? CloseRequested;

    public string Username { get => _username; set => SetField(ref _username, value); }
    public string DisplayName { get => _displayName; set => SetField(ref _displayName, value); }
    public string? ErrorMessage { get => _errorMessage; private set { if (SetField(ref _errorMessage, value)) OnPropertyChanged(nameof(HasError)); } }
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public string? SuccessMessage { get => _successMessage; private set => SetField(ref _successMessage, value); }
    public string? TemporaryPassword { get => _temporaryPassword; private set => SetField(ref _temporaryPassword, value); }
    public bool HasTemporaryPassword => !string.IsNullOrWhiteSpace(TemporaryPassword);
    public bool IsBusy { get => _isBusy; private set { if (SetField(ref _isBusy, value)) { RaiseCommandStates(); OnPropertyChanged(nameof(CanEditFields)); } } }
    public bool IsCompleted { get => _isCompleted; private set { if (SetField(ref _isCompleted, value)) { RaiseCommandStates(); OnPropertyChanged(nameof(CanEditFields)); OnPropertyChanged(nameof(DismissButtonText)); } } }
    public bool CanEditFields => !IsBusy && !IsCompleted && (_authorization?.HasPermission(DecorPermissions.UsersCreate) ?? true);
    public string? CreatedUsername { get => _createdUsername; private set => SetField(ref _createdUsername, value); }

    public async Task InitializeAsync()
    {
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            Roles.Clear();
            foreach (var role in await _roleAdministrationService.GetRolesAsync())
                Roles.Add(new CreateUserRoleOption { Role = role });
        }
        catch (Exception)
        {
            ErrorMessage = "Não foi possível carregar os grupos de permissões.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task CreateAsync()
    {
        if (!CanEditFields) return;
        ErrorMessage = null;
        SuccessMessage = null;
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
            var roleIds = Roles.Where(option => option.IsSelected).Select(option => option.Role.RoleID).ToArray();
            var result = await _userAdministrationService.CreateAsync(Username.Trim(), Username.Trim(), roleIds);
            CreatedUsername = Username.Trim();
            TemporaryPassword = result.TemporaryPassword;
            OnPropertyChanged(nameof(HasTemporaryPassword));
            SuccessMessage = "Usuário criado. Entregue a senha temporária ao usuário; a troca será obrigatória no primeiro login.";
            IsCompleted = true;
        }
        catch (MySqlException ex) when (ex.Number == 1062)
        {
            ErrorMessage = "Já existe um usuário com este username.";
        }
        catch (Exception)
        {
            ErrorMessage = "Não foi possível criar o usuário.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void RaiseCommandStates()
    {
        ((RelayCommand)CreateCommand).RaiseCanExecuteChanged();
        ((RelayCommand)CancelCommand).RaiseCanExecuteChanged();
        ((RelayCommand)FinishCommand).RaiseCanExecuteChanged();
        ((RelayCommand)DismissCommand).RaiseCanExecuteChanged();
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