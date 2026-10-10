using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Decor.Core.Common;
using Decor.Core.Interfaces.Services;

namespace Decor.AvaloniaUI.ViewModels;

public abstract class ContactRegistrationViewModel<TDto> : IStatusBarSource, IWorkspaceDocumentState
    where TDto : class
{
    private readonly IAuthorizationService _authorizationService;
    private readonly Func<string, int, int, Task<IEnumerable<TDto>>> _search;
    private readonly Func<int, int, Task<IEnumerable<TDto>>> _getAll;
    private readonly Func<TDto, int> _getId;
    private readonly Func<TDto, bool> _getIsActive;
    private readonly Func<TDto, string?> _getName;
    private readonly Func<TDto, string?> _getDocument;
    private readonly Func<TDto, string?> _getPhone;
    private readonly Func<TDto, string?> _getEmail;
    private readonly Func<TDto, string?> _getAddress;
    private readonly Func<int, string?, string?, string?, string?, string?, bool, TDto> _createDto;
    private readonly Func<TDto, Task> _save;
    private readonly Func<int, Task> _delete;
    private readonly string _viewPermission;
    private readonly string _createPermission;
    private readonly string _editPermission;
    private readonly string _deletePermission;
    private readonly string _singular;
    private readonly string _plural;
    private readonly string _nameLabel;
    private readonly string _searchPlaceholder;
    private TDto? _selectedItem;
    private TDto? _itemToDelete;
    private string _searchText = string.Empty;
    private string _statusMessage = string.Empty;
    private string _errorMessage = string.Empty;
    private string _name = string.Empty;
    private string _document = string.Empty;
    private string _phone = string.Empty;
    private string _email = string.Empty;
    private string _address = string.Empty;
    private bool _isActive = true;
    private bool _isBusy;
    private bool _isEditing;
    private bool _isNew;
    private bool _showDeleteConfirmation;
    private int _recordId;
    private int _recordCount;

    protected ContactRegistrationViewModel(
        IAuthorizationService authorizationService,
        Func<string, int, int, Task<IEnumerable<TDto>>> search,
        Func<int, int, Task<IEnumerable<TDto>>> getAll,
        Func<TDto, int> getId,
        Func<TDto, bool> getIsActive,
        Func<TDto, string?> getName,
        Func<TDto, string?> getDocument,
        Func<TDto, string?> getPhone,
        Func<TDto, string?> getEmail,
        Func<TDto, string?> getAddress,
        Func<int, string?, string?, string?, string?, string?, bool, TDto> createDto,
        Func<TDto, Task> save,
        Func<int, Task> delete,
        string viewPermission,
        string createPermission,
        string editPermission,
        string deletePermission,
        string singular,
        string plural,
        string nameLabel,
        string searchPlaceholder)
    {
        _authorizationService = authorizationService;
        _search = search;
        _getAll = getAll;
        _getId = getId;
        _getIsActive = getIsActive;
        _getName = getName;
        _getDocument = getDocument;
        _getPhone = getPhone;
        _getEmail = getEmail;
        _getAddress = getAddress;
        _createDto = createDto;
        _save = save;
        _delete = delete;
        _viewPermission = viewPermission;
        _createPermission = createPermission;
        _editPermission = editPermission;
        _deletePermission = deletePermission;
        _singular = singular;
        _plural = plural;
        _nameLabel = nameLabel;
        _searchPlaceholder = searchPlaceholder;

        Listing = new GridListState<TDto>(Items, () => !IsEditing);
        Listing.PropertyChanged += (_, args) => OnPropertyChanged(args.PropertyName);
        SearchCommand = new RelayCommand(async () => await LoadAsync(), () => CanSearch);
        ClearSearchCommand = new RelayCommand(ClearSearch, () => CanSearch);
        NewCommand = new RelayCommand(async () => await BeginNewAsync(), () => CanNew);
        EditCommand = new RelayCommand(BeginEdit, () => CanEdit);
        DeleteCommand = new RelayCommand(BeginDelete, () => CanDelete);
        SaveCommand = new RelayCommand(async () => await SaveAsync(), () => CanSave);
        CancelCommand = new RelayCommand(CancelEdit, () => CanCancel);
        ConfirmDeleteCommand = new RelayCommand(async () => await ConfirmDeleteAsync(), () => CanConfirmDelete);
        CancelDeleteCommand = new RelayCommand(CancelDelete, () => true);
    }

    public ObservableCollection<TDto> Items { get; } = [];
    public GridListState<TDto> Listing { get; }
    public ICommand SearchCommand { get; }
    public ICommand ClearSearchCommand { get; }
    public ICommand NewCommand { get; }
    public ICommand EditCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand ConfirmDeleteCommand { get; }
    public ICommand CancelDeleteCommand { get; }

    public string SearchText { get => _searchText; set => SetField(ref _searchText, value); }
    public string StatusMessage { get => _statusMessage; private set => SetField(ref _statusMessage, value); }
    public string ErrorMessage { get => _errorMessage; private set { if (SetField(ref _errorMessage, value)) OnPropertyChanged(nameof(HasError)); } }
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public string Name { get => _name; set => SetField(ref _name, value); }
    public string Document { get => _document; set => SetField(ref _document, value); }
    public string Phone { get => _phone; set => SetField(ref _phone, value); }
    public string Email { get => _email; set => SetField(ref _email, value); }
    public string Address { get => _address; set => SetField(ref _address, value); }
    public bool IsActive { get => _isActive; set => SetField(ref _isActive, value); }
    public string NameLabel => _nameLabel;
    public string SearchPlaceholder => _searchPlaceholder;
    public string FormTitle => $"Informações do {_singular}";
    public string NewButtonText => $"Novo {_singular}";
    public string CodeDisplay => IsAdding
        ? RecordCodeDisplay.ForNewRecord(_recordCount)
        : RecordCodeDisplay.ForExistingRecord(RecordId);
    public string DeleteConfirmationMessage => _itemToDelete is null
        ? string.Empty
        : $"Excluir {_singular} \"{_getName(_itemToDelete)}\"?";

    public int RecordId
    {
        get => _recordId;
        private set
        {
            if (SetField(ref _recordId, value))
                OnPropertyChanged(nameof(CodeDisplay));
        }
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

    public bool IsEditing
    {
        get => _isEditing;
        private set
        {
            if (!SetField(ref _isEditing, value)) return;
            RefreshCommands();
            Listing.Refresh();
            OnPropertyChanged(nameof(IsAdding));
            OnPropertyChanged(nameof(CodeDisplay));
        }
    }

    public bool IsAdding => IsEditing && _isNew;
    public bool ShowDeleteConfirmation { get => _showDeleteConfirmation; private set { if (SetField(ref _showDeleteConfirmation, value)) RefreshCommands(); } }
    public bool CanSearch => _authorizationService.HasPermission(_viewPermission) && !IsBusy && !IsEditing;
    public bool CanNew => _authorizationService.HasPermission(_createPermission) && !IsBusy && !IsEditing;
    public bool CanEdit => _authorizationService.HasPermission(_editPermission) && SelectedItem is not null && !IsBusy && !IsEditing;
    public bool CanDelete => _authorizationService.HasPermission(_deletePermission) && SelectedItem is not null && !IsBusy && !IsEditing;
    public bool CanSave => IsEditing && !IsBusy && _authorizationService.HasPermission(_isNew ? _createPermission : _editPermission);
    private bool CanConfirmDelete => !IsBusy && ShowDeleteConfirmation && _itemToDelete is not null && _authorizationService.HasPermission(_deletePermission);
    public bool CanCancel => IsEditing && !IsBusy;

    public TDto? SelectedItem
    {
        get => _selectedItem;
        set
        {
            if (SetField(ref _selectedItem, value))
                RefreshCommands();
        }
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

    public Task InitializeAsync()
    {
        Listing.Clear();
        SelectedItem = null;
        StatusMessage = string.Empty;
        return Task.CompletedTask;
    }

    public async Task BeginNewAsync()
    {
        try
        {
            _recordCount = await RecordCodeDisplay.CountAllAsync(_getAll);
        }
        catch
        {
            _recordCount = Items.Count;
        }

        _isNew = true;
        RecordId = 0;
        Name = Document = Phone = Email = Address = string.Empty;
        IsActive = true;
        ErrorMessage = string.Empty;
        SelectedItem = null;
        IsEditing = true;
        StatusMessage = $"Cadastrando {_singular}.";
    }

    public void BeginEdit()
    {
        if (SelectedItem is null) return;
        _isNew = false;
        RecordId = _getId(SelectedItem);
        Name = _getName(SelectedItem) ?? string.Empty;
        Document = _getDocument(SelectedItem) ?? string.Empty;
        Phone = _getPhone(SelectedItem) ?? string.Empty;
        Email = _getEmail(SelectedItem) ?? string.Empty;
        Address = _getAddress(SelectedItem) ?? string.Empty;
        IsActive = _getIsActive(SelectedItem);
        ErrorMessage = string.Empty;
        IsEditing = true;
        StatusMessage = $"Editando {_singular} {RecordId}.";
    }

    public void CancelEdit()
    {
        IsEditing = false;
        _isNew = false;
        SelectedItem = null;
        ErrorMessage = string.Empty;
        StatusMessage = string.Empty;
    }

    public void BeginDelete()
    {
        if (SelectedItem is null) return;
        _itemToDelete = SelectedItem;
        ShowDeleteConfirmation = true;
        OnPropertyChanged(nameof(DeleteConfirmationMessage));
    }

    public void CancelDelete()
    {
        _itemToDelete = null;
        ShowDeleteConfirmation = false;
        OnPropertyChanged(nameof(DeleteConfirmationMessage));
    }

    public async Task ConfirmDeleteAsync()
    {
        if (!CanConfirmDelete) return;
        if (_itemToDelete is null) return;
        IsBusy = true;
        try
        {
            await _delete(_getId(_itemToDelete));
            StatusMessage = $"{_singular} excluído com sucesso.";
            CancelDelete();
            SelectedItem = null;
            await LoadAsync();
        }
        catch (UnauthorizedAccessException)
        {
            StatusMessage = $"Você não possui permissão para excluir {_plural}.";
        }
        catch (Exception)
        {
            StatusMessage = $"Não foi possível excluir {_singular}.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ClearSearch()
    {
        SearchText = string.Empty;
        SelectedItem = null;
        Listing.Clear();
        StatusMessage = "Pesquisa limpa.";
    }

    private async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            const int pageSize = 500;
            var all = new List<TDto>();
            var page = 1;
            IEnumerable<TDto> pageItems;
            do
            {
                pageItems = await _search(SearchText, page, pageSize);
                var results = pageItems.ToArray();
                all.AddRange(results);
                page++;
                if (results.Length < pageSize) break;
            } while (true);

            Listing.Load(all);
            if (!IsEditing)
                StatusMessage = all.Count == 0 ? $"Nenhum {_singular} encontrado." : $"{all.Count} {_plural}.";
        }
        catch (UnauthorizedAccessException)
        {
            StatusMessage = $"Você não possui permissão para consultar {_plural}.";
        }
        catch (Exception)
        {
            StatusMessage = $"Não foi possível carregar {_plural}.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task SaveAsync()
    {
        if (!CanSave) return;
        ErrorMessage = string.Empty;
        IsBusy = true;
        try
        {
            await _save(_createDto(RecordId, NullIfEmpty(Name), NullIfEmpty(Document), NullIfEmpty(Phone),
                NullIfEmpty(Email), NullIfEmpty(Address), IsActive));
            StatusMessage = _isNew ? $"{_singular} incluído com sucesso." : $"{_singular} atualizado com sucesso.";
            IsEditing = false;
            _isNew = false;
            await LoadAsync();
        }
        catch (System.ComponentModel.DataAnnotations.ValidationException exception)
        {
            ErrorMessage = exception.Message;
            StatusMessage = "Verifique os dados informados.";
        }
        catch (UnauthorizedAccessException)
        {
            ErrorMessage = $"Você não possui permissão para salvar {_plural}.";
        }
        catch (Exception)
        {
            ErrorMessage = $"Não foi possível salvar {_singular}.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void RefreshCommands()
    {
        foreach (var command in new[] { SearchCommand, ClearSearchCommand, NewCommand, EditCommand, DeleteCommand, SaveCommand, CancelCommand, ConfirmDeleteCommand })
            if (command is RelayCommand relayCommand) relayCommand.RaiseCanExecuteChanged();
        OnPropertyChanged(nameof(CanSearch));
        OnPropertyChanged(nameof(CanNew));
        OnPropertyChanged(nameof(CanEdit));
        OnPropertyChanged(nameof(CanDelete));
        OnPropertyChanged(nameof(CanSave));
        OnPropertyChanged(nameof(CanCancel));
    }

    private static string? NullIfEmpty(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}