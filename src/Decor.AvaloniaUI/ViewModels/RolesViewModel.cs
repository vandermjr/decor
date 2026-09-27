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
    public string? AreaName { get; init; }
    public string? ScreenName { get; init; }
    public string PermissionDisplayName => !string.IsNullOrWhiteSpace(Permission.Description)
        ? Permission.Description.Trim()
        : ResolveActionLabel(Permission.PermissionCode);

    public static string ResolveActionLabel(string? permissionCode)
    {
        return permissionCode switch
        {
            DecorPermissions.ProductsView => "Consultar produtos",
            DecorPermissions.ProductsCreate => "Criar produto",
            DecorPermissions.ProductsEdit => "Editar produto",
            DecorPermissions.BrandsView => "Consultar marcas",
            DecorPermissions.BrandsCreate => "Criar marca",
            DecorPermissions.BrandsEdit => "Editar marca",
            DecorPermissions.BrandsDelete => "Excluir marca",
            DecorPermissions.ClassificationsView => "Consultar classificações",
            DecorPermissions.TermDeliveryView => "Consultar termo de entrega",
            DecorPermissions.UsersView => "Consultar usuários",
            DecorPermissions.UsersCreate => "Criar usuário",
            DecorPermissions.UsersEdit => "Editar usuário",
            DecorPermissions.UsersActivate => "Ativar usuário",
            DecorPermissions.UsersDeactivate => "Desativar usuário",
            DecorPermissions.UsersAssignRoles => "Atribuir grupos",
            DecorPermissions.UsersManagePermissions => "Gerenciar permissões de usuários",
            DecorPermissions.UsersResetPassword => "Redefinir senha de usuário",
            DecorPermissions.UsersRestorePermissions => "Restaurar permissões de usuário",
            DecorPermissions.RolesView => "Consultar grupos de permissões",
            DecorPermissions.RolesEdit => "Editar grupo de permissões",
            DecorPermissions.RolesManagePermissions => "Gerenciar permissões de grupos",
            DecorPermissions.RolesRestoreDefaults => "Restaurar permissões padrão",
            DecorPermissions.DatabaseMaintenanceView => "Consultar manutenção do banco de dados",
            _ => "Permissão"
        };
    }
}

public sealed class RolesViewModel : INotifyPropertyChanged
{
    private sealed record NavigationScreen(string Name, IReadOnlyList<string> PermissionCodes);
    private sealed record NavigationArea(string Name, IReadOnlyList<NavigationScreen> Screens, string? ScreenGroupName = null);

    private static readonly IReadOnlyList<NavigationArea> Navigation =
    [
        new("Cadastros",
        [
            new("Produtos", [DecorPermissions.ProductsView, DecorPermissions.ProductsCreate, DecorPermissions.ProductsEdit]),
            new("Marcas", [DecorPermissions.BrandsView, DecorPermissions.BrandsCreate, DecorPermissions.BrandsEdit, DecorPermissions.BrandsDelete]),
            new("Classificações", [DecorPermissions.ClassificationsView])
        ]),
        new("Comercial",
        [
            new("Termo de Entrega", [DecorPermissions.TermDeliveryView])
        ]),
        new("Configurações",
        [
            new("Usuários", [DecorPermissions.UsersView, DecorPermissions.UsersCreate, DecorPermissions.UsersEdit, DecorPermissions.UsersActivate, DecorPermissions.UsersDeactivate, DecorPermissions.UsersAssignRoles, DecorPermissions.UsersManagePermissions, DecorPermissions.UsersResetPassword, DecorPermissions.UsersRestorePermissions]),
            new("Grupos de Permissões", [DecorPermissions.RolesView, DecorPermissions.RolesEdit, DecorPermissions.RolesManagePermissions, DecorPermissions.RolesRestoreDefaults]),
            new("Manutenção do Banco de Dados", [DecorPermissions.DatabaseMaintenanceView])
        ], "Administração do Sistema")
    ];

    private static readonly IReadOnlyDictionary<string, (string AreaName, string ScreenName)> PermissionLocations =
        Navigation
            .SelectMany(area => area.Screens.SelectMany(screen => screen.PermissionCodes
                .Select(code => (Code: code, AreaName: area.Name, ScreenName: screen.Name))))
            .ToDictionary(item => item.Code, item => (item.AreaName, item.ScreenName), StringComparer.OrdinalIgnoreCase);

    private readonly IRoleAdministrationService _roleAdministrationService;
    private AdministrativeRoleDTO? _selectedRole;
    private string? _selectedModule;
    private string? _selectedScreen;
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
    public ObservableCollection<string> Screens { get; } = [];
    public ObservableCollection<RolePermissionToggle> Permissions { get; } = [];

    public ICommand SaveCommand { get; private set; }

    public AdministrativeRoleDTO? SelectedRole
    {
        get => _selectedRole;
        set
        {
            if (!SetField(ref _selectedRole, value)) return;
            _selectedModule = null;
            _selectedScreen = null;
            OnPropertyChanged(nameof(SelectedScreenGroupName));
            OnPropertyChanged(nameof(HasSelectedScreenGroup));
            Modules.Clear();
            Screens.Clear();
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
            OnPropertyChanged(nameof(SelectedScreenGroupName));
            OnPropertyChanged(nameof(HasSelectedScreenGroup));
            _selectedScreen = null;
            Screens.Clear();
            Permissions.Clear();
            if (value is not null)
            {
                var area = Navigation.FirstOrDefault(item => item.Name.Equals(value, StringComparison.OrdinalIgnoreCase));
                if (area is null) return;
                foreach (var screen in area.Screens)
                    Screens.Add(screen.Name);
                if (Screens.Count > 0)
                    SelectedScreen = Screens.First();
            }
        }
    }

    public string? SelectedScreen
    {
        get => _selectedScreen;
        set
        {
            if (!SetField(ref _selectedScreen, value)) return;
            Permissions.Clear();
            if (value is not null && SelectedModule is not null)
            {
                foreach (var permission in _catalog
                             .Where(item => string.Equals(item.AreaName, SelectedModule, StringComparison.OrdinalIgnoreCase)
                                 && string.Equals(item.ScreenName, value, StringComparison.OrdinalIgnoreCase)))
                {
                    Permissions.Add(permission);
                }
            }
        }
    }

    public string? SelectedScreenGroupName => Navigation
        .FirstOrDefault(item => string.Equals(item.Name, SelectedModule, StringComparison.OrdinalIgnoreCase))?
        .ScreenGroupName;
    public bool HasSelectedScreenGroup => !string.IsNullOrWhiteSpace(SelectedScreenGroupName);

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
                    PermissionLocations.TryGetValue(permission.PermissionCode, out var location);
                    return new RolePermissionToggle
                    {
                        Permission = permission,
                        IsGranted = permissionSet.Contains(permission.PermissionID),
                        AreaName = location.AreaName,
                        ScreenName = location.ScreenName
                    };
                })
                .OrderBy(item => item.PermissionDisplayName, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (!IsCurrentPermissionLoad(roleId, requestVersion))
                return;

            _catalog = catalog;
            Modules.Clear();
            foreach (var area in Navigation)
                Modules.Add(area.Name);

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
