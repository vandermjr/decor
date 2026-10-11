using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Decor.Core.Common;
using Decor.Core.DTOs;
using Decor.Core.Interfaces.Services;

namespace Decor.AvaloniaUI.ViewModels;

public sealed class BrandsViewModel : IStatusBarSource, IWorkspaceDocumentState
{
    private readonly IBrandService _brandService;
    private readonly IAuthorizationService? _authorizationService;
    private BrandDTO? _selectedBrand;
    private string _searchText = string.Empty;
    private string _statusMessage = string.Empty;
    private bool _isBusy;
    private bool _isEditing;
    private bool _isNew;
    private int _recordCount;
    private int _brandId;
    private string? _brandName;
    private readonly Dictionary<string, string> _fieldErrors = [];
    private bool _showDeleteConfirmation;
    private BrandDTO? _brandToDelete;

    public BrandsViewModel(IBrandService brandService, IAuthorizationService? authorizationService = null)
    {
        _brandService = brandService;
        _authorizationService = authorizationService;
        SearchCommand = new RelayCommand(async () => await LoadBrandsAsync(), () => CanSearch);
        ClearSearchCommand = new RelayCommand(ClearSearch, () => CanSearch);
        Listing = new GridListState<BrandDTO>(Brands, () => !IsEditing);
        Listing.PropertyChanged += (_, args) => OnPropertyChanged(args.PropertyName);
        NewCommand = new RelayCommand(async () => await BeginNewAsync(), () => CanNew);
        EditCommand = new RelayCommand(BeginEdit, () => CanEdit);
        DeleteCommand = new RelayCommand(BeginDelete, () => CanDelete);
        ConfirmDeleteCommand = new RelayCommand(async () => await ConfirmDeleteAsync(), () => CanConfirmDelete);
        CancelDeleteCommand = new RelayCommand(CancelDelete, () => true);
        SaveCommand = new RelayCommand(async () => await SaveAsync(), () => CanSave);
        CancelCommand = new RelayCommand(CancelEdit, () => IsEditing && !IsBusy);
    }

    public ObservableCollection<BrandDTO> Brands { get; } = [];
    public GridListState<BrandDTO> Listing { get; }

