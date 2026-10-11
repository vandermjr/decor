using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Decor.Core.Common;
using Decor.Core.DTOs;
using Decor.Core.Interfaces.Services;

namespace Decor.AvaloniaUI.ViewModels;

public sealed class UsersViewModel : IWorkspaceDocumentState, IStatusBarSource
{
    private readonly IUserAdministrationService _userAdministrationService;
    private readonly IRoleAdministrationService? _roleAdministrationService;
    private readonly IAuthorizationService? _authorization;
    private readonly IAuthenticatedUserContext? _context;
    private string _search = string.Empty;
    private string _statusMessage = string.Empty;
    private bool _isBusy;
    private AdministrativeUserDTO? _selectedUser;
    private UserFormViewModel? _activeForm;

    public UsersViewModel(IUserAdministrationService userAdministrationService,
        IRoleAdministrationService? roleAdministrationService = null,
        IAuthorizationService? authorization = null,
        IAuthenticatedUserContext? context = null)
    {
        _userAdministrationService = userAdministrationService;
        _roleAdministrationService = roleAdministrationService;
        _authorization = authorization;
        _context = context;
        SearchCommand = new RelayCommand(async () => await LoadUsersAsync(), () => CanSearch);
        ClearSearchCommand = new RelayCommand(ClearSearch, () => CanSearch);
        ClearSelectionCommand = new RelayCommand(ClearSelection, () => !IsEditing && SelectedUser is not null);
        NewUserCommand = new RelayCommand(() => FormLoadTask = OpenCreateFormAsync(), () => !IsEditing && !IsBusy && Allowed(DecorPermissions.UsersCreate));
        EditUserCommand = new RelayCommand(() => FormLoadTask = OpenEditFormAsync(), () => CanMutateSelected && Allowed(DecorPermissions.UsersEdit));
        EditUserRolesCommand = new RelayCommand(() => EditUserRolesRequested?.Invoke(this, EventArgs.Empty), () => CanMutateSelected && Allowed(DecorPermissions.UsersAssignRoles));
        Listing = new GridListState<AdministrativeUserDTO>(Users, () => !IsEditing);
        Listing.PropertyChanged += (_, args) => OnPropertyChanged(args.PropertyName);
    }

    public ObservableCollection<AdministrativeUserDTO> Users { get; } = [];
    public GridListState<AdministrativeUserDTO> Listing { get; }
    public UserFormViewModel? ActiveForm
    {
        get => _activeForm;
        private set
        {
            if (!SetField(ref _activeForm, value)) return;
            OnPropertyChanged(nameof(IsEditing));
            OnPropertyChanged(nameof(IsAdding));
            OnPropertyChanged(nameof(StatusMessage));
            Listing.Refresh();
            RefreshCommands();
        }
    }
    public bool IsEditing => ActiveForm is not null;
    public bool IsAdding => ActiveForm is { UserId: 0 };
    public Task FormLoadTask { get; private set; } = Task.CompletedTask;
    public Task FormCloseTask { get; private set; } = Task.CompletedTask;
    private bool CanMutateSelected => !IsEditing && !IsBusy && SelectedUser is { IsSystemAdministrator: false };
    private bool CanSearch => !IsEditing && !IsBusy && Allowed(DecorPermissions.UsersView);
    private bool Allowed(string permission) => (_context?.IsAuthenticated ?? true) && (_authorization?.HasPermission(permission) ?? true);

    public ICommand SearchCommand { get; private set; }
    public ICommand ClearSearchCommand { get; private set; }
    public ICommand ClearSelectionCommand { get; private set; }
    public ICommand NewUserCommand { get; private set; }
    public ICommand EditUserCommand { get; private set; }
    public ICommand EditUserRolesCommand { get; private set; }

    public string SearchText
    {
        get => _search;
        set => SetField(ref _search, value);
    }

