using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Decor.Core.Common;
using Decor.Core.DTOs;
using Decor.Core.Interfaces.Services;

namespace Decor.AvaloniaUI.ViewModels;

public sealed class RoleFunctionalContext : INotifyPropertyChanged
{
    private bool _isAssociated;

    public RoleFunctionalContext(DecorFunctionalPermissionContext context, bool isAssociated)
    {
        Context = context;
        _isAssociated = isAssociated;
    }

    public DecorFunctionalPermissionContext Context { get; }
    public string ContextName => Context.ContextName;
    public bool IsAssociated
    {
        get => _isAssociated;
        set
        {
            if (_isAssociated == value)
                return;

            _isAssociated = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsAssociated)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

public sealed class RolesViewModel : INotifyPropertyChanged
{
    private readonly IRoleAdministrationService _roleAdministrationService;
    private AdministrativeRoleDTO? _selectedRole;
    private string? _selectedModule;
    private bool _isLoading;
    private bool _isSaving;
    private string? _errorMessage;
    private int _permissionLoadVersion;
    private IReadOnlyList<AdministrativePermissionDTO> _allPermissions = [];
    private readonly HashSet<int> _selectedPermissionIds = [];
    private readonly HashSet<string> _grantedPermissionCodes = new(StringComparer.OrdinalIgnoreCase);

    public RolesViewModel(IRoleAdministrationService roleAdministrationService)
    {
        _roleAdministrationService = roleAdministrationService;
        SaveCommand = new RelayCommand(async () => await SaveAsync(), () => SelectedRole is not null && !IsLoading && !IsSaving);
    }

    public ObservableCollection<AdministrativeRoleDTO> Roles { get; } = [];
    public ObservableCollection<string> Modules { get; } = [];
    public ObservableCollection<RoleFunctionalContext> Forms { get; } = [];

    public ICommand SaveCommand { get; private set; }

    public AdministrativeRoleDTO? SelectedRole
    {
        get => _selectedRole;
        set
        {
            if (!SetField(ref _selectedRole, value)) return;
            _selectedModule = null;
            Modules.Clear();
            Forms.Clear();
            _allPermissions = [];
            _selectedPermissionIds.Clear();
            _grantedPermissionCodes.Clear();
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
            Forms.Clear();
            if (value is not null)
            {
                foreach (var context in DecorFunctionalPermissionCatalog.Contexts
                             .Where(context => string.Equals(context.ModuleName, value, StringComparison.Ordinal)))
                {
                    var form = new RoleFunctionalContext(
                        context,
                        context.PermissionCodes.Any(_grantedPermissionCodes.Contains));
                    form.PropertyChanged += OnFunctionalContextPropertyChanged;
                    Forms.Add(form);
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
            ErrorMessage = "Não foi possível carregar os grupos funcionais.";
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

            if (!IsCurrentPermissionLoad(roleId, requestVersion))
                return;

            _allPermissions = allPermissions;
            _selectedPermissionIds.Clear();
            _grantedPermissionCodes.Clear();
            foreach (var permission in grantedPermissions)
            {
                _selectedPermissionIds.Add(permission.PermissionID);
                _grantedPermissionCodes.Add(permission.PermissionCode);
            }

            Modules.Clear();
            foreach (var module in DecorFunctionalPermissionCatalog.ModuleNames)
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

    private void OnFunctionalContextPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (sender is not RoleFunctionalContext form
            || eventArgs.PropertyName != nameof(RoleFunctionalContext.IsAssociated))
            return;

        foreach (var permissionCode in form.Context.PermissionCodes)
        {
            if (form.IsAssociated)
                _grantedPermissionCodes.Add(permissionCode);
            else
                _grantedPermissionCodes.Remove(permissionCode);

            foreach (var permission in _allPermissions.Where(permission =>
                         string.Equals(permission.PermissionCode, permissionCode, StringComparison.OrdinalIgnoreCase)))
            {
                if (form.IsAssociated)
                    _selectedPermissionIds.Add(permission.PermissionID);
                else
                    _selectedPermissionIds.Remove(permission.PermissionID);
            }
        }
    }

    private async Task SaveAsync()
    {
        if (SelectedRole is null) return;

        var roleId = SelectedRole.RoleID;
        IsSaving = true;
        ErrorMessage = null;
        try
        {
            var permissionIds = _selectedPermissionIds.ToArray();

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