    public ICommand SearchCommand { get; }
    public ICommand ClearSearchCommand { get; }
    public ICommand NewCommand { get; }
    public ICommand EditCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand ConfirmDeleteCommand { get; }
    public ICommand CancelDeleteCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }

    public string SearchText
    {
        get => _searchText;
        set => SetField(ref _searchText, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetField(ref _statusMessage, value);
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

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetField(ref _isBusy, value))
            {
                RaiseCommandStates();
                NotifyCommandVisibilityChanged();
            }
        }
    }

    public bool IsEditing
    {
        get => _isEditing;
        private set
        {
            if (SetField(ref _isEditing, value))
            {
                RaiseCommandStates();
                NotifyCommandVisibilityChanged();
                Listing.Refresh();
                OnPropertyChanged(nameof(IsAdding));
                OnPropertyChanged(nameof(BrandCodeDisplay));
            }
        }
    }

    public bool IsAdding => IsEditing && _isNew;
    public string BrandCodeDisplay => IsAdding
        ? RecordCodeDisplay.ForNewRecord(_recordCount)
        : RecordCodeDisplay.ForExistingRecord(BrandId);

    private bool HasPermission(string permission) => _authorizationService?.HasPermission(permission) ?? true;
    public bool CanSearch => !IsBusy && !IsEditing && HasPermission(DecorPermissions.BrandsView);
    public bool CanNew => !IsBusy && !IsEditing && HasPermission(DecorPermissions.BrandsCreate);
    public bool CanEdit => SelectedBrand is not null && !IsBusy && !IsEditing && HasPermission(DecorPermissions.BrandsEdit);
    public bool CanDelete => SelectedBrand is not null && !IsBusy && !IsEditing && HasPermission(DecorPermissions.BrandsDelete);
    public bool CanSave => IsEditing && !IsBusy && HasPermission(_isNew ? DecorPermissions.BrandsCreate : DecorPermissions.BrandsEdit);
    private bool CanConfirmDelete => !IsBusy && ShowDeleteConfirmation && _brandToDelete is not null && HasPermission(DecorPermissions.BrandsDelete);
    public bool CanCancel => IsEditing && !IsBusy;

    public BrandDTO? SelectedBrand
    {
        get => _selectedBrand;
        set
        {
            if (SetField(ref _selectedBrand, value))
            {
                RaiseCommandStates();
                NotifyCommandVisibilityChanged();
            }
        }
    }

    public int BrandId
    {
        get => _brandId;
        private set
        {
            if (SetField(ref _brandId, value))
                OnPropertyChanged(nameof(BrandCodeDisplay));
        }
    }

    public string? BrandName
    {
        get => _brandName;
        set => SetField(ref _brandName, value);
    }

    // Propriedades de erro para cada campo
    public string BrandNameError => GetFieldError("BrandName");
    public bool HasBrandNameError => HasFieldError("BrandName");

    // Propriedades para diálogo de confirmação de delete
    public bool ShowDeleteConfirmation
    {
        get => _showDeleteConfirmation;
        private set { if (SetField(ref _showDeleteConfirmation, value)) RaiseCommandStates(); }
    }

    public BrandDTO? BrandToDelete => _brandToDelete;

    public string DeleteConfirmationMessage => BrandToDelete != null 
        ? $"Tem certeza que deseja excluir a marca \"{BrandToDelete.BrandName}\"?" 
        : string.Empty;

    public Task InitializeAsync()
    {
        Listing.Clear();
        SelectedBrand = null;
        StatusMessage = string.Empty;
        return Task.CompletedTask;
    }

    public async Task BeginNewAsync()
    {
        if (!HasPermission(DecorPermissions.BrandsCreate)) return;
        try
        {
            _recordCount = await RecordCodeDisplay.CountAllAsync(async (page, pageSize) =>
                await _brandService.GetAllBrandsAsync(page, pageSize));
        }
        catch
        {
            _recordCount = Brands.Count;
        }
        OnPropertyChanged(nameof(BrandCodeDisplay));
        _isNew = true;
        IsEditing = true;
        SelectedBrand = null;
        BrandId = 0;
        BrandName = string.Empty;
        ClearFieldErrors();
        StatusMessage = "Cadastrando uma marca.";
    }

    public void BeginEdit()
    {
        if (!HasPermission(DecorPermissions.BrandsEdit)) return;
        if (SelectedBrand is null)
        {
            return;
        }

        _isNew = false;
        IsEditing = true;
        BrandId = SelectedBrand.BrandID;
        BrandName = SelectedBrand.BrandName;
        ClearFieldErrors();
        StatusMessage = $"Editando a marca {BrandId}.";
    }

    public void CancelEdit()
    {
        IsEditing = false;
        _isNew = false;
        BrandId = 0;
        BrandName = string.Empty;
        ClearFieldErrors();
        SelectedBrand = null;
        StatusMessage = string.Empty;
    }

    private void ClearSearch()
    {
        SearchText = string.Empty;
        SelectedBrand = null;
        Listing.Clear();
        StatusMessage = "Pesquisa limpa.";
    }

    public void BeginDelete()
    {
        if (!CanDelete) return;
        if (SelectedBrand is null)
        {
            return;
        }

        _brandToDelete = SelectedBrand;
        ShowDeleteConfirmation = true;
        OnPropertyChanged(nameof(DeleteConfirmationMessage));
    }

    public void CancelDelete()
    {
        _brandToDelete = null;
        ShowDeleteConfirmation = false;
        OnPropertyChanged(nameof(DeleteConfirmationMessage));
    }

    public async Task ConfirmDeleteAsync()
    {
        if (!CanConfirmDelete) return;
        if (_brandToDelete is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await _brandService.DeleteBrandAsync(_brandToDelete.BrandID);
            StatusMessage = "Marca excluída com sucesso.";
            _brandToDelete = null;
            ShowDeleteConfirmation = false;
            SelectedBrand = null;
            await LoadBrandsAsync();
        }
        catch (UnauthorizedAccessException)
        {
            StatusMessage = "Você não possui permissão para excluir marcas.";
        }
        catch (Exception)
        {
            StatusMessage = "Não foi possível excluir a marca.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadBrandsAsync()
    {
        IsBusy = true;
        try
        {
            Listing.Load(await _brandService.SearchBrandsAsync(SearchText, pageSize: 500));
            OnPropertyChanged(nameof(BrandCodeDisplay));
            if (!IsEditing)
                StatusMessage = $"{Listing.TotalCount} marca(s).";
        }
        catch (UnauthorizedAccessException)
        {
            StatusMessage = "Você não possui permissão para consultar marcas.";
        }
        catch (Exception)
        {
            StatusMessage = "Não foi possível carregar as marcas.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task SaveAsync()
    {
        if (!CanSave) return;
        IsBusy = true;
        ClearFieldErrors();
        try
        {
            var dto = new BrandDTO(BrandId, BrandName);
            await _brandService.SaveBrandAsync(dto);
            StatusMessage = _isNew ? "Marca incluída com sucesso." : "Marca atualizada com sucesso.";
            IsEditing = false;
            _isNew = false;
            await LoadBrandsAsync();
        }
        catch (System.ComponentModel.DataAnnotations.ValidationException vex)
        {
            PopulateFieldErrors(vex.Message);
            StatusMessage = "Verifique os erros realçados nos campos abaixo.";
        }
        catch (UnauthorizedAccessException)
        {
            StatusMessage = "Você não possui permissão para salvar marcas.";
        }
        catch (Exception)
        {
            StatusMessage = "Não foi possível salvar a marca.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> source)
    {
        target.Clear();
        foreach (var item in source)
        {
            target.Add(item);
        }
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(name);
        return true;
    }

    private void RaiseCommandStates()
    {
        if (EditCommand is RelayCommand relayEdit) relayEdit.RaiseCanExecuteChanged();
        if (DeleteCommand is RelayCommand relayDelete) relayDelete.RaiseCanExecuteChanged();
        if (SaveCommand is RelayCommand relaySave) relaySave.RaiseCanExecuteChanged();
        if (CancelCommand is RelayCommand relayCancel) relayCancel.RaiseCanExecuteChanged();
        if (NewCommand is RelayCommand relayNew) relayNew.RaiseCanExecuteChanged();
        if (SearchCommand is RelayCommand relaySearch) relaySearch.RaiseCanExecuteChanged();
        if (ConfirmDeleteCommand is RelayCommand relayConfirm) relayConfirm.RaiseCanExecuteChanged();
        if (ClearSearchCommand is RelayCommand relayClear) relayClear.RaiseCanExecuteChanged();
    }

    private void NotifyCommandVisibilityChanged()
    {
        OnPropertyChanged(nameof(CanNew));
        OnPropertyChanged(nameof(CanEdit));
        OnPropertyChanged(nameof(CanDelete));
        OnPropertyChanged(nameof(CanSave));
        OnPropertyChanged(nameof(CanCancel));
    }

    public string GetFieldError(string fieldName) => _fieldErrors.TryGetValue(fieldName, out var error) ? error : string.Empty;

    public bool HasFieldError(string fieldName) => _fieldErrors.ContainsKey(fieldName);

    private void ClearFieldErrors()
    {
        _fieldErrors.Clear();
        NotifyErrorPropertiesChanged();
    }

    private void PopulateFieldErrors(string errorMessage)
    {
        var lines = errorMessage.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var fieldName = ExtractFieldName(line);
            if (!string.IsNullOrEmpty(fieldName))
            {
                _fieldErrors[fieldName] = line;
            }
        }
        NotifyErrorPropertiesChanged();
    }

    private void NotifyErrorPropertiesChanged()
    {
        OnPropertyChanged(nameof(BrandNameError));
        OnPropertyChanged(nameof(HasBrandNameError));
    }

    private static string ExtractFieldName(string errorMessage)
    {
        // Map error message keywords to field names
        if (errorMessage.Contains("nome", StringComparison.OrdinalIgnoreCase) || 
            errorMessage.Contains("marca", StringComparison.OrdinalIgnoreCase))
            return "BrandName";

        return string.Empty;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
