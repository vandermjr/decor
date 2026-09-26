using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Decor.Core.DTOs;
using Decor.Core.Interfaces.Services;

namespace Decor.AvaloniaUI.ViewModels;

public sealed class RolePermissionToggle
{
    public required AdministrativePermissionDTO Permission { get; init; }
    public bool IsGranted { get; set; }
    public string ModuleName => Permission.PermissionCode.Split('.', 2, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)[0];
    public string NormalizedAction => Permission.PermissionCode.Contains('.') ? Permission.PermissionCode.Split('.', 2, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)[1] : Permission.PermissionCode;
    public string ActionDisplayName => NormalizedAction switch
    {
        "View" => "Ver",
        "Create" => "Criar",
        "Edit" => "Editar",
        "Delete" => "Deletar",
        "Activate" => "Ativar",
        "Deactivate" => "Desativar",
        "AssignRoles" => "Atribuir grupos",
        "ManagePermissions" => "Gerenciar permissões",
        "RestorePermissions" => "Restaurar permissões",
        "ResetPassword" => "Redefinir senha",
        "Approve" => "Aprovar",
        "Cancel" => "Cancelar",
        "Close" => "Encerrar",
        _ => NormalizedAction
    };
}

public sealed class RolesViewModel : INotifyPropertyChanged
{
    private readonly IRoleAdministrationService _roleAdministrationService;
    private AdministrativeRoleDTO? _selectedRole;
    private string? _selectedModule;
    private string? _selectedAction;
    private bool _isLoading;
    private bool _isSaving;
    private string? _errorMessage;
    private IReadOnlyList<RolePermissionToggle> _catalog = [];

    public RolesViewModel(IRoleAdministrationService roleAdministrationService)
    {
        _roleAdministrationService = roleAdministrationService;
        SaveCommand = new RelayCommand(async () => await SaveAsync(), () => SelectedRole is not null && !IsLoading && !IsSaving);
    }

    public ObservableCollection<AdministrativeRoleDTO> Roles { get; } = [];
    public ObservableCollection<string> Modules { get; } = [];
    public ObservableCollection<string> Actions { get; } = [];
    public ObservableCollection<RolePermissionToggle> Permissions { get; } = [];

    public ICommand SaveCommand { get; private set; }

    public AdministrativeRoleDTO? SelectedRole
    {
        get => _selectedRole;
        set
        {
            if (!SetField(ref _selectedRole, value)) return;
            _selectedModule = null;
            _selectedAction = null;
            Modules.Clear();
            Actions.Clear();
            Permissions.Clear();
            if (SaveCommand is RelayCommand relay)
                relay.RaiseCanExecuteChanged();
            if (value is not null)
                _ = LoadPermissionsAsync(value.RoleID);
        }
    }

    public string? SelectedModule
    {
        get => _selectedModule;
        set
        {
            if (!SetField(ref _selectedModule, value)) return;
            _selectedAction = null;
            Actions.Clear();
            Permissions.Clear();
            if (value is not null)
            {
                var options = _catalog.Where(item => item.ModuleName.Equals(value, StringComparison.OrdinalIgnoreCase));
                foreach (var action in options.Select(item => item.NormalizedAction).Distinct(StringComparer.OrdinalIgnoreCase))
                    Actions.Add(action);
                if (Actions.Count > 0)
                    SelectedAction = Actions.First();
            }
        }
    }

    public string? SelectedAction
    {
        get => _selectedAction;
        set
        {
            if (!SetField(ref _selectedAction, value)) return;
            Permissions.Clear();
            if (value is not null && SelectedModule is not null)
            {
                foreach (var permission in _catalog
                             .Where(item => item.ModuleName.Equals(SelectedModule, StringComparison.OrdinalIgnoreCase)
                                 && item.NormalizedAction.Equals(value, StringComparison.OrdinalIgnoreCase)))
                {
                    Permissions.Add(new RolePermissionToggle
                    {
                        Permission = permission.Permission,
                        IsGranted = permission.IsGranted
                    });
                }
            }
        }
    }

    public bool HasSelectedRole => SelectedRole is not null;
    public bool IsLoading
    {
        get => _isLoading;
        private set => SetField(ref _isLoading, value);
    }

    public bool IsSaving
    {
        get => _isSaving;
        private set => SetField(ref _isSaving, value);
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

    public event PropertyChangedEventHandler? PropertyChanged;

    public async Task InitializeAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            Roles.Clear();
            foreach (var role in await _roleAdministrationService.GetRolesAsync())
                Roles.Add(role);
        }
        catch (Exception)
        {
            ErrorMessage = "Não foi possível carregar os grupos de permissões.";
        }
        finally
        {
            IsLoading = false;
            if (SaveCommand is RelayCommand relay)
                relay.RaiseCanExecuteChanged();
        }
    }

    private async Task LoadPermissionsAsync(int roleId)
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var grantedPermissions = await _roleAdministrationService.GetPermissionsAsync(roleId);
            var allPermissions = await _roleAdministrationService.GetAllPermissionsAsync();
            var permissionSet = grantedPermissions.Select(permission => permission.PermissionID).ToHashSet();

            _catalog = allPermissions
                .Select(permission => new RolePermissionToggle
                {
                    Permission = permission,
                    IsGranted = permissionSet.Contains(permission.PermissionID)
                })
                .OrderBy(item => item.ModuleName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.NormalizedAction, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.Permission.PermissionCode, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            Modules.Clear();
            foreach (var module in _catalog.Select(item => item.ModuleName).Distinct(StringComparer.OrdinalIgnoreCase))
                Modules.Add(module);

            if (Modules.Count > 0)
                SelectedModule = Modules.First();
        }
        catch (Exception)
        {
            ErrorMessage = "Não foi possível carregar as permissões do grupo.";
        }
        finally
        {
            IsLoading = false;
            if (SaveCommand is RelayCommand relay)
                relay.RaiseCanExecuteChanged();
        }
    }

    private async Task SaveAsync()
    {
        if (SelectedRole is null) return;

        IsSaving = true;
        ErrorMessage = null;
        try
        {
            var permissionIds = Permissions
                .Where(item => item.IsGranted)
                .Select(item => item.Permission.PermissionID)
                .Distinct()
                .ToArray();

            await _roleAdministrationService.ReplacePermissionsAsync(SelectedRole.RoleID, permissionIds);
        }
        catch (Exception)
        {
            ErrorMessage = "Não foi possível salvar as permissões do grupo.";
        }
        finally
        {
            IsSaving = false;
            if (SaveCommand is RelayCommand relay)
                relay.RaiseCanExecuteChanged();
            _ = LoadPermissionsAsync(SelectedRole.RoleID);
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
