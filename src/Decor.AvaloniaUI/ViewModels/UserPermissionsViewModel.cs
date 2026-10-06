using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Decor.Core.Common;
using Decor.Core.DTOs;
using Decor.Core.Interfaces.Services;

namespace Decor.AvaloniaUI.ViewModels;

public sealed class UserPermissionOption : INotifyPropertyChanged
{
    private string _selection = "Herdar dos grupos";
    public static IReadOnlyList<string> Choices { get; } = ["Herdar dos grupos", "Permitir", "Negar"];
    public required AdministrativePermissionDTO Permission { get; init; }
    public bool CanCustomize { get; init; }
    public bool GrantedByGroups { get; init; }
    public DecorPermissionPresentation Presentation => DecorPermissionPresentationCatalog.Describe(Permission.PermissionCode);
    public string ModuleName => Presentation.ModuleName;
    public string FormName => Presentation.FormName;
    public string ActionName => Presentation.ActionName;
    public string InheritedAccess => GrantedByGroups ? "Permitido" : "Não permitido";
    public string EffectiveAccess => Selection == "Permitir" || (Selection == "Herdar dos grupos" && GrantedByGroups) ? "Permitido" : "Não permitido";
    public string Selection
    {
        get => _selection;
        set
        {
            if (_selection == value || !Choices.Contains(value)) return;
            _selection = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Selection)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(EffectiveAccess)));
        }
    }
    public event PropertyChangedEventHandler? PropertyChanged;
}

public sealed class UserPermissionsViewModel : INotifyPropertyChanged
{
    private readonly IUserAdministrationService _users;
    private readonly IRoleAdministrationService _roles;
    private readonly IAuthorizationService _authorization;
    private readonly IAuthenticatedUserContext _context;
    private AdministrativeUserDTO? _selectedUser;
    private string? _selectedModule;
    private string _searchText = string.Empty;
    private string _statusMessage = string.Empty;
    private bool _isBusy;
    private bool _isLoaded;
    private int _loadVersion;
    private IReadOnlyList<UserPermissionOption> _catalog = [];
    private IReadOnlyCollection<PermissionOverrideDTO> _unlistedOverrides = [];

    public UserPermissionsViewModel(IUserAdministrationService users, IRoleAdministrationService roles,
        IAuthorizationService authorization, IAuthenticatedUserContext context)
    {
        _users = users;
        _roles = roles;
        _authorization = authorization;
        _context = context;
        SearchCommand = new RelayCommand(async () => await InitializeAsync(), () => !IsBusy);
        ClearSearchCommand = new RelayCommand(ClearSearch, () => !IsBusy);
        SaveCommand = new RelayCommand(async () => await SaveAsync(), () => CanChange(DecorPermissions.UsersManagePermissions));
        RestoreCommand = new RelayCommand(async () => await RestoreAsync(), () => CanChange(DecorPermissions.UsersRestorePermissions));
        AssignGroupsCommand = new RelayCommand(() => AssignGroupsRequested?.Invoke(this, EventArgs.Empty), () => CanChange(DecorPermissions.UsersAssignRoles));
    }

