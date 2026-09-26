using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Decor.Core.DTOs;
using Decor.Core.Interfaces.Services;

namespace Decor.AvaloniaUI.ViewModels;

public sealed class UserCascadePermissionItem
{
    public required AdministrativePermissionDTO Permission { get; init; }
    public bool IsGranted { get; set; }
    public string ModuleName => GetModuleName(Permission.PermissionCode);
    public string ScreenName => GetScreenName(Permission.PermissionCode);
    public string ActionDisplayName => GetActionDisplayName(Permission.PermissionCode);

    public static string GetModuleName(string permissionCode)
    {
        var parts = permissionCode.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length > 0 ? parts[0] : permissionCode;
    }

    public static string GetScreenName(string permissionCode)
    {
        var parts = permissionCode.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length > 1 ? parts[1] : permissionCode;
    }

    public static string GetActionDisplayName(string permissionCode)
    {
        var action = GetScreenName(permissionCode);
        return action switch
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
            "Register" => "Registrar",
            "Approve" => "Aprovar",
            "Cancel" => "Cancelar",
            "Respond" => "Responder",
            "Close" => "Encerrar",
            "Release" => "Liberar",
            "Update" => "Atualizar",
            _ => action
        };
    }
}

public sealed class UsersViewModel : INotifyPropertyChanged, IStatusBarSource
{
    private readonly IUserAdministrationService _userAdministrationService;
    private readonly IRoleAdministrationService _roleAdministrationService;
    private string _search = string.Empty;
    private string _statusMessage = string.Empty;
    private bool _isBusy;
    private AdministrativeUserDTO? _selectedUser;
    private AdministrativeRoleDTO? _selectedRole;
    private string? _selectedModule;
    private string? _selectedScreen;
    private IReadOnlyList<UserCascadePermissionItem> _rolePermissionCatalog = [];

    public UsersViewModel(IUserAdministrationService userAdministrationService, IRoleAdministrationService roleAdministrationService)
    {
        _userAdministrationService = userAdministrationService;
        _roleAdministrationService = roleAdministrationService;
        SearchCommand = new RelayCommand(async () => await LoadUsersAsync(), () => !IsBusy);
        ClearSelectionCommand = new RelayCommand(ClearSelection, () => SelectedUser is not null);
        NewUserCommand = new RelayCommand(() => NewUserRequested?.Invoke(this, EventArgs.Empty), () => !IsBusy);
        EditUserCommand = new RelayCommand(() => EditUserRequested?.Invoke(this, EventArgs.Empty), () => !IsBusy && SelectedUser is not null);
        EditUserRolesCommand = new RelayCommand(() => EditUserRolesRequested?.Invoke(this, EventArgs.Empty), () => !IsBusy && SelectedUser is not null);
        ToggleActiveCommand = new RelayCommand(async () => await ToggleActiveAsync(), () => !IsBusy && SelectedUser is not null);
        ConfirmToggleActiveCommand = new RelayCommand(async () => await ConfirmToggleActiveAsync(), () => !IsBusy && ShowToggleUserConfirmation);
        CancelToggleActiveCommand = new RelayCommand(CancelToggleActive, () => !IsBusy && ShowToggleUserConfirmation);
    }

    public UsersViewModel(IUserAdministrationService userAdministrationService)
        : this(userAdministrationService, new NoOpRoleAdministrationService())
    {
    }

    public ObservableCollection<AdministrativeUserDTO> Users { get; } = [];
    public ObservableCollection<AdministrativeRoleDTO> UserRoles { get; } = [];
    public ObservableCollection<string> Modules { get; } = [];
    public ObservableCollection<string> Screens { get; } = [];
    public ObservableCollection<UserCascadePermissionItem> Permissions { get; } = [];

    public ICommand SearchCommand { get; private set; }
    public ICommand ClearSelectionCommand { get; private set; }
    public ICommand NewUserCommand { get; private set; }
    public ICommand EditUserCommand { get; private set; }
    public ICommand EditUserRolesCommand { get; private set; }
    public ICommand ToggleActiveCommand { get; private set; }
    public ICommand ConfirmToggleActiveCommand { get; private set; }
    public ICommand CancelToggleActiveCommand { get; private set; }

