using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Input;
using Decor.Core.Common;
using Decor.Core.DTOs;
using Decor.Core.Interfaces.Services;

namespace Decor.AvaloniaUI.ViewModels;

public sealed class ServicesViewModel : IStatusBarSource, IWorkspaceDocumentState
{
    private readonly IServiceCatalogService _catalog;
    private readonly IAuthorizationService _authorization;
    private ServiceDTO? _selectedService;
    private ServiceDTO? _serviceToDelete;
    private string _searchText = string.Empty;
    private string _statusMessage = string.Empty;
    private string _validationMessage = string.Empty;
    private bool _isBusy;
    private bool _isEditing;
    private bool _isNew;
    private bool _selectionMode;
    private bool _showDeleteConfirmation;
    private int _serviceId;
    private string _description = string.Empty;
    private bool _isActive = true;
    private decimal? _costPrice;
    private decimal? _salePrice;
    private decimal? _employeeCommissionValue;
    private string? _observations;

    public ServicesViewModel(IServiceCatalogService catalog, IAuthorizationService authorization)
    {
        _catalog = catalog;
        _authorization = authorization;
        Listing = new GridListState<ServiceDTO>(Services, () => !IsEditing);
        Listing.PropertyChanged += (_, args) => OnPropertyChanged(args.PropertyName);
        SearchCommand = new RelayCommand(async () => await LoadServicesAsync(), () => CanSearch);
        ClearSearchCommand = new RelayCommand(ClearSearch, () => CanSearch);
        NewCommand = new RelayCommand(BeginNew, () => CanNew);
        EditCommand = new RelayCommand(async () => await BeginEditAsync(), () => CanEdit);
        DeleteCommand = new RelayCommand(BeginDelete, () => CanDelete);
        ConfirmDeleteCommand = new RelayCommand(async () => await ConfirmDeleteAsync(), () => CanConfirmDelete);
        CancelDeleteCommand = new RelayCommand(CancelDelete, () => ShowDeleteConfirmation && !IsBusy);
        SaveCommand = new RelayCommand(async () => await SaveAsync(), () => CanSave);
        CancelCommand = new RelayCommand(CancelEdit, () => CanCancel);
    }

