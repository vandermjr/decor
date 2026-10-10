using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Decor.Core.Common;
using Decor.Core.DTOs;
using Decor.Core.Interfaces.Services;

namespace Decor.AvaloniaUI.ViewModels;

public sealed class UnitsOfMeasureViewModel : IStatusBarSource, IWorkspaceDocumentState
{
    private readonly IUnitOfMeasureService _service;
    private readonly IAuthorizationService _authorization;
    private UnitOfMeasureDTO? _selectedUnit;
    private string _searchText = string.Empty;
    private string _statusMessage = string.Empty;
    private string _validationMessage = string.Empty;
    private string _code = string.Empty;
    private string _description = string.Empty;
    private bool _allowsFraction;
    private bool _isActive = true;
    private bool _isBusy;
    private bool _isEditing;
    private bool _isNew;
    private int _unitId;

    public UnitsOfMeasureViewModel(IUnitOfMeasureService service, IAuthorizationService authorization)
    {
        _service = service;
        _authorization = authorization;
        Listing = new GridListState<UnitOfMeasureDTO>(Units, () => !IsEditing);
        Listing.PropertyChanged += (_, args) => OnPropertyChanged(args.PropertyName);
        SearchCommand = new RelayCommand(async () => await LoadUnitsAsync(), () => CanSearch);
        ClearSearchCommand = new RelayCommand(ClearSearch, () => CanSearch);
        NewCommand = new RelayCommand(BeginNew, () => CanNew);
        EditCommand = new RelayCommand(async () => await BeginEditAsync(), () => CanEdit);
        SaveCommand = new RelayCommand(async () => await SaveAsync(), () => CanSave);
        CancelCommand = new RelayCommand(CancelEdit, () => CanCancel);
    }

