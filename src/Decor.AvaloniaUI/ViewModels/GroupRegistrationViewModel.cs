using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Decor.Core.Common;
using Decor.Core.DTOs;
using Decor.Core.Interfaces.Services;

namespace Decor.AvaloniaUI.ViewModels;

public sealed class GroupRegistrationViewModel : INotifyPropertyChanged, IWorkspaceDocumentState, IStatusBarSource
{
    private readonly IRoleRegistrationService _service;
    private readonly IAuthenticatedUserContext _context;
    private readonly IAuthorizationService _authorization;
    private readonly HashSet<int> _editableRoleIds = [];
    private AdministrativeRoleDTO? _selectedRole;
    private string _name = string.Empty;
    private string? _description;
    private int _hierarchyLevel;
    private bool _isNew;
    private bool _isEditing;
    private bool _isBusy;
    private string? _errorMessage;

    public GroupRegistrationViewModel(IRoleRegistrationService service, IAuthenticatedUserContext context, IAuthorizationService authorization)
    {
        _service = service;
        _context = context;
        _authorization = authorization;
        NewCommand = new RelayCommand(New, () => CanCreate);
        EditCommand = new RelayCommand(() => IsEditing = true, () => CanEdit);
        CancelCommand = new RelayCommand(Cancel, () => IsEditing && !IsBusy);
        SaveCommand = new RelayCommand(async () => await SaveAsync(), () => CanSave);
        Listing = new GridListState<AdministrativeRoleDTO>(Roles, () => !IsEditing);
        Listing.PropertyChanged += (_, args) => OnPropertyChanged(args.PropertyName);
    }

    public ObservableCollection<AdministrativeRoleDTO> Roles { get; } = [];
    public GridListState<AdministrativeRoleDTO> Listing { get; }
    public ICommand NewCommand { get; }
    public ICommand EditCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand SaveCommand { get; }
    public event PropertyChangedEventHandler? PropertyChanged;

    public AdministrativeRoleDTO? SelectedRole
    {
        get => _selectedRole;
        set
        {
            if (!SetField(ref _selectedRole, value)) return;
            _isNew = false;
            IsEditing = false;
            Name = value?.RoleName ?? string.Empty;
            Description = value?.Description;
            HierarchyLevel = value?.HierarchyLevel ?? 0;
            ErrorMessage = null;
            RefreshCommands();
        }
    }

    public string Name { get => _name; set => SetField(ref _name, value); }
    public string? Description { get => _description; set => SetField(ref _description, value); }
    public int HierarchyLevel { get => _hierarchyLevel; set => SetField(ref _hierarchyLevel, value); }
    private bool HasEditPermission => _context.IsAuthenticated && _authorization.HasPermission(DecorPermissions.RolesEdit);
    public bool CanCreate => !IsBusy && !IsEditing && HasEditPermission;
    public bool CanEdit => !IsBusy && !IsEditing && HasEditPermission && SelectedRole is not null && _editableRoleIds.Contains(SelectedRole.RoleID);
    public bool CanSave => !IsBusy && IsEditing && HasEditPermission && (_isNew || SelectedRole is not null && _editableRoleIds.Contains(SelectedRole.RoleID));
    public bool IsEditing
    {
        get => _isEditing;
        private set
        {
            if (!SetField(ref _isEditing, value)) return;
            OnPropertyChanged(nameof(IsAdding));
            OnPropertyChanged(nameof(StatusMessage));
            Listing.Refresh();
            RefreshCommands();
        }
    }
    public bool IsAdding => IsEditing && _isNew;
    public string StatusMessage => !IsEditing ? string.Empty
        : IsAdding ? "Cadastrando um grupo." : $"Editando o grupo {SelectedRole!.RoleID}.";
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
    public bool IsBusy
    {
        get => _isBusy;
        private set { if (SetField(ref _isBusy, value)) { OnPropertyChanged(nameof(IsIdle)); RefreshCommands(); } }
    }
    public bool IsIdle => !IsBusy;
    public string? ErrorMessage
    {
        get => _errorMessage;
        private set { if (SetField(ref _errorMessage, value)) OnPropertyChanged(nameof(HasError)); }
    }
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public async Task InitializeAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var roles = await _service.GetRolesAsync();
            var editableIds = await _service.GetEditableRoleIdsAsync();
            _editableRoleIds.Clear();
            _editableRoleIds.UnionWith(editableIds);
            Listing.Load(roles);
            SelectedRole = Roles.FirstOrDefault();
        }
        catch (Exception exception) { ErrorMessage = Message(exception, "Não foi possível carregar os grupos."); }
        finally { IsBusy = false; }
    }

    private void New()
    {
        if (!CanCreate) return;
        SelectedRole = null;
        _isNew = true;
        IsEditing = true;
        Name = string.Empty;
        Description = null;
        HierarchyLevel = 0;
        ErrorMessage = null;
        RefreshCommands();
    }

    private void Cancel()
    {
        _isNew = false;
        Name = SelectedRole?.RoleName ?? string.Empty;
        Description = SelectedRole?.Description;
        HierarchyLevel = SelectedRole?.HierarchyLevel ?? 0;
        ErrorMessage = null;
        IsEditing = false;
    }

    public async Task SaveAsync()
    {
        if (!CanSave) return;
        var roleId = _isNew ? 0 : SelectedRole!.RoleID;
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var saved = await _service.SaveAsync(roleId, Name, Description, HierarchyLevel);
            var all = Listing.AllItems.ToList();
            var index = all.FindIndex(role => role.RoleID == saved.RoleID);
            if (index < 0) all.Add(saved);
            else all[index] = saved;
            Listing.Load(all, keepPage: true);
            _editableRoleIds.Add(saved.RoleID);
            SelectedRole = saved;
        }
        catch (Exception exception) { ErrorMessage = Message(exception, "Não foi possível salvar o grupo."); }
        finally { IsBusy = false; }
    }

    private static string Message(Exception exception, string fallback)
        => exception is ArgumentException or InvalidOperationException or UnauthorizedAccessException ? exception.Message : fallback;

    private void RefreshCommands()
    {
        OnPropertyChanged(nameof(CanCreate));
        OnPropertyChanged(nameof(CanEdit));
        OnPropertyChanged(nameof(CanSave));
        ((RelayCommand)NewCommand).RaiseCanExecuteChanged();
        ((RelayCommand)EditCommand).RaiseCanExecuteChanged();
        ((RelayCommand)CancelCommand).RaiseCanExecuteChanged();
        ((RelayCommand)SaveCommand).RaiseCanExecuteChanged();
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}