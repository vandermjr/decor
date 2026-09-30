using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Decor.Core.Common;
using Decor.Core.DTOs;
using Decor.Core.Interfaces.Services;

namespace Decor.AvaloniaUI.ViewModels;

public sealed class RolePermissionToggle
{
    public required AdministrativePermissionDTO Permission { get; init; }
    public bool IsGranted { get; set; }
    public DecorPermissionPresentation Presentation => DecorPermissionPresentationCatalog.Describe(Permission.PermissionCode);
    public string ModuleName => Presentation.ModuleName;
    public string FormName => Presentation.FormName;
    public string ActionDisplayName => Presentation.ActionName;
}

public sealed class RolesViewModel : INotifyPropertyChanged
{
    private readonly IRoleAdministrationService _roleAdministrationService;
    private AdministrativeRoleDTO? _selectedRole;
    private string? _selectedModule;
    private string? _selectedForm;
    private bool _isLoading;
    private bool _isSaving;
    private string? _errorMessage;
    private int _permissionLoadVersion;
    private IReadOnlyList<RolePermissionToggle> _catalog = [];

    public RolesViewModel(IRoleAdministrationService roleAdministrationService)
    {
        _roleAdministrationService = roleAdministrationService;
        SaveCommand = new RelayCommand(async () => await SaveAsync(), () => SelectedRole is not null && !IsLoading && !IsSaving);
    }

    public ObservableCollection<AdministrativeRoleDTO> Roles { get; } = [];
    public ObservableCollection<string> Modules { get; } = [];
    public ObservableCollection<string> Forms { get; } = [];
    public ObservableCollection<RolePermissionToggle> Permissions { get; } = [];

    public ICommand SaveCommand { get; private set; }

    public AdministrativeRoleDTO? SelectedRole
    {
        get => _selectedRole;
        set
        {
            if (!SetField(ref _selectedRole, value)) return;
            _selectedModule = null;
            _selectedForm = null;
            Modules.Clear();
            Forms.Clear();
            Permissions.Clear();
            if (SaveCommand is RelayCommand relay)
                relay.RaiseCanExecuteChanged();
            if (value is not null)
                _ = LoadPermissionsAsync(value.RoleID, ++_permissionLoadVersion);
            else
            {
                _permissionLoadVersion++;
                IsLoading = false;
            }
        }
    }

    public string? SelectedModule
    {
        get => _selectedModule;
        set
        {
            if (!SetField(ref _selectedModule, value)) return;
            _selectedForm = null;
            Forms.Clear();
            Permissions.Clear();
            if (value is not null)
            {
                foreach (var form in _catalog.Where(item => item.ModuleName.Equals(value, StringComparison.OrdinalIgnoreCase))
                             .Select(item => item.FormName).Distinct(StringComparer.OrdinalIgnoreCase))
                    Forms.Add(form);
                if (Forms.Count > 0) SelectedForm = Forms.First();
            }
        }
    }

    public string? SelectedForm
    {
        get => _selectedForm;
        set
        {
            if (!SetField(ref _selectedForm, value)) return;
            Permissions.Clear();
            if (value is not null && SelectedModule is not null)
            {
                foreach (var permission in _catalog
                             .Where(item => string.Equals(item.ModuleName, SelectedModule, StringComparison.OrdinalIgnoreCase)
                                 && string.Equals(item.FormName, value, StringComparison.OrdinalIgnoreCase)))
                {
                    Permissions.Add(permission);
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

    private async Task LoadPermissionsAsync(int roleId, int requestVersion)
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var grantedPermissions = await _roleAdministrationService.GetPermissionsAsync(roleId);
            var allPermissions = await _roleAdministrationService.GetAllPermissionsAsync();
            var permissionSet = grantedPermissions.Select(permission => permission.PermissionID).ToHashSet();

            var catalog = allPermissions
                .Select(permission =>
                {
                    return new RolePermissionToggle
                    {
                        Permission = permission,
                        IsGranted = permissionSet.Contains(permission.PermissionID)
                    };
                })
                .OrderBy(item => item.ModuleName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.FormName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.ActionDisplayName, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (!IsCurrentPermissionLoad(roleId, requestVersion))
                return;

            _catalog = catalog;
            Modules.Clear();
            foreach (var module in DecorPermissionPresentationCatalog.ModuleNames.Where(module => catalog.Any(item => item.ModuleName == module)))
                Modules.Add(module);

            if (Modules.Count > 0)
                SelectedModule = Modules.First();
        }
        catch (Exception)
        {
            if (IsCurrentPermissionLoad(roleId, requestVersion))
                ErrorMessage = "Não foi possível carregar as permissões do grupo.";
        }
        finally
        {
            if (IsCurrentPermissionLoad(roleId, requestVersion))
            {
                IsLoading = false;
                if (SaveCommand is RelayCommand relay)
                    relay.RaiseCanExecuteChanged();
            }
        }
    }

    private bool IsCurrentPermissionLoad(int roleId, int requestVersion)
        => requestVersion == _permissionLoadVersion && SelectedRole?.RoleID == roleId;

    private async Task SaveAsync()
    {
        if (SelectedRole is null) return;

        var roleId = SelectedRole.RoleID;
        IsSaving = true;
        ErrorMessage = null;
        try
        {
            var permissionIds = _catalog
                .Where(item => item.IsGranted)
                .Select(item => item.Permission.PermissionID)
                .Distinct()
                .ToArray();

            await _roleAdministrationService.ReplacePermissionsAsync(roleId, permissionIds);
            if (SelectedRole?.RoleID == roleId)
                _ = LoadPermissionsAsync(roleId, ++_permissionLoadVersion);
        }
        catch (Exception)
        {
            if (SelectedRole?.RoleID == roleId)
                ErrorMessage = "Não foi possível salvar as permissões do grupo.";
        }
        finally
        {
            IsSaving = false;
            if (SaveCommand is RelayCommand relay)
                relay.RaiseCanExecuteChanged();
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
