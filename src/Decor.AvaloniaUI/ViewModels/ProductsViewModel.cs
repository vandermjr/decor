using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Decor.Core.DTOs;
using Decor.Core.Interfaces.Services;

namespace Decor.AvaloniaUI.ViewModels;

public sealed class ProductsViewModel : IStatusBarSource, IWorkspaceDocumentState
{
    private readonly IProductService _productService;
    private readonly IBrandService _brandService;
    private readonly IClassService _classService;
    private readonly IFamilyService _familyService;
    private readonly IGroupService _groupService;
    private readonly ISubgroupService _subgroupService;

    private ProductDTO? _selectedProduct;
    private string _searchText = string.Empty;
    private bool _isBusy;
    private bool _isEditing;
    private bool _isNew;
    private string _statusMessage = string.Empty;
    private int _productId;
    private string? _barcode, _description, _manufacturerRef, _auxiliarRef, _dimensions, _observations;
    private bool _isActive;
    private decimal _stockQuantity, _minimumStock;
    private BrandDTO? _selectedBrand;
    private ClassDTO? _selectedClass;
    private FamilyDTO? _selectedFamily;
    private GroupDTO? _selectedGroup;
    private SubgroupDTO? _selectedSubgroup;
    private readonly Dictionary<string, string> _fieldErrors = [];
    private string? _valueMatchColumn;
    private int _valueMatchCount;
    private Task? _lookupsLoadTask;
    private static readonly IReadOnlyList<int> AvailablePageSizes = [10, 25, 50, 100];
    private int _pageSize = 10;
    private int _currentPage = 1;
    private IReadOnlyList<ProductDTO> _allProducts = [];
    private bool _hasSearched;

    public ProductsViewModel(
        IProductService productService,
        IBrandService brandService,
        IClassService classService,
        IFamilyService familyService,
        IGroupService groupService,
        ISubgroupService subgroupService)
    {
        _productService = productService;
        _brandService = brandService;
        _classService = classService;
        _familyService = familyService;
        _groupService = groupService;
        _subgroupService = subgroupService;

        SearchCommand = new RelayCommand(async () => await SearchProductsAsync(), () => !IsBusy && !string.IsNullOrWhiteSpace(SearchText));
        PreviousPageCommand = new RelayCommand(() => ChangePage(-1), () => !IsBusy && !IsEditing && CurrentPage > 1);
        NextPageCommand = new RelayCommand(() => ChangePage(1), () => !IsBusy && !IsEditing && CurrentPage < TotalPages);
        FirstPageCommand = new RelayCommand(() => ChangePageTo(1), () => !IsBusy && !IsEditing && CurrentPage > 1);
        LastPageCommand = new RelayCommand(() => ChangePageTo(TotalPages), () => !IsBusy && !IsEditing && CurrentPage < TotalPages);
        NewCommand = new RelayCommand(async () => await BeginNewAsync(), () => !IsBusy && !IsEditing);
        EditCommand = new RelayCommand(async () => await BeginEditAsync(), () => SelectedProduct is not null && !IsBusy && !IsEditing);
        SaveCommand = new RelayCommand(async () => await SaveAsync(), () => IsEditing && !IsBusy);
        CancelCommand = new RelayCommand(CancelEdit, () => IsEditing && !IsBusy);
    }

    public ObservableCollection<ProductDTO> Products { get; } = [];
    public ObservableCollection<BrandDTO> Brands { get; } = [];
    public ObservableCollection<ClassDTO> Classes { get; } = [];
    public ObservableCollection<FamilyDTO> Families { get; } = [];
    public ObservableCollection<GroupDTO> Groups { get; } = [];
    public ObservableCollection<SubgroupDTO> Subgroups { get; } = [];

