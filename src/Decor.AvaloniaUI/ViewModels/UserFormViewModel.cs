using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Decor.Core.Common;
using Decor.Core.DTOs;
using Decor.Core.Interfaces.Services;
using MySqlConnector;

namespace Decor.AvaloniaUI.ViewModels;

public sealed class UserFormViewModel : IUserFormViewModel
{
    private readonly IUserAdministrationService _users;
    private readonly IRoleAdministrationService? _roles;
    private readonly IAuthorizationService? _authorization;
    private readonly IAuthenticatedUserContext? _context;
    private readonly bool _isSystemAdministrator;
    private string _originalUsername;
    private bool _originalIsActive;
    private HashSet<int> _originalRoleIds;
    private string _username;
    private bool _isActive;
    private bool _isBusy;
    private bool _isCompleted;
    private bool _rolesLoaded;
    private string? _errorMessage;
    private string? _temporaryPassword;

    public UserFormViewModel(IUserAdministrationService users,
        IRoleAdministrationService? roles,
        IAuthorizationService? authorization = null,
        IAuthenticatedUserContext? context = null,
        AdministrativeUserDTO? user = null,
        int recordCount = 0)
    {
        _users = users;
        _roles = roles;
        _authorization = authorization;
        _context = context;
        UserId = user?.UserID ?? 0;
        _isSystemAdministrator = user?.IsSystemAdministrator ?? false;
        _username = _originalUsername = user?.Username ?? string.Empty;
        _isActive = _originalIsActive = user?.IsActive ?? true;
        _originalRoleIds = user?.Roles.Select(role => role.RoleID).ToHashSet() ?? [];
        foreach (var role in user?.Roles ?? [])
            Groups.Add(new EditUserRoleOption { Role = role, IsSelected = true });
        UserCodeDisplay = IsAdding
            ? RecordCodeDisplay.ForNewRecord(recordCount)
            : RecordCodeDisplay.ForExistingRecord(UserId);
        SaveCommand = new RelayCommand(async () => await SaveAsync(), () => CanSave);
        DismissCommand = new RelayCommand(() =>
        {
            if (!IsBusy) CloseRequested?.Invoke(this, EventArgs.Empty);
        }, () => !IsBusy);
    }

    public int UserId { get; }
    public bool IsAdding => UserId == 0;
    public string FormTitle => IsAdding ? "Novo usuário" : "Editar usuário";
    public string UserCodeDisplay { get; }
    public ObservableCollection<EditUserRoleOption> Groups { get; } = [];
    public ICommand SaveCommand { get; }
    public ICommand DismissCommand { get; }
    public string DismissButtonText => IsCompleted ? "Concluir" : "Cancelar";
    public bool WasSaved { get; private set; }
    public string? CreatedUsername { get; private set; }
    public string? SuccessMessage => IsCompleted
        ? "Usuário criado. Entregue a senha temporária ao usuário; a troca será obrigatória no primeiro login."
        : null;
    public string? TemporaryPassword => _temporaryPassword;
    public bool HasTemporaryPassword => !string.IsNullOrWhiteSpace(TemporaryPassword);
    public string Username { get => _username; set => SetField(ref _username, value); }
    public bool IsActive { get => _isActive; set => SetField(ref _isActive, value); }
    public bool IsBusy => _isBusy;
    public bool IsCompleted => _isCompleted;
    public string? ErrorMessage => _errorMessage;
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    private bool Allowed(string permission) => (_context?.IsAuthenticated ?? true)
        && (_authorization?.HasPermission(permission) ?? true);
    public bool CanEditFields => !IsBusy && !IsCompleted && !_isSystemAdministrator
        && Allowed(IsAdding ? DecorPermissions.UsersCreate : DecorPermissions.UsersEdit);
    public bool CanChangeActive => !IsAdding && CanEditFields
        && Allowed(_originalIsActive ? DecorPermissions.UsersDeactivate : DecorPermissions.UsersActivate);
    public bool CanEditGroups => CanEditFields && _rolesLoaded
        && (IsAdding || Allowed(DecorPermissions.UsersAssignRoles));
    private bool CanSave => CanEditFields && (!IsAdding || _rolesLoaded);
    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? CloseRequested;