    public ObservableCollection<UnitOfMeasureDTO> Units { get; } = [];
    public GridListState<UnitOfMeasureDTO> Listing { get; }
    public ICommand SearchCommand { get; }
    public ICommand ClearSearchCommand { get; }
    public ICommand NewCommand { get; }
    public ICommand EditCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }

    public string SearchText { get => _searchText; set => SetField(ref _searchText, value); }
    public string StatusMessage { get => _statusMessage; private set => SetField(ref _statusMessage, value); }
    public string ValidationMessage { get => _validationMessage; private set { if (SetField(ref _validationMessage, value)) OnPropertyChanged(nameof(HasValidationMessage)); } }
    public bool HasValidationMessage => !string.IsNullOrEmpty(ValidationMessage);
    public string Code { get => _code; set => SetField(ref _code, value); }
    public string Description { get => _description; set => SetField(ref _description, value); }
    public bool AllowsFraction { get => _allowsFraction; set => SetField(ref _allowsFraction, value); }
    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (SetField(ref _isActive, value))
                OnPropertyChanged(nameof(CanChangeActive));
        }
    }
    public bool CanChangeActive => CanSave && (!IsActive || HasPermission(DecorPermissions.UnitsOfMeasureDeactivate));
    public bool IsBusy { get => _isBusy; private set { if (SetField(ref _isBusy, value)) RefreshCommands(); } }
    public bool IsEditing
    {
        get => _isEditing;
        private set
        {
            if (!SetField(ref _isEditing, value)) return;
            OnPropertyChanged(nameof(IsAdding));
            OnPropertyChanged(nameof(UnitCodeDisplay));
            Listing.Refresh();
            RefreshCommands();
        }
    }
    public bool IsAdding => IsEditing && _isNew;
    public int UnitId { get => _unitId; private set { if (SetField(ref _unitId, value)) OnPropertyChanged(nameof(UnitCodeDisplay)); } }
    public string UnitCodeDisplay => IsAdding ? "Novo" : RecordCodeDisplay.ForExistingRecord(UnitId);
    public UnitOfMeasureDTO? SelectedUnit { get => _selectedUnit; set { if (SetField(ref _selectedUnit, value)) RefreshCommands(); } }
    public bool CanSearch => !IsBusy && !IsEditing && HasPermission(DecorPermissions.UnitsOfMeasureView);
    public bool CanNew => CanSearch && HasPermission(DecorPermissions.UnitsOfMeasureCreate);
    public bool CanEdit => CanSearch && SelectedUnit is not null && HasPermission(DecorPermissions.UnitsOfMeasureEdit);
    public bool CanSave => IsEditing && !IsBusy && HasPermission(_isNew ? DecorPermissions.UnitsOfMeasureCreate : DecorPermissions.UnitsOfMeasureEdit);
    public bool CanCancel => IsEditing && !IsBusy;

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

    public async Task InitializeAsync() => await LoadUnitsAsync();

    public async Task LoadUnitsAsync(CancellationToken cancellationToken = default)
    {
        if (!CanSearch) return;
        await RunAsync(async () =>
        {
            const int batchSize = 200;
            var results = new List<UnitOfMeasureDTO>();
            var search = SearchText;
            for (var page = 1; ; page++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var batch = (await _service.SearchAsync(search, page, batchSize, cancellationToken)).ToArray();
                results.AddRange(batch);
                if (batch.Length < batchSize) break;
            }
            SelectedUnit = null;
            Listing.Load(results);
            StatusMessage = $"{Listing.TotalCount} unidade(s) de medida.";
        }, "consultar", cancellationToken);
    }

    private void ClearSearch()
    {
        if (!CanSearch) return;
        SearchText = string.Empty;
        SelectedUnit = null;
        Listing.Clear();
        StatusMessage = "Pesquisa limpa.";
    }

    public void BeginNew()
    {
        if (!CanNew) return;
        ResetForm();
        _isNew = true;
        SelectedUnit = null;
        IsEditing = true;
        StatusMessage = "Cadastrando uma unidade de medida.";
    }

    public async Task BeginEditAsync(CancellationToken cancellationToken = default)
    {
        if (!CanEdit) return;
        var id = SelectedUnit!.UnitOfMeasureID;
        await RunAsync(async () =>
        {
            var unit = await _service.GetByIdAsync(id, cancellationToken);
            UnitId = unit.UnitOfMeasureID;
            Code = unit.Code;
            Description = unit.Description;
            AllowsFraction = unit.AllowsFraction;
            IsActive = unit.IsActive;
            _isNew = false;
            ValidationMessage = string.Empty;
            IsEditing = true;
            StatusMessage = $"Editando a unidade de medida {UnitId}.";
        }, "carregar", cancellationToken);
    }

    public void CancelEdit()
    {
        if (!CanCancel) return;
        IsEditing = false;
        _isNew = false;
        ResetForm();
        SelectedUnit = null;
        StatusMessage = string.Empty;
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        if (!CanSave) return;
        ValidationMessage = string.Empty;
        await RunAsync(async () =>
        {
            var dto = new UnitOfMeasureDTO(UnitId, Code.Trim(), Description.Trim(), AllowsFraction, IsActive);
            await _service.SaveUnitOfMeasureAsync(dto, cancellationToken);
            IsEditing = false;
            _isNew = false;
            StatusMessage = "Unidade de medida salva com sucesso.";
            await RefreshAfterMutationAsync(cancellationToken);
        }, "salvar", cancellationToken);
    }

    private async Task RefreshAfterMutationAsync(CancellationToken cancellationToken)
    {
        try
        {
            await ReloadListingAsync(cancellationToken);
        }
        catch (Exception)
        {
            Listing.Clear();
            StatusMessage += " Pesquise novamente para atualizar a lista.";
        }
    }

    private async Task ReloadListingAsync(CancellationToken cancellationToken)
    {
        const int batchSize = 200;
        var results = new List<UnitOfMeasureDTO>();
        for (var page = 1; ; page++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var batch = (await _service.SearchAsync(SearchText, page, batchSize, cancellationToken)).ToArray();
            results.AddRange(batch);
            if (batch.Length < batchSize) break;
        }
        Listing.Load(results);
    }

    private async Task RunAsync(Func<Task> operation, string action, CancellationToken cancellationToken)
    {
        IsBusy = true;
        try { await operation(); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { StatusMessage = "Operação cancelada."; }
        catch (ValidationException exception) { ValidationMessage = exception.Message; StatusMessage = "Verifique os dados informados."; }
        catch (UnauthorizedAccessException) { StatusMessage = $"Você não possui permissão para {action} unidades de medida."; }
        catch (Exception) { StatusMessage = $"Não foi possível {action} a unidade de medida."; }
        finally { IsBusy = false; }
    }

    private void ResetForm()
    {
        UnitId = 0;
        Code = string.Empty;
        Description = string.Empty;
        AllowsFraction = false;
        IsActive = true;
        ValidationMessage = string.Empty;
    }

    private bool HasPermission(string permission) => _authorization.HasPermission(permission);

    private void RefreshCommands()
    {
        foreach (var command in new[] { SearchCommand, ClearSearchCommand, NewCommand, EditCommand, SaveCommand, CancelCommand })
            ((RelayCommand)command).RaiseCanExecuteChanged();
        foreach (var property in new[] { nameof(CanNew), nameof(CanEdit), nameof(CanSave), nameof(CanCancel), nameof(CanChangeActive) })
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