    public ObservableCollection<ServiceDTO> Services { get; } = [];
    public GridListState<ServiceDTO> Listing { get; }
    public ICommand SearchCommand { get; }
    public ICommand ClearSearchCommand { get; }
    public ICommand NewCommand { get; }
    public ICommand EditCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand ConfirmDeleteCommand { get; }
    public ICommand CancelDeleteCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }
    public string SearchText { get => _searchText; set => SetField(ref _searchText, value); }
    public string StatusMessage { get => _statusMessage; private set => SetField(ref _statusMessage, value); }
    public string ValidationMessage { get => _validationMessage; private set { SetField(ref _validationMessage, value); OnPropertyChanged(nameof(HasValidationMessage)); } }
    public bool HasValidationMessage => !string.IsNullOrEmpty(ValidationMessage);
    public bool IsBusy { get => _isBusy; private set { if (SetField(ref _isBusy, value)) RefreshCommands(); } }
    public bool IsEditing
    {
        get => _isEditing;
        private set
        {
            if (!SetField(ref _isEditing, value)) return;
            OnPropertyChanged(nameof(IsAdding));
            OnPropertyChanged(nameof(ServiceCodeDisplay));
            Listing.Refresh();
            RefreshCommands();
        }
    }
    public bool IsAdding => IsEditing && _isNew;
    public bool SelectionMode
    {
        get => _selectionMode;
        set
        {
            if (!SetField(ref _selectionMode, value)) return;
            if (value && IsEditing) CancelEdit();
            if (value && ShowDeleteConfirmation) CancelDelete();
            RefreshCommands();
        }
    }
    public int ServiceId { get => _serviceId; private set { SetField(ref _serviceId, value); OnPropertyChanged(nameof(ServiceCodeDisplay)); } }
    public string ServiceCodeDisplay => IsAdding ? "Novo" : RecordCodeDisplay.ForExistingRecord(ServiceId);
    public string Description { get => _description; set => SetField(ref _description, value); }
    public bool IsActive { get => _isActive; set => SetField(ref _isActive, value); }
    public decimal? CostPrice { get => _costPrice; set => SetField(ref _costPrice, value); }
    public decimal? SalePrice { get => _salePrice; set => SetField(ref _salePrice, value); }
    public decimal? EmployeeCommissionValue { get => _employeeCommissionValue; set => SetField(ref _employeeCommissionValue, value); }
    public string? Observations
    {
        get => _observations;
        set
        {
            if (SetField(ref _observations, value is null ? null : QuoteNotesRules.Truncate(value)))
                OnPropertyChanged(nameof(ObservationsCounter));
        }
    }
    public string ObservationsCounter => $"{Encoding.UTF8.GetByteCount(Observations ?? string.Empty)}/{QuoteNotesRules.MaximumBytes} bytes";
    public ServiceDTO? SelectedService { get => _selectedService; set { if (SetField(ref _selectedService, value)) RefreshCommands(); } }
    public bool ShowDeleteConfirmation { get => _showDeleteConfirmation; private set { if (SetField(ref _showDeleteConfirmation, value)) RefreshCommands(); } }
    public string DeleteConfirmationMessage => $"Excluir o servi\u00e7o \"{_serviceToDelete?.Description}\"?";
    private bool CanSearch => !IsBusy && !IsEditing && !ShowDeleteConfirmation && _authorization.HasPermission(DecorPermissions.ServicesView);
    public bool CanNew => !SelectionMode && CanSearch && _authorization.HasPermission(DecorPermissions.ServicesCreate);
    public bool CanEdit => !SelectionMode && CanSearch && SelectedService is not null && _authorization.HasPermission(DecorPermissions.ServicesEdit);
    public bool CanDelete => !SelectionMode && CanSearch && SelectedService is not null && _authorization.HasPermission(DecorPermissions.ServicesDelete);
    public bool CanSave => !SelectionMode && IsEditing && !IsBusy && _authorization.HasPermission(_isNew ? DecorPermissions.ServicesCreate : DecorPermissions.ServicesEdit);
    public bool CanCancel => IsEditing && !IsBusy;
    private bool CanConfirmDelete => ShowDeleteConfirmation && _serviceToDelete is not null && !IsBusy && _authorization.HasPermission(DecorPermissions.ServicesDelete);

    public string? StatusPrimary => null;
    public bool HasStatusPrimary => false;
    public string? StatusSecondary => Listing.StatusSecondary;
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

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task LoadServicesAsync(CancellationToken cancellationToken = default)
    {
        if (!CanSearch) return;
        await RunAsync(async () =>
        {
            await RefreshListingAsync(cancellationToken);
            StatusMessage = $"{Listing.TotalCount} servi\u00e7o(s).";
        }, "consultar", cancellationToken);
    }

    private async Task RefreshListingAsync(CancellationToken cancellationToken)
    {
        const int batchSize = 200;
        var results = new List<ServiceDTO>();
        var search = SearchText;
        for (var page = 1; ; page++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var batch = (await _catalog.SearchServicesAsync(search, page, batchSize, cancellationToken)).ToArray();
            results.AddRange(batch);
            if (batch.Length < batchSize) break;
        }
        cancellationToken.ThrowIfCancellationRequested();
        SelectedService = null;
        Listing.Load(results);
    }

    private void ClearSearch()
    {
        if (!CanSearch) return;
        SearchText = string.Empty;
        SelectedService = null;
        Listing.Clear();
        StatusMessage = "Pesquisa limpa.";
    }

    public void BeginNew()
    {
        if (!CanNew) return;
        ResetForm();
        _isNew = true;
        SelectedService = null;
        IsEditing = true;
        StatusMessage = "Cadastrando um servi\u00e7o.";
    }

    public async Task BeginEditAsync(CancellationToken cancellationToken = default)
    {
        if (!CanEdit) return;
        var id = SelectedService!.ServiceID;
        await RunAsync(async () =>
        {
            var service = await _catalog.GetServiceByIdAsync(id, cancellationToken);
            if (service is null) throw new InvalidOperationException();
            _isNew = false;
            ServiceId = service.ServiceID;
            Description = service.Description ?? string.Empty;
            IsActive = service.IsActive;
            CostPrice = service.CostPrice;
            SalePrice = service.SalePrice;
            EmployeeCommissionValue = service.EmployeeCommissionValue;
            Observations = service.Observations;
            ValidationMessage = string.Empty;
            IsEditing = true;
            StatusMessage = $"Editando o servi\u00e7o {ServiceId}.";
        }, "carregar", cancellationToken);
    }

    public void CancelEdit()
    {
        if (!CanCancel) return;
        IsEditing = false;
        _isNew = false;
        ResetForm();
        SelectedService = null;
        StatusMessage = string.Empty;
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        if (!CanSave) return;
        ValidationMessage = string.Empty;
        await RunAsync(async () =>
        {
            var dto = new ServiceDTO(ServiceID: ServiceId, Description: Description, IsActive: IsActive,
                CostPrice: CostPrice, SalePrice: SalePrice, EmployeeCommissionValue: EmployeeCommissionValue, Observations: Observations);
            await _catalog.SaveServiceAsync(dto, cancellationToken);
            IsEditing = false;
            _isNew = false;
            StatusMessage = "Servi\u00e7o salvo com sucesso.";
            await RefreshAfterMutationAsync(cancellationToken);
        }, "salvar", cancellationToken);
    }

    public void BeginDelete()
    {
        if (!CanDelete) return;
        ValidationMessage = string.Empty;
        _serviceToDelete = SelectedService;
        OnPropertyChanged(nameof(DeleteConfirmationMessage));
        ShowDeleteConfirmation = true;
    }

    public void CancelDelete()
    {
        if (IsBusy) return;
        _serviceToDelete = null;
        ShowDeleteConfirmation = false;
        OnPropertyChanged(nameof(DeleteConfirmationMessage));
    }

    public async Task ConfirmDeleteAsync(CancellationToken cancellationToken = default)
    {
        if (!CanConfirmDelete) return;
        ValidationMessage = string.Empty;
        var id = _serviceToDelete!.ServiceID;
        await RunAsync(async () =>
        {
            await _catalog.DeleteServiceAsync(id, cancellationToken);
            _serviceToDelete = null;
            ShowDeleteConfirmation = false;
            SelectedService = null;
            StatusMessage = "Servi\u00e7o exclu\u00eddo com sucesso.";
            await RefreshAfterMutationAsync(cancellationToken);
        }, "excluir", cancellationToken);
    }

    private async Task RefreshAfterMutationAsync(CancellationToken cancellationToken)
    {
        try { await RefreshListingAsync(cancellationToken); }
        catch (Exception) { Listing.Clear(); StatusMessage += " Pesquise novamente para atualizar a lista."; }
    }

    private async Task RunAsync(Func<Task> operation, string action, CancellationToken cancellationToken)
    {
        IsBusy = true;
        try { await operation(); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { StatusMessage = "Opera\u00e7\u00e3o cancelada."; }
        catch (ValidationException exception) { ValidationMessage = exception.Message; StatusMessage = IsEditing ? "Verifique os dados informados." : exception.Message; }
        catch (UnauthorizedAccessException) { StatusMessage = $"Voc\u00ea n\u00e3o possui permiss\u00e3o para {action} servi\u00e7os."; }
        catch (Exception) { StatusMessage = $"N\u00e3o foi poss\u00edvel {action} o servi\u00e7o."; }
        finally { IsBusy = false; }
    }

    private void ResetForm()
    {
        ServiceId = 0;
        Description = string.Empty;
        IsActive = true;
        CostPrice = SalePrice = EmployeeCommissionValue = 0m;
        Observations = null;
        ValidationMessage = string.Empty;
    }

    private void RefreshCommands()
    {
        foreach (var command in new[] { SearchCommand, ClearSearchCommand, NewCommand, EditCommand, DeleteCommand, ConfirmDeleteCommand, CancelDeleteCommand, SaveCommand, CancelCommand })
            ((RelayCommand)command).RaiseCanExecuteChanged();
        foreach (var property in new[] { nameof(CanNew), nameof(CanEdit), nameof(CanDelete), nameof(CanSave), nameof(CanCancel) })
            OnPropertyChanged(property);
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}