    public string StatusMessage
    {
        get => ActiveForm is null ? _statusMessage
            : IsAdding ? "Cadastrando um usuário." : $"Editando o usuário {ActiveForm.UserId}.";
        private set => SetField(ref _statusMessage, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetField(ref _isBusy, value))
                RefreshCommands();
        }
    }

    public AdministrativeUserDTO? SelectedUser
    {
        get => _selectedUser;
        set
        {
            if (!SetField(ref _selectedUser, value)) return;
            RefreshCommands();
        }
    }

    public event EventHandler? NewUserRequested;
    public event EventHandler? EditUserRequested;
    public event EventHandler? EditUserRolesRequested;
    public event PropertyChangedEventHandler? PropertyChanged;

    private async Task OpenCreateFormAsync()
    {
        if (!NewUserCommand.CanExecute(null)) return;
        NewUserRequested?.Invoke(this, EventArgs.Empty);
        if (_roleAdministrationService is null) return;
        var form = new UserFormViewModel(_userAdministrationService, _roleAdministrationService, _authorization, _context, recordCount: Listing.TotalCount);
        form.CloseRequested += CloseForm;
        ActiveForm = form;
        await form.InitializeAsync();
    }

    private async Task OpenEditFormAsync()
    {
        if (!EditUserCommand.CanExecute(null) || SelectedUser is null) return;
        EditUserRequested?.Invoke(this, EventArgs.Empty);
        var form = new UserFormViewModel(_userAdministrationService, _roleAdministrationService, _authorization, _context, SelectedUser);
        form.CloseRequested += CloseForm;
        ActiveForm = form;
        await form.InitializeAsync();
    }

    private void CloseForm(object? sender, EventArgs args)
    {
        if (!ReferenceEquals(sender, ActiveForm) || ActiveForm is { IsBusy: true }) return;
        FormCloseTask = CloseFormAsync();
    }

    private async Task CloseFormAsync()
    {
        var form = ActiveForm;
        if (form is not null) form.CloseRequested -= CloseForm;
        ActiveForm = null;
        if (!(_context?.IsAuthenticated ?? true)) return;
        if (form is { CreatedUsername: not null } created)
            await HandleUserCreatedAsync(created.CreatedUsername);
        else if (form is { WasSaved: true } updated)
            await HandleUserUpdatedAsync(updated.UserId);
    }

    private void RefreshCommands()
    {
        foreach (var command in new[] { SearchCommand, ClearSearchCommand, ClearSelectionCommand, NewUserCommand, EditUserCommand, EditUserRolesCommand })
            ((RelayCommand?)command)?.RaiseCanExecuteChanged();
    }

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
        if (!Allowed(DecorPermissions.UsersView)) return;
        IsBusy = true;
        try
        {
            var values = await _userAdministrationService.SearchAsync(SearchText);
            Listing.Load(values);
            StatusMessage = values.Count == 0
                ? "Nenhum usuário encontrado."
                : $"{values.Count} {(values.Count == 1 ? "usuário carregado" : "usuários carregados")}.";
            if (SelectedUser is not null)
            {
                SelectedUser = Users.FirstOrDefault(user => user.UserID == SelectedUser.UserID);
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

    private void ClearSearch()
    {
        if (!CanSearch) return;
        SearchText = string.Empty;
        SelectedUser = null;
        Listing.Clear();
        StatusMessage = "Pesquisa limpa.";
    }

    private void ClearSelection()
    {
        SelectedUser = null;
        StatusMessage = "Nenhum usuário selecionado.";
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
    public string? StatusSecondary => Listing.StatusSecondary;
    public bool HasStatusPrimary => false;
    public bool HasStatusSecondary => Listing.HasStatusSecondary;
    public string? PaginationStatus => Listing.PaginationStatus;
    public string? PaginationPageStatus => Listing.PaginationPageStatus;
    public bool HasPagination => Listing.HasPagination;
    public ICommand? PreviousPageCommand => Listing.PreviousPageCommand;
    public ICommand? NextPageCommand => Listing.NextPageCommand;
    public ICommand? FirstPageCommand => Listing.FirstPageCommand;
    public ICommand? LastPageCommand => Listing.LastPageCommand;
    public bool HasPreviousPage => Listing.HasPreviousPage;
    public bool HasNextPage => Listing.HasNextPage;
    public bool HasFirstPage => Listing.HasFirstPage;
    public bool HasLastPage => Listing.HasLastPage;
    public IReadOnlyList<int> PageSizeOptions => Listing.PageSizeOptions;
    public int SelectedPageSize { get => Listing.SelectedPageSize; set => Listing.SelectedPageSize = value; }

    public void SetValueMatch(string columnName, int matchCount) => Listing.SetValueMatch(columnName, matchCount);
}