    public ICommand SearchCommand { get; }
    public ICommand NewCommand { get; }
    public ICommand EditCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand PreviousPageCommand { get; }
    public ICommand NextPageCommand { get; }
    public ICommand FirstPageCommand { get; }
    public ICommand LastPageCommand { get; }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetField(ref _searchText, value))
                RaiseCommandStates();
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetField(ref _isBusy, value))
            {
                RaiseCommandStates();
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
                OnPropertyChanged(nameof(IsListVisible));
                OnPropertyChanged(nameof(StatusPrimary));
                OnPropertyChanged(nameof(StatusSecondary));
                OnPropertyChanged(nameof(PaginationPageStatus));
                OnPropertyChanged(nameof(HasStatusPrimary));
                OnPropertyChanged(nameof(HasStatusSecondary));
            }
        }
    }

    public bool IsListVisible => !IsEditing;

    // Propriedades para visibilidade/estado de botões
    public bool CanNew => !IsBusy && !IsEditing;
    public bool CanEdit => SelectedProduct is not null && !IsBusy && !IsEditing;
    public bool CanSave => IsEditing && !IsBusy;
    public bool CanCancel => IsEditing && !IsBusy;

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetField(ref _statusMessage, value);
    }

    public string? StatusPrimary => null;
    public string? StatusSecondary => IsEditing || string.IsNullOrEmpty(_valueMatchColumn)
        ? null
        : $"{_valueMatchColumn}: {_valueMatchCount} correspondência(s)";
    public bool HasStatusPrimary => false;
    public bool HasStatusSecondary => !IsEditing && !string.IsNullOrEmpty(_valueMatchColumn);
    public int CurrentPage => _currentPage;
    public int TotalCount => _allProducts.Count;
    public int TotalPages => TotalCount == 0 ? 0 : (int)Math.Ceiling((double)TotalCount / _pageSize);
    public int FirstItem => TotalCount == 0 ? 0 : ((_currentPage - 1) * _pageSize) + 1;
    public int LastItem => Math.Min(_currentPage * _pageSize, TotalCount);
    public bool HasNextPage => CurrentPage < TotalPages;
    public bool HasPreviousPage => CurrentPage > 1;
    public bool HasFirstPage => HasPreviousPage;
    public bool HasLastPage => HasNextPage;
    public string? PaginationStatus => HasPagination ? $"{FirstItem} a {LastItem} de {TotalCount}" : null;
    public string? PaginationPageStatus => HasPagination ? $"Página {_currentPage} de {TotalPages}" : null;
    public bool HasPagination => _hasSearched && !IsEditing && Products.Count > 0;
    public IReadOnlyList<int> PageSizeOptions => AvailablePageSizes;
    public int SelectedPageSize
    {
        get => _pageSize;
        set
        {
            if (!AvailablePageSizes.Contains(value) || value == _pageSize)
                return;
            _pageSize = value;
            _currentPage = Math.Clamp(_currentPage, 1, TotalPages);
            RefreshPage();
        }
    }

    public void SetValueMatch(string columnName, int matchCount)
    {
        _valueMatchColumn = string.IsNullOrWhiteSpace(columnName) ? null : columnName;
        _valueMatchCount = matchCount;
        OnPropertyChanged(nameof(StatusSecondary));
        OnPropertyChanged(nameof(HasStatusSecondary));
    }

    private async Task SearchProductsAsync()
    {
        _currentPage = 1;
        _hasSearched = true;
        OnPropertyChanged(nameof(CurrentPage));
        OnPropertyChanged(nameof(HasPagination));
        await LoadProductsAsync();
    }

    private void ChangePage(int delta)
    {
        var requestedPage = _currentPage + delta;
        if (requestedPage < 1 || requestedPage > TotalPages)
            return;

        ChangePageTo(requestedPage);
    }

    private void ChangePageTo(int page)
    {
        if (page < 1 || page > TotalPages || page == _currentPage)
            return;
        _currentPage = page;
        RefreshPage();
    }

    public ProductDTO? SelectedProduct
    {
        get => _selectedProduct;
        set
        {
            if (SetField(ref _selectedProduct, value))
            {
                RaiseCommandStates();
            }
        }
    }

    public int ProductId
    {
        get => _productId;
        private set
        {
            if (SetField(ref _productId, value))
                OnPropertyChanged(nameof(ProductIdDisplay));
        }
    }

    public string ProductIdDisplay => ProductId == 0 ? "Gerado ao salvar" : ProductId.ToString();

    public string? Barcode
    {
        get => _barcode;
        set => SetField(ref _barcode, value);
    }

    public bool IsActive
    {
        get => _isActive;
        set => SetField(ref _isActive, value);
    }

    public string? Description
    {
        get => _description;
        set => SetField(ref _description, value);
    }

    public decimal StockQuantity
    {
        get => _stockQuantity;
        set => SetField(ref _stockQuantity, value);
    }

    public decimal MinimumStock
    {
        get => _minimumStock;
        set => SetField(ref _minimumStock, value);
    }

    public string? ManufacturerRef
    {
        get => _manufacturerRef;
        set => SetField(ref _manufacturerRef, value);
    }

    public string? AuxiliarRef
    {
        get => _auxiliarRef;
        set => SetField(ref _auxiliarRef, value);
    }

    public string? Dimensions
    {
        get => _dimensions;
        set => SetField(ref _dimensions, value);
    }

    public string? Observations
    {
        get => _observations;
        set => SetField(ref _observations, value);
    }

    public BrandDTO? SelectedBrand
    {
        get => _selectedBrand;
        set => SetField(ref _selectedBrand, value);
    }

    public ClassDTO? SelectedClass
    {
        get => _selectedClass;
        set
        {
            if (SetField(ref _selectedClass, value))
            {
                _ = LoadFamiliesAsync();
            }
        }
    }

    public FamilyDTO? SelectedFamily
    {
        get => _selectedFamily;
        set
        {
            if (SetField(ref _selectedFamily, value))
            {
                _ = LoadGroupsAsync();
            }
        }
    }

    public GroupDTO? SelectedGroup
    {
        get => _selectedGroup;
        set
        {
            if (SetField(ref _selectedGroup, value))
            {
                _ = LoadSubgroupsAsync();
            }
        }
    }

    public SubgroupDTO? SelectedSubgroup
    {
        get => _selectedSubgroup;
        set => SetField(ref _selectedSubgroup, value);
    }

    // Propriedades de erro para cada campo
    public string BarcodeError => GetFieldError("Barcode");
    public string DescriptionError => GetFieldError("Description");
    public string StockQuantityError => GetFieldError("StockQuantity");
    public string MinimumStockError => GetFieldError("MinimumStock");
    public string ManufacturerRefError => GetFieldError("ManufacturerRef");
    public string AuxiliarRefError => GetFieldError("AuxiliarRef");
    public string DimensionsError => GetFieldError("Dimensions");
    public string BrandError => GetFieldError("Brand");
    public string ObservationsError => GetFieldError("Observations");

    // Propriedades booleanas para indicar se há erro
    public bool HasBarcodeError => HasFieldError("Barcode");
    public bool HasDescriptionError => HasFieldError("Description");
    public bool HasStockQuantityError => HasFieldError("StockQuantity");
    public bool HasMinimumStockError => HasFieldError("MinimumStock");
    public bool HasManufacturerRefError => HasFieldError("ManufacturerRef");
    public bool HasAuxiliarRefError => HasFieldError("AuxiliarRef");
    public bool HasDimensionsError => HasFieldError("Dimensions");
    public bool HasBrandError => HasFieldError("Brand");
    public bool HasObservationsError => HasFieldError("Observations");

    public async Task InitializeAsync()
    {
        // A tela abre apenas com os cabeçalhos do grid.
        // Os produtos são carregados somente após uma pesquisa explícita.
        await EnsureLookupsAsync();
    }

    public async Task BeginNewAsync()
    {
        if (IsEditing)
        {
            return;
        }

        // O formulário exige marca e classificação. Aberturas pelo menu podem
        // acontecer enquanto a inicialização assíncrona da tela ainda está em curso.
        await EnsureLookupsAsync();

        if (Brands.Count == 0 || Classes.Count == 0)
        {
            StatusMessage = "Não foi possível carregar os dados necessários para incluir o produto.";
            return;
        }

        _isNew = true;
        IsEditing = true;
        SelectedProduct = null;
        ProductId = 0;
        Barcode = null;
        IsActive = true;
        Description = null;
        StockQuantity = 0;
        MinimumStock = 0;
        ManufacturerRef = null;
        AuxiliarRef = null;
        Dimensions = null;
        Observations = null;
        SelectedBrand = null;
        ResetClassificationSelection();
        ClearFieldErrors();
        StatusMessage = "Novo produto.";
    }

    public async Task BeginEditAsync()
    {
        if (SelectedProduct is null)
        {
            return;
        }

        await EnsureLookupsAsync();

        if (Brands.Count == 0 || Classes.Count == 0)
        {
            StatusMessage = "Não foi possível carregar os dados necessários para editar o produto.";
            return;
        }

        _isNew = false;
        IsEditing = true;
        ProductId = SelectedProduct.ProductID;
        Barcode = SelectedProduct.Barcode;
        IsActive = SelectedProduct.IsActive;
        Description = SelectedProduct.Description;
        StockQuantity = SelectedProduct.StockQuantity;
        MinimumStock = SelectedProduct.MinimumStock;
        ManufacturerRef = SelectedProduct.ManufacturerRef;
        AuxiliarRef = SelectedProduct.AuxiliarRef;
        Dimensions = SelectedProduct.Dimensions;
        Observations = SelectedProduct.Observations;
        SelectedBrand = Brands.FirstOrDefault(x => x.BrandID == SelectedProduct.BrandID);
        // Evita disparar um carregamento sem IDs e, em paralelo, outro com os IDs
        // do produto. Essa concorrência deixava as listas hierárquicas vazias.
        _selectedClass = Classes.FirstOrDefault(x => x.ClassID == SelectedProduct.ClassID);
        OnPropertyChanged(nameof(SelectedClass));

        if (_selectedClass is not null)
        {
            await LoadFamiliesAsync(SelectedProduct.FamilyID, SelectedProduct.GroupID, SelectedProduct.SubgroupID);
        }

        StatusMessage = $"Editando produto {ProductId}.";
    }

    public void CancelEdit()
    {
        IsEditing = false;
        _isNew = false;
        StatusMessage = string.Empty;
        ResetClassificationSelection();
        ClearFieldErrors();
    }

    private async Task LoadLookupsAsync()
    {
        IsBusy = true;
        try
        {
            var brandsTask = _brandService.GetAllBrandsAsync();
            var classesTask = _classService.GetAllAsync();
            await Task.WhenAll(brandsTask, classesTask);

            Replace(Brands, await brandsTask);
            Replace(Classes, await classesTask);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Erro ao carregar classificações: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task EnsureLookupsAsync()
    {
        if (Brands.Count > 0 && Classes.Count > 0)
        {
            return;
        }

        // Compartilha a mesma carga entre inicialização, Novo e Editar.
        // Assim nenhum fluxo tenta abrir o formulário durante uma carga parcial.
        if (_lookupsLoadTask is null || _lookupsLoadTask.IsCompleted)
        {
            _lookupsLoadTask = LoadLookupsAsync();
        }

        await _lookupsLoadTask;
    }

    private async Task LoadProductsAsync()
    {
        IsBusy = true;
        try
        {
            _allProducts = (await _productService.SearchProductsAsync(SearchText, 1, int.MaxValue)).ToArray();
            _currentPage = 1;
            RefreshPage();
            StatusMessage = "Pesquisa concluída.";
            SetValueMatch(string.Empty, 0);
        }
        catch (UnauthorizedAccessException)
        {
            StatusMessage = "Você não possui permissão para consultar produtos.";
        }
        catch (Exception)
        {
            StatusMessage = "Não foi possível carregar os produtos.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void RefreshPage()
    {
        var pageItems = _allProducts.Skip((_currentPage - 1) * _pageSize).Take(_pageSize);
        Replace(Products, pageItems);
        OnPropertyChanged(nameof(CurrentPage));
        OnPropertyChanged(nameof(TotalCount));
        OnPropertyChanged(nameof(TotalPages));
        OnPropertyChanged(nameof(FirstItem));
        OnPropertyChanged(nameof(LastItem));
        OnPropertyChanged(nameof(PaginationStatus));
        OnPropertyChanged(nameof(PaginationPageStatus));
        OnPropertyChanged(nameof(HasPagination));
        OnPropertyChanged(nameof(HasNextPage));
        OnPropertyChanged(nameof(HasPreviousPage));
        OnPropertyChanged(nameof(HasFirstPage));
        OnPropertyChanged(nameof(HasLastPage));
        RaisePaginationCommandStates();
    }

    private async Task LoadFamiliesAsync(int? familyId = null, int? groupId = null, int? subgroupId = null)
    {
        Families.Clear();
        Groups.Clear();
        Subgroups.Clear();

        if (SelectedClass is null)
        {
            return;
        }

        try
        {
            Replace(Families, await _familyService.GetByClassIdAsync(SelectedClass.ClassID));

            if (familyId.HasValue)
            {
                _selectedFamily = Families.FirstOrDefault(x => x.FamilyID == familyId);
                OnPropertyChanged(nameof(SelectedFamily));
            }

            if (_selectedFamily is not null && groupId.HasValue)
            {
                await LoadGroupsAsync(groupId, subgroupId);
            }
        }
        catch (Exception)
        {
            StatusMessage = "Não foi possível carregar as famílias.";
        }
    }

    private async Task LoadGroupsAsync(int? groupId = null, int? subgroupId = null)
    {
        Groups.Clear();
        Subgroups.Clear();

        if (SelectedFamily is null)
        {
            return;
        }

        try
        {
            Replace(Groups, await _groupService.GetByFamilyIdAsync(SelectedFamily.FamilyID));

            if (groupId.HasValue)
            {
                _selectedGroup = Groups.FirstOrDefault(x => x.GroupID == groupId);
                OnPropertyChanged(nameof(SelectedGroup));
            }

            if (_selectedGroup is not null && subgroupId.HasValue)
            {
                await LoadSubgroupsAsync(subgroupId);
            }
        }
        catch (Exception)
        {
            StatusMessage = "Não foi possível carregar os grupos.";
        }
    }

    private async Task LoadSubgroupsAsync(int? subgroupId = null)
    {
        Subgroups.Clear();

        if (SelectedGroup is null)
        {
            return;
        }

        try
        {
            Replace(Subgroups, await _subgroupService.GetByGroupIdAsync(SelectedGroup.GroupID));

            if (subgroupId.HasValue)
            {
                SelectedSubgroup = Subgroups.FirstOrDefault(x => x.SubgroupID == subgroupId);
            }
        }
        catch (Exception)
        {
            StatusMessage = "Não foi possível carregar os subgrupos.";
        }
    }

    private void ResetClassificationSelection()
    {
        _selectedClass = null;
        _selectedFamily = null;
        _selectedGroup = null;
        _selectedSubgroup = null;
        Families.Clear();
        Groups.Clear();
        Subgroups.Clear();
        OnPropertyChanged(nameof(SelectedClass));
        OnPropertyChanged(nameof(SelectedFamily));
        OnPropertyChanged(nameof(SelectedGroup));
        OnPropertyChanged(nameof(SelectedSubgroup));
    }

    private async Task SaveAsync()
    {
        IsBusy = true;
        ClearFieldErrors();
        try
        {
            var dto = new ProductDTO(
                ProductId,
                Barcode,
                IsActive,
                Description,
                StockQuantity,
                SelectedBrand?.BrandID,
                SelectedBrand?.BrandName,
                SelectedSubgroup?.SubgroupID,
                SelectedSubgroup?.SubgroupName,
                SelectedGroup?.GroupID,
                SelectedGroup?.GroupName,
                SelectedFamily?.FamilyID,
                SelectedFamily?.FamilyName,
                SelectedClass?.ClassID,
                SelectedClass?.ClassName,
                ManufacturerRef,
                AuxiliarRef,
                Dimensions,
                Observations,
                MinimumStock);

            await _productService.SaveProductAsync(dto);
            StatusMessage = _isNew ? "Produto incluído com sucesso." : "Produto atualizado com sucesso.";
            IsEditing = false;
            _isNew = false;
            await LoadProductsAsync();
        }
        catch (System.ComponentModel.DataAnnotations.ValidationException vex)
        {
            PopulateFieldErrors(vex.Message);
            StatusMessage = "Verifique os erros realçados nos campos abaixo.";
        }
        catch (UnauthorizedAccessException)
        {
            StatusMessage = "Você não possui permissão para salvar produtos.";
        }
        catch (Exception)
        {
            StatusMessage = "Não foi possível salvar o produto.";
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
        if (SearchCommand is RelayCommand relaySearch) relaySearch.RaiseCanExecuteChanged();
        if (EditCommand is RelayCommand relayEdit) relayEdit.RaiseCanExecuteChanged();
        if (SaveCommand is RelayCommand relaySave) relaySave.RaiseCanExecuteChanged();
        if (CancelCommand is RelayCommand relayCancel) relayCancel.RaiseCanExecuteChanged();
        if (NewCommand is RelayCommand relayNew) relayNew.RaiseCanExecuteChanged();
        RaisePaginationCommandStates();
        
        // Notify button visibility properties
        OnPropertyChanged(nameof(CanNew));
        OnPropertyChanged(nameof(CanEdit));
        OnPropertyChanged(nameof(CanSave));
        OnPropertyChanged(nameof(CanCancel));
        OnPropertyChanged(nameof(HasPagination));
    }

    private void RaisePaginationCommandStates()
    {
        if (PreviousPageCommand is RelayCommand previous) previous.RaiseCanExecuteChanged();
        if (NextPageCommand is RelayCommand next) next.RaiseCanExecuteChanged();
        if (FirstPageCommand is RelayCommand first) first.RaiseCanExecuteChanged();
        if (LastPageCommand is RelayCommand last) last.RaiseCanExecuteChanged();
    }

    public string GetFieldError(string fieldName) => _fieldErrors.TryGetValue(fieldName, out var error) ? error : string.Empty;

    public bool HasFieldError(string fieldName) => _fieldErrors.ContainsKey(fieldName);

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
        // Notify about all error properties after populating
        NotifyErrorPropertiesChanged();
    }

    private void ClearFieldErrors()
    {
        _fieldErrors.Clear();
        NotifyErrorPropertiesChanged();
    }

    private void NotifyErrorPropertiesChanged()
    {
        OnPropertyChanged(nameof(BarcodeError));
        OnPropertyChanged(nameof(DescriptionError));
        OnPropertyChanged(nameof(StockQuantityError));
        OnPropertyChanged(nameof(MinimumStockError));
        OnPropertyChanged(nameof(ManufacturerRefError));
        OnPropertyChanged(nameof(AuxiliarRefError));
        OnPropertyChanged(nameof(DimensionsError));
        OnPropertyChanged(nameof(BrandError));
        OnPropertyChanged(nameof(ObservationsError));
        OnPropertyChanged(nameof(HasBarcodeError));
        OnPropertyChanged(nameof(HasDescriptionError));
        OnPropertyChanged(nameof(HasStockQuantityError));
        OnPropertyChanged(nameof(HasMinimumStockError));
        OnPropertyChanged(nameof(HasManufacturerRefError));
        OnPropertyChanged(nameof(HasAuxiliarRefError));
        OnPropertyChanged(nameof(HasDimensionsError));
        OnPropertyChanged(nameof(HasBrandError));
        OnPropertyChanged(nameof(HasObservationsError));
    }

    private static string ExtractFieldName(string errorMessage)
    {
        // Map error message keywords to field names
        if (errorMessage.Contains("Código de Barras", StringComparison.OrdinalIgnoreCase))
            return "Barcode";
        if (errorMessage.Contains("descrição", StringComparison.OrdinalIgnoreCase))
            return "Description";
        if (errorMessage.Contains("Estoque Físico", StringComparison.OrdinalIgnoreCase))
            return "StockQuantity";
        if (errorMessage.Contains("Estoque Mínimo", StringComparison.OrdinalIgnoreCase))
            return "MinimumStock";
        if (errorMessage.Contains("Marca", StringComparison.OrdinalIgnoreCase))
            return "Brand";
        if (errorMessage.Contains("Fabricante", StringComparison.OrdinalIgnoreCase))
            return "ManufacturerRef";
        if (errorMessage.Contains("Auxiliar", StringComparison.OrdinalIgnoreCase))
            return "AuxiliarRef";
        if (errorMessage.Contains("dimensões", StringComparison.OrdinalIgnoreCase))
            return "Dimensions";
        if (errorMessage.Contains("observações", StringComparison.OrdinalIgnoreCase))
            return "Observations";

        return string.Empty;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