    public async Task InitializeAsync()
    {
        if (IsBusy || IsCompleted) return;
        SetError(null);
        SetBusy(true);
        _rolesLoaded = false;
        try
        {
            if (_roles is null)
            {
                if (IsAdding) SetError("Não foi possível carregar os grupos de permissões.");
                return;
            }
            var roles = await _roles.GetRolesAsync();
            var assigned = Groups.Where(group => group.IsSelected).Select(group => group.Role.RoleID).ToHashSet();
            var options = Groups.Select(group => group.Role).Concat(roles)
                .DistinctBy(role => role.RoleID).ToArray();
            Groups.Clear();
            foreach (var role in options)
                Groups.Add(new EditUserRoleOption { Role = role, IsSelected = assigned.Contains(role.RoleID) });
            _rolesLoaded = true;
        }
        catch (Exception exception)
        {
            SetError(exception is UnauthorizedAccessException
                ? "Você não possui permissão para consultar os grupos."
                : "Não foi possível carregar os grupos de permissões.");
        }
        finally
        {
            SetBusy(false);
        }
    }

    public async Task SaveAsync()
    {
        if (!CanSave) return;
        SetError(null);
        var username = Username.Trim();
        if (string.IsNullOrWhiteSpace(username) || username.Length > 50)
        {
            SetError("Informe um username com até 50 caracteres.");
            return;
        }
        if (SystemAccountDefaults.IsAdministrator(username))
        {
            SetError("O nome admin é reservado à conta Administrador do sistema.");
            return;
        }

        var roleIds = Groups.Where(group => group.IsSelected).Select(group => group.Role.RoleID).ToHashSet();
        var groupsChanged = !_originalRoleIds.SetEquals(roleIds);
        var activeChanged = IsActive != _originalIsActive;
        var usernameChanged = username != _originalUsername;
        if (activeChanged && !CanChangeActive)
        {
            SetError("Você não possui permissão para alterar o estado deste usuário.");
            return;
        }
        if (groupsChanged && !CanEditGroups)
        {
            SetError("Você não possui permissão para atribuir grupos a este usuário.");
            return;
        }
        var changeCount = (usernameChanged ? 1 : 0) + (activeChanged ? 1 : 0) + (groupsChanged ? 1 : 0);
        if (!IsAdding && _context?.User?.UserID == UserId && changeCount > 1)
        {
            SetError("Altere apenas o usuário, o estado ou os grupos por vez na própria conta. A sessão será encerrada após salvar.");
            return;
        }

        SetBusy(true);
        try
        {
            if (IsAdding)
            {
                var result = await _users.CreateAsync(username, username, roleIds.ToArray());
                CreatedUsername = username;
                _temporaryPassword = result.TemporaryPassword;
                _isCompleted = true;
                WasSaved = true;
                foreach (var property in new[] { nameof(CreatedUsername), nameof(TemporaryPassword), nameof(HasTemporaryPassword), nameof(IsCompleted), nameof(SuccessMessage), nameof(DismissButtonText), nameof(WasSaved) })
                    OnPropertyChanged(property);
            }
            else
            {
                if (usernameChanged)
                {
                    await _users.UpdateAsync(UserId, username, username);
                    _originalUsername = username;
                    WasSaved = true;
                }
                if (activeChanged)
                {
                    await _users.SetActiveAsync(UserId, IsActive);
                    _originalIsActive = IsActive;
                    WasSaved = true;
                }
                if (groupsChanged)
                {
                    await _users.ReplaceRolesAsync(UserId, roleIds.ToArray());
                    _originalRoleIds = roleIds;
                    WasSaved = true;
                }
                OnPropertyChanged(nameof(WasSaved));
            }
        }
        catch (MySqlException exception) when (exception.Number == 1062)
        {
            SetError("Já existe um usuário com este username.");
            return;
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or InvalidOperationException)
        {
            SetError(string.IsNullOrWhiteSpace(exception.Message) ? "Não foi possível salvar o usuário." : exception.Message);
            return;
        }
        catch (Exception)
        {
            SetError("Não foi possível salvar o usuário.");
            return;
        }
        finally
        {
            SetBusy(false);
        }
        if (!IsAdding) CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private void SetBusy(bool value)
    {
        if (SetField(ref _isBusy, value, nameof(IsBusy))) RefreshState();
    }

    private void RefreshState()
    {
        foreach (var property in new[] { nameof(CanEditFields), nameof(CanChangeActive), nameof(CanEditGroups) })
            OnPropertyChanged(property);
        ((RelayCommand)SaveCommand).RaiseCanExecuteChanged();
        ((RelayCommand)DismissCommand).RaiseCanExecuteChanged();
    }

    private void SetError(string? value)
    {
        if (SetField(ref _errorMessage, value, nameof(ErrorMessage))) OnPropertyChanged(nameof(HasError));
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