    public ObservableCollection<AdministrativeUserDTO> Users { get; } = [];
    public ObservableCollection<string> Modules { get; } = [];
    public ObservableCollection<UserPermissionOption> Permissions { get; } = [];
    public ICommand SearchCommand { get; }
    public ICommand ClearSearchCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand RestoreCommand { get; }
    public ICommand AssignGroupsCommand { get; }
    public event EventHandler? AssignGroupsRequested;
    public event PropertyChangedEventHandler? PropertyChanged;
    public string SearchText { get => _searchText; set { _searchText = value; OnPropertyChanged(); } }
    public string StatusMessage { get => _statusMessage; private set { _statusMessage = value; OnPropertyChanged(); } }
    public bool IsBusy { get => _isBusy; private set { _isBusy = value; OnPropertyChanged(); RefreshCommands(); } }
    public string GroupNames => string.Join(", ", SelectedUser?.Roles.Select(role => role.RoleName) ?? []);
    public AdministrativeUserDTO? SelectedUser
    {
        get => _selectedUser;
        set
        {
            if (IsBusy || Equals(_selectedUser, value)) return;
            _selectedUser = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(GroupNames));
            _isLoaded = false;
            Permissions.Clear();
            Modules.Clear();
            _selectedModule = null;
            OnPropertyChanged(nameof(SelectedModule));
            RefreshCommands();
            if (value is not null) _ = LoadUserAsync(value.UserID, ++_loadVersion);
        }
    }
    public string? SelectedModule
    {
        get => _selectedModule;
        set
        {
            _selectedModule = value;
            OnPropertyChanged();
            Permissions.Clear();
            foreach (var permission in _catalog.Where(item => item.ModuleName == value)) Permissions.Add(permission);
        }
    }

    public async Task InitializeAsync()
    {
        IsBusy = true;
        try
        {
            var values = await _users.SearchAsync(SearchText);
            _selectedUser = null;
            _isLoaded = false;
            _catalog = [];
            Users.Clear();
            Permissions.Clear();
            Modules.Clear();
            _selectedModule = null;
            OnPropertyChanged(nameof(SelectedModule));
            OnPropertyChanged(nameof(SelectedUser));
            OnPropertyChanged(nameof(GroupNames));
            foreach (var user in values) Users.Add(user);
            StatusMessage = values.Count == 0 ? "Nenhum usuário encontrado." : string.Empty;
        }
        catch (Exception) { StatusMessage = "Não foi possível carregar os usuários."; }
        finally { IsBusy = false; }
    }

    public Task ReloadSelectedAsync() => SelectedUser is null ? Task.CompletedTask : LoadUserAsync(SelectedUser.UserID, ++_loadVersion);

    private void ClearSearch()
    {
        SearchText = string.Empty;
        SelectedUser = null;
        Users.Clear();
        StatusMessage = string.Empty;
    }

    private async Task LoadUserAsync(int userId, int version)
    {
        IsBusy = true;
        _isLoaded = false;
        StatusMessage = string.Empty;
        try
        {
            var user = await _users.GetByIdAsync(userId) ?? throw new InvalidOperationException("Usuário não encontrado.");
            var catalog = await _roles.GetAllPermissionsAsync();
            var inheritedIds = new HashSet<int>();
            foreach (var role in user.Roles)
                foreach (var permission in await _roles.GetPermissionsAsync(role.RoleID)) inheritedIds.Add(permission.PermissionID);
            if (version != _loadVersion || SelectedUser?.UserID != userId) return;
            _selectedUser = user;
            OnPropertyChanged(nameof(SelectedUser));
            OnPropertyChanged(nameof(GroupNames));
            _catalog = catalog.Select(permission => new UserPermissionOption
            {
                Permission = permission,
                CanCustomize = !user.IsSystemAdministrator && _authorization.HasPermission(DecorPermissions.UsersManagePermissions),
                GrantedByGroups = inheritedIds.Contains(permission.PermissionID),
                Selection = user.PermissionOverrides.FirstOrDefault(item => item.PermissionID == permission.PermissionID) is { } item
                    ? item.IsGranted ? "Permitir" : "Negar" : "Herdar dos grupos"
            }).OrderBy(item => item.ModuleName).ThenBy(item => item.FormName).ThenBy(item => item.ActionName).ToArray();
            var knownIds = catalog.Select(item => item.PermissionID).ToHashSet();
            _unlistedOverrides = user.PermissionOverrides.Where(item => !knownIds.Contains(item.PermissionID)).ToArray();
            Modules.Clear();
            foreach (var module in _catalog.Select(item => item.ModuleName).Distinct()) Modules.Add(module);
            SelectedModule = Modules.FirstOrDefault();
            _isLoaded = true;
        }
        catch (Exception) { StatusMessage = "Não foi possível carregar as permissões deste usuário."; }
        finally { if (version == _loadVersion) IsBusy = false; }
    }

    private async Task SaveAsync()
    {
        if (!CanChange(DecorPermissions.UsersManagePermissions)) return;
        var userId = SelectedUser!.UserID;
        var overrides = _catalog.Where(item => item.Selection != "Herdar dos grupos")
            .Select(item => new PermissionOverrideDTO(item.Permission.PermissionID, item.Permission.PermissionCode, item.Selection == "Permitir"))
            .Concat(_unlistedOverrides).ToArray();
        IsBusy = true;
        try
        {
            await _users.ReplacePermissionOverridesAsync(userId, overrides);
            StatusMessage = "Permissões personalizadas salvas.";
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or InvalidOperationException) { StatusMessage = exception.Message; }
        catch (Exception) { StatusMessage = "Não foi possível salvar as permissões."; }
        finally { IsBusy = false; }
    }

    private async Task RestoreAsync()
    {
        if (!CanChange(DecorPermissions.UsersRestorePermissions)) return;
        IsBusy = true;
        try
        {
            await _users.RestorePermissionsAsync(SelectedUser!.UserID);
            if (_context.IsAuthenticated) await ReloadSelectedAsync();
            StatusMessage = "Permissões herdadas dos grupos restauradas.";
        }
        catch (Exception) { StatusMessage = "Não foi possível restaurar as permissões."; }
        finally { IsBusy = false; }
    }

    private bool CanChange(string permission) => _context.IsAuthenticated && _isLoaded && !IsBusy && SelectedUser is not null && !SelectedUser.IsSystemAdministrator && _authorization.HasPermission(permission);
    private void RefreshCommands()
    {
        ((RelayCommand)SearchCommand).RaiseCanExecuteChanged();
        ((RelayCommand)ClearSearchCommand).RaiseCanExecuteChanged();
        ((RelayCommand)SaveCommand).RaiseCanExecuteChanged();
        ((RelayCommand)RestoreCommand).RaiseCanExecuteChanged();
        ((RelayCommand)AssignGroupsCommand).RaiseCanExecuteChanged();
    }
    private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}