    public string SearchText
    {
        get => _search;
        set => SetField(ref _search, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetField(ref _statusMessage, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetField(ref _isBusy, value))
            {
                ((RelayCommand)SearchCommand).RaiseCanExecuteChanged();
                ((RelayCommand)ClearSelectionCommand).RaiseCanExecuteChanged();
                ((RelayCommand)NewUserCommand).RaiseCanExecuteChanged();
                ((RelayCommand)EditUserCommand).RaiseCanExecuteChanged();
                ((RelayCommand)EditUserRolesCommand).RaiseCanExecuteChanged();
                ((RelayCommand)ToggleActiveCommand).RaiseCanExecuteChanged();
                ((RelayCommand)ConfirmToggleActiveCommand).RaiseCanExecuteChanged();
                ((RelayCommand)CancelToggleActiveCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public AdministrativeUserDTO? SelectedUser
    {
        get => _selectedUser;
        set
        {
            if (!SetField(ref _selectedUser, value)) return;
            ClearUserContext();
            if (value is not null)
            {
                _ = LoadUserContextAsync(value.UserID);
            }
            OnPropertyChanged(nameof(ToggleActiveButtonText));
            ((RelayCommand)ClearSelectionCommand).RaiseCanExecuteChanged();
            ((RelayCommand)EditUserCommand).RaiseCanExecuteChanged();
            ((RelayCommand)EditUserRolesCommand).RaiseCanExecuteChanged();
            ((RelayCommand)ToggleActiveCommand).RaiseCanExecuteChanged();
        }
    }

    public AdministrativeRoleDTO? SelectedRole
    {
        get => _selectedRole;
        set
        {
            if (!SetField(ref _selectedRole, value)) return;
            _selectedModule = null;
            _selectedScreen = null;
            Modules.Clear();
            Screens.Clear();
            Permissions.Clear();
            OnPropertyChanged(nameof(HasSelectionSummary));
            if (value is not null)
            {
                _ = LoadRoleContextAsync(value.RoleID);
            }
        }
    }

    public string? SelectedModule
    {
        get => _selectedModule;
        set
        {
            if (!SetField(ref _selectedModule, value)) return;
            _selectedScreen = null;
            Screens.Clear();
            Permissions.Clear();
            if (value is not null)
            {
                var filtered = GetPermissionsForSelectedRole().Where(p => p.ModuleName.Equals(value, StringComparison.OrdinalIgnoreCase)).ToArray();
                foreach (var screen in filtered.Select(p => p.ScreenName).Distinct(StringComparer.OrdinalIgnoreCase))
                    Screens.Add(screen);
                if (Screens.Count > 0)
                    SelectedScreen = Screens.First();
            }
            OnPropertyChanged(nameof(HasSelectionSummary));
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
                var filtered = GetPermissionsForSelectedRole()
                    .Where(p => p.ModuleName.Equals(SelectedModule, StringComparison.OrdinalIgnoreCase) && p.ScreenName.Equals(value, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(p => p.Permission.PermissionCode, StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                foreach (var permission in filtered)
                    Permissions.Add(permission);
            }
            OnPropertyChanged(nameof(HasSelectionSummary));
        }
    }

    public bool HasSelectionSummary => SelectedUser is not null || SelectedRole is not null || SelectedModule is not null || SelectedScreen is not null;
    public bool ShowToggleUserConfirmation { get; private set; }
    public string ToggleActiveConfirmationMessage { get; private set; } = string.Empty;
    public string ToggleActiveButtonText => SelectedUser is not null && SelectedUser.IsActive ? "Desativar usuário" : "Ativar usuário";

    public event EventHandler? NewUserRequested;
    public event EventHandler? EditUserRequested;
    public event EventHandler? EditUserRolesRequested;
    public event PropertyChangedEventHandler? PropertyChanged;

    public async Task InitializeAsync() => await LoadUsersAsync();

    public async Task HandleUserCreatedAsync(string username)
    {
        await LoadUsersAsync();
        SelectedUser = Users.FirstOrDefault(user => user.Username.Equals(username, StringComparison.OrdinalIgnoreCase));
    }

    public async Task HandleUserUpdatedAsync(int userId)
    {
        await LoadUsersAsync();
        SelectedUser = Users.FirstOrDefault(user => user.UserID == userId);
    }

    public async Task HandleUserPermissionsUpdatedAsync(int userId, IAuthenticatedUserContext authenticatedUserContext)
    {
        if (authenticatedUserContext.IsAuthenticated)
        {
            await HandleUserUpdatedAsync(userId);
        }
    }

    private async Task LoadUsersAsync()
    {
        IsBusy = true;
        try
        {
            var values = await _userAdministrationService.SearchAsync(SearchText);
            Users.Clear();
            foreach (var user in values)
                Users.Add(user);
            StatusMessage = values.Count == 0 ? "Nenhum usuário encontrado." : $"{values.Count} usuário(s) carregado(s).";
            if (SelectedUser is not null && Users.All(user => user.UserID != SelectedUser.UserID))
            {
                SelectedUser = null;
            }
        }
        catch (Exception)
        {
            StatusMessage = "Não foi possível carregar os usuários.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadUserContextAsync(int userId)
    {
        IsBusy = true;
        try
        {
            var user = await _userAdministrationService.GetByIdAsync(userId);
            if (user is null)
            {
                StatusMessage = "Usuário não encontrado.";
                return;
            }

            UserRoles.Clear();
            foreach (var role in user.Roles.OrderBy(role => role.RoleName, StringComparer.OrdinalIgnoreCase))
                UserRoles.Add(role);

            if (UserRoles.Count == 0)
            {
                StatusMessage = $"{user.DisplayName} não possui grupos atribuídos.";
                return;
            }

            SelectedRole = UserRoles.First();
            var uniquePermissionCount = await GetUniquePermissionCountForRoles(user.Roles);
            StatusMessage = $"{user.DisplayName} · {UserRoles.Count} grupo(s) · {uniquePermissionCount} permissão(ões) disponíveis.";
        }
        catch (Exception)
        {
            StatusMessage = "Não foi possível carregar o contexto do usuário.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadRoleContextAsync(int roleId)
    {
        IsBusy = true;
        try
        {
            var permissions = await _roleAdministrationService.GetPermissionsAsync(roleId);
            var grantedIds = permissions.Select(item => item.PermissionID).ToHashSet();
            var allPermissions = await _roleAdministrationService.GetAllPermissionsAsync();

            _rolePermissionCatalog = allPermissions
                .Select(permission => new UserCascadePermissionItem
                {
                    Permission = permission,
                    IsGranted = grantedIds.Contains(permission.PermissionID)
                })
                .Where(permission => permission.ModuleName.Length > 0)
                .OrderBy(permission => permission.ModuleName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(permission => permission.ScreenName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(permission => permission.Permission.PermissionCode, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            Modules.Clear();
            foreach (var module in _rolePermissionCatalog.Select(item => item.ModuleName).Distinct(StringComparer.OrdinalIgnoreCase))
                Modules.Add(module);

            if (Modules.Count > 0)
            {
                SelectedModule = Modules.First();
            }
        }
        catch (Exception)
        {
            StatusMessage = "Não foi possível carregar os módulos do grupo.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private IReadOnlyList<UserCascadePermissionItem> GetPermissionsForSelectedRole() => _rolePermissionCatalog;

    private async Task<int> GetUniquePermissionCountForRoles(IEnumerable<AdministrativeRoleDTO> roles)
    {
        var ids = new HashSet<int>();
        foreach (var role in roles)
        {
            var permissions = await _roleAdministrationService.GetPermissionsAsync(role.RoleID);
            foreach (var permission in permissions)
                ids.Add(permission.PermissionID);
        }

        return ids.Count;
    }

    private async Task ToggleActiveAsync()
    {
        if (SelectedUser is null)
            return;

        if (SelectedUser.IsActive)
        {
            ToggleActiveConfirmationMessage = $"Deseja realmente desativar o usuário \"{SelectedUser.Username}\"?";
            ShowToggleUserConfirmation = true;
            return;
        }

        await ExecuteToggleActiveAsync(SelectedUser.UserID, true);
    }

    private async Task ConfirmToggleActiveAsync()
    {
        if (SelectedUser is null)
            return;

        await ExecuteToggleActiveAsync(SelectedUser.UserID, !SelectedUser.IsActive);
    }

    private void CancelToggleActive()
    {
        ShowToggleUserConfirmation = false;
        ToggleActiveConfirmationMessage = string.Empty;
    }

    private async Task ExecuteToggleActiveAsync(int userId, bool requestedActiveState)
    {
        IsBusy = true;
        try
        {
            await _userAdministrationService.SetActiveAsync(userId, requestedActiveState);
            StatusMessage = requestedActiveState ? "Usuário ativado com sucesso." : "Usuário desativado com sucesso.";
            await LoadUsersAsync();
            SelectedUser = Users.FirstOrDefault(user => user.UserID == userId);
            ShowToggleUserConfirmation = false;
            ToggleActiveConfirmationMessage = string.Empty;
        }
        catch (Exception)
        {
            StatusMessage = "Não foi possível atualizar o estado do usuário.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ClearSelection()
    {
        SelectedUser = null;
        UserRoles.Clear();
        Modules.Clear();
        Screens.Clear();
        Permissions.Clear();
        SelectedRole = null;
        StatusMessage = "Nenhum usuário selecionado.";
    }

    private void ClearUserContext()
    {
        UserRoles.Clear();
        SelectedRole = null;
        Modules.Clear();
        Screens.Clear();
        Permissions.Clear();
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

    public string? StatusPrimary => null;
    public string? StatusSecondary => null;
    public bool HasStatusPrimary => false;
    public bool HasStatusSecondary => false;
    public string? PaginationStatus => null;
    public string? PaginationPageStatus => null;
    public bool HasPagination => false;
    public ICommand? PreviousPageCommand => null;
    public ICommand? NextPageCommand => null;
    public ICommand? FirstPageCommand => null;
    public ICommand? LastPageCommand => null;
    public bool HasPreviousPage => false;
    public bool HasNextPage => false;
    public bool HasFirstPage => false;
    public bool HasLastPage => false;
    public IReadOnlyList<int> PageSizeOptions => [];
    public int SelectedPageSize { get => 0; set { } }

    private sealed class NoOpRoleAdministrationService : IRoleAdministrationService
    {
        public Task<IReadOnlyList<AdministrativeRoleDTO>> GetRolesAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AdministrativeRoleDTO>>([]);
        public Task<IReadOnlyList<AdministrativePermissionDTO>> GetAllPermissionsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AdministrativePermissionDTO>>([]);
        public Task<IReadOnlyList<AdministrativePermissionDTO>> GetPermissionsAsync(int roleId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AdministrativePermissionDTO>>([]);
        public Task ReplacePermissionsAsync(int roleId, IReadOnlyCollection<int> permissionIds, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RestoreDefaultsAsync(int roleId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
