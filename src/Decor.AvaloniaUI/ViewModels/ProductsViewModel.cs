using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Input;
using Decor.Core.Common;
using Decor.Core.DTOs;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Services;
using Microsoft.Extensions.Logging;

namespace Decor.AvaloniaUI.ViewModels;

public sealed class ProductsViewModel : IStatusBarSource, IWorkspaceDocumentState
{
    private static readonly CultureInfo BrazilianCulture = CultureInfo.GetCultureInfo("pt-BR");
    private readonly IProductService _productService;
    private readonly IBrandService _brandService;
    private readonly IClassService _classService;
    private readonly IFamilyService _familyService;
    private readonly IGroupService _groupService;
    private readonly ISubgroupService _subgroupService;
    private readonly IUnitOfMeasureService? _unitOfMeasureService;
    private readonly IAuthorizationService? _authorizationService;
    private readonly ILogger<ProductsViewModel>? _logger;

    private ProductDTO? _selectedProduct;
    private string _searchText = string.Empty;
    private bool _isBusy;
    private bool _isEditing;
    private bool _isNew;
    private bool _selectionMode;
    private string _statusMessage = string.Empty;
    private int _productId;
    private string? _barcode, _productName, _manufacturerRef, _auxiliarRef, _dimensions, _observations;
    private bool _isActive;
    private ProductAcquisitionMode _acquisitionMode = ProductAcquisitionMode.Estocado;
    private decimal _stockQuantity, _minimumStock;
    private BrandDTO? _selectedBrand;
    private ClassDTO? _selectedClass;
    private FamilyDTO? _selectedFamily;
    private GroupDTO? _selectedGroup;
    private SubgroupDTO? _selectedSubgroup;
    private UnitOfMeasureDTO? _selectedStockUnit;
    private readonly Dictionary<string, string> _fieldErrors = [];
    private Task? _lookupsLoadTask;
    private int _recordCount;
    private ProductCatalogSummaryDTO? _catalogSummary;

    public ProductsViewModel(
        IProductService productService,
        IBrandService brandService,
        IClassService classService,
        IFamilyService familyService,
        IGroupService groupService,
        ISubgroupService subgroupService,
        IAuthorizationService? authorizationService = null,
        IUnitOfMeasureService? unitOfMeasureService = null,
        ILogger<ProductsViewModel>? logger = null)
    {
        _productService = productService;
        _brandService = brandService;
        _classService = classService;
        _familyService = familyService;
        _groupService = groupService;
        _subgroupService = subgroupService;
        _unitOfMeasureService = unitOfMeasureService;
        _authorizationService = authorizationService;
        _logger = logger;

        SearchCommand = new RelayCommand(async () => await SearchProductsAsync(), () => CanSearch);
        ClearSearchCommand = new RelayCommand(ClearSearch, () => CanSearch);
        Listing = new GridListState<ProductDTO>(Products, () => !IsEditing);
        Listing.PropertyChanged += (_, args) => OnPropertyChanged(args.PropertyName);
        NewCommand = new RelayCommand(async () => await BeginNewAsync(), () => CanNew);
        EditCommand = new RelayCommand(async () => await BeginEditAsync(), () => CanEdit);
        SaveCommand = new RelayCommand(async () => await SaveAsync(), () => CanSave);
        CancelCommand = new RelayCommand(CancelEdit, () => IsEditing && !IsBusy);
    }

    public ObservableCollection<ProductDTO> Products { get; } = [];
    public ObservableCollection<BrandDTO> Brands { get; } = [];
    public ObservableCollection<ClassDTO> Classes { get; } = [];
    public ObservableCollection<FamilyDTO> Families { get; } = [];
    public ObservableCollection<GroupDTO> Groups { get; } = [];
    public ObservableCollection<SubgroupDTO> Subgroups { get; } = [];
    public ObservableCollection<UnitOfMeasureDTO> StockUnits { get; } = [];

    public ICommand SearchCommand { get; }
    public ICommand ClearSearchCommand { get; }
    public ICommand NewCommand { get; }
    public ICommand EditCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }
    public GridListState<ProductDTO> Listing { get; }
    public ICommand PreviousPageCommand => Listing.PreviousPageCommand;
    public ICommand NextPageCommand => Listing.NextPageCommand;
    public ICommand FirstPageCommand => Listing.FirstPageCommand;
    public ICommand LastPageCommand => Listing.LastPageCommand;

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
                OnPropertyChanged(nameof(HasStatusPrimary));
                Listing.Refresh();
                OnPropertyChanged(nameof(IsAdding));
                OnPropertyChanged(nameof(ProductCodeDisplay));
            }
        }
    }

    public bool IsListVisible => !IsEditing;
    public bool IsAdding => IsEditing && _isNew;
    public bool SelectionMode
    {
        get => _selectionMode;
        set
        {
            if (!SetField(ref _selectionMode, value)) return;
            if (value && IsEditing) CancelEdit();
            RaiseCommandStates();
        }
    }

    // Propriedades para visibilidade/estado de botões
    public bool CanSearch => !IsBusy && !IsEditing && (_authorizationService?.CanView("Products") ?? true);
    public bool CanNew => !SelectionMode && !IsBusy && !IsEditing && (_authorizationService?.CanCreate("Products") ?? true);
    public bool CanEdit => !SelectionMode && SelectedProduct is not null && !IsBusy && !IsEditing && (_authorizationService?.CanEdit("Products") ?? true);
    public bool CanSave => !SelectionMode && IsEditing && !IsBusy && (_isNew
        ? _authorizationService?.CanCreate("Products") ?? true
        : _authorizationService?.CanEdit("Products") ?? true);
    public bool CanCancel => IsEditing && !IsBusy;

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetField(ref _statusMessage, value);
    }

    public void ReportProductsExported(string filePath) => StatusMessage = $"Arquivo aberto para salvar como: {Path.GetFileName(filePath)}";
    public void ReportProductsExportFailure() => StatusMessage = "Não foi possível exportar os produtos.";

    public string? StatusPrimary => null;
    public string? StatusSecondary => Listing.StatusSecondary;
    public bool HasStatusPrimary => false;
    public bool HasStatusSecondary => Listing.HasStatusSecondary;
    public int CurrentPage => Listing.CurrentPage;
    public int TotalCount => Listing.TotalCount;
    public int TotalPages => Listing.TotalPages;
    public bool HasNextPage => Listing.HasNextPage;
    public bool HasPreviousPage => Listing.HasPreviousPage;
    public bool HasFirstPage => Listing.HasFirstPage;
    public bool HasLastPage => Listing.HasLastPage;
    public string? PaginationStatus => Listing.PaginationStatus;
    public string? PaginationPageStatus => Listing.PaginationPageStatus;
    public bool HasPagination => Listing.HasPagination;
    public IReadOnlyList<int> PageSizeOptions => Listing.PageSizeOptions;
    public int SelectedPageSize
    {
        get => Listing.SelectedPageSize;
        set => Listing.SelectedPageSize = value;
    }

    public void SetValueMatch(string columnName, int matchCount) => Listing.SetValueMatch(columnName, matchCount);

    private Task SearchProductsAsync() => LoadProductsAsync();

    private void ClearSearch()
    {
        SearchText = string.Empty;
        SelectedProduct = null;
        Listing.Clear();
        OnPropertyChanged(nameof(CanExport));
        StatusMessage = "Pesquisa limpa.";
    }

    public ProductDTO? SelectedProduct
    {
        get => _selectedProduct;
        set
        {
            if (SetField(ref _selectedProduct, value))
            {
                OnPropertyChanged(nameof(SelectedBarcodeDisplay));
                OnPropertyChanged(nameof(SelectedAcquisitionModeDisplay));
                OnPropertyChanged(nameof(SelectedObservationsDisplay));
                OnPropertyChanged(nameof(SelectedDimensionsDisplay));
                OnPropertyChanged(nameof(SelectedStockUnitDisplay));
                OnPropertyChanged(nameof(SelectedManufacturerReferenceDisplay));
                OnPropertyChanged(nameof(SelectedAuxiliaryReferenceDisplay));
                OnPropertyChanged(nameof(HasSelectedProduct));
                OnPropertyChanged(nameof(SelectedProductCodeDisplay));
                OnPropertyChanged(nameof(SelectedProductCategoryDisplay));
                OnPropertyChanged(nameof(SelectedProductStateDisplay));
                OnPropertyChanged(nameof(IsSelectedProductActive));
                OnPropertyChanged(nameof(IsSelectedProductInactive));
                OnPropertyChanged(nameof(SelectedSalePriceDisplay));
                OnPropertyChanged(nameof(SelectedCostPriceDisplay));
                OnPropertyChanged(nameof(HasSelectedCostPrice));
                OnPropertyChanged(nameof(SelectedAvailableStockDisplay));
                OnPropertyChanged(nameof(SelectedStockStatusDisplay));
                OnPropertyChanged(nameof(SelectedStockLevelValue));
                OnPropertyChanged(nameof(HasSelectedStockMinimum));
                OnPropertyChanged(nameof(SelectedStockLevelDescription));
                OnPropertyChanged(nameof(IsSelectedStockWarning));
                OnPropertyChanged(nameof(IsSelectedStockNegative));
                OnPropertyChanged(nameof(IsSelectedStockPositive));
                OnPropertyChanged(nameof(IsSelectedStockMadeToOrder));
                RaiseCommandStates();
            }
        }
    }

    public bool HasSelectedProduct => SelectedProduct is not null;
    public string SelectedProductCodeDisplay => SelectedProduct is null
        ? string.Empty
        : SelectedProduct.ProductID.ToString("D6", BrazilianCulture);
    public string SelectedProductCategoryDisplay
    {
        get
        {
            if (SelectedProduct is null) return string.Empty;
            var category = new[]
            {
                SelectedProduct.ClassName,
                SelectedProduct.FamilyName,
                SelectedProduct.GroupName,
                SelectedProduct.SubgroupName
            }.Where(value => !string.IsNullOrWhiteSpace(value));
            return string.Join(" / ", category);
        }
    }
    public string SelectedProductStateDisplay => SelectedProduct?.IsActive == true ? "Ativo" : "Inativo";
    public bool IsSelectedProductActive => SelectedProduct?.IsActive == true;
    public bool IsSelectedProductInactive => SelectedProduct is { IsActive: false };
    public string SelectedBarcodeDisplay => DisplayOptional(SelectedProduct?.Barcode);
    public string SelectedAcquisitionModeDisplay => SelectedProduct?.AcquisitionMode == ProductAcquisitionMode.SobEncomenda
        ? "Sob encomenda"
        : SelectedProduct is null ? string.Empty : "Estocado";
    public string SelectedObservationsDisplay => DisplayOptional(SelectedProduct?.Observations);
    public string SelectedDimensionsDisplay => DisplayOptional(SelectedProduct?.Dimensions);
    public string SelectedStockUnitDisplay => DisplayOptional(SelectedProduct?.StockUnitCode);
    public string SelectedManufacturerReferenceDisplay => DisplayOptional(SelectedProduct?.ManufacturerRef);
    public string SelectedAuxiliaryReferenceDisplay => DisplayOptional(SelectedProduct?.AuxiliarRef);
    public string SelectedSalePriceDisplay => SelectedProduct?.SalePrice?.ToString("C", BrazilianCulture) ?? "Não informado";
    public string SelectedCostPriceDisplay => SelectedProduct?.CostPrice?.ToString("C", BrazilianCulture) ?? "Não informado";
    public bool HasSelectedCostPrice => SelectedProduct?.CostPrice.HasValue == true;
    public string SelectedAvailableStockDisplay => SelectedProduct is null
        ? string.Empty
        : $"{SelectedProduct.AvailableStockQuantity.ToString("N2", BrazilianCulture)} {SelectedProduct.StockUnitCode}".TrimEnd();
    public string SelectedStockStatusDisplay => SelectedProduct?.StockStatus ?? string.Empty;
    public double SelectedStockLevelValue => SelectedProduct is not { MinimumStock: > 0 } product
        ? 0
        : (double)Math.Clamp(product.AvailableStockQuantity / product.MinimumStock * 100m, 0m, 100m);
    public bool HasSelectedStockMinimum => SelectedProduct?.MinimumStock > 0;
    public string SelectedStockLevelDescription => SelectedProduct is not { } product
        ? string.Empty
        : HasSelectedStockMinimum
            ? $"Mínimo: {product.MinimumStock.ToString("N2", BrazilianCulture)} {product.StockUnitCode}".TrimEnd()
            : "Sem estoque mínimo cadastrado.";
    public bool IsSelectedStockWarning => SelectedProduct?.StockStatus is "Atenção" or "Repor";
    public bool IsSelectedStockNegative => SelectedProduct?.StockStatus == "Negativo";
    public bool IsSelectedStockPositive => SelectedProduct?.StockStatus == "Positivo";
    public bool IsSelectedStockMadeToOrder => SelectedProduct?.StockStatus == "Sob Demanda";
    public bool CanExport => Listing.TotalCount > 0;

    private static string DisplayOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "Não cadastrado" : value;

    public int ProductId
    {
        get => _productId;
        private set
        {
            if (SetField(ref _productId, value))
                OnPropertyChanged(nameof(ProductCodeDisplay));
        }
    }

    public string ProductCodeDisplay => IsAdding
        ? RecordCodeDisplay.ForNewRecord(_recordCount)
        : RecordCodeDisplay.ForExistingRecord(ProductId);

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

    public bool IsMadeToOrder
    {
        get => _acquisitionMode == ProductAcquisitionMode.SobEncomenda;
        set
        {
            var mode = value ? ProductAcquisitionMode.SobEncomenda : ProductAcquisitionMode.Estocado;
            if (_acquisitionMode == mode) return;
            _acquisitionMode = mode;
            OnPropertyChanged();
        }
    }

    public string? ProductName
    {
        get => _productName;
        set => SetField(ref _productName, value);
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
        set
        {
            if (SetField(ref _observations, QuoteNotesRules.Truncate(value)))
                OnPropertyChanged(nameof(ObservationsCounter));
        }
    }

    public string ObservationsCounter => $"{(_observations ?? string.Empty).EnumerateRunes().Count()}/{QuoteNotesRules.MaximumBytes} caracteres";

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

    public UnitOfMeasureDTO? SelectedStockUnit
    {
        get => _selectedStockUnit;
        set
        {
            if (!SetField(ref _selectedStockUnit, value))
                return;

            OnPropertyChanged(nameof(SelectedStockUnitCode));
        }
    }

    public string SelectedStockUnitCode => SelectedStockUnit?.Code ?? string.Empty;

    // Propriedades de erro para cada campo
    public string BarcodeError => GetFieldError("Barcode");
    public string ProductNameError => GetFieldError("ProductName");
    public string StockQuantityError => GetFieldError("StockQuantity");
    public string MinimumStockError => GetFieldError("MinimumStock");
    public string StockUnitError => GetFieldError("StockUnit");
    public string ManufacturerRefError => GetFieldError("ManufacturerRef");
    public string AuxiliarRefError => GetFieldError("AuxiliarRef");
    public string DimensionsError => GetFieldError("Dimensions");
    public string BrandError => GetFieldError("Brand");
    public string ObservationsError => GetFieldError("Observations");

    // Propriedades booleanas para indicar se há erro
    public bool HasBarcodeError => HasFieldError("Barcode");
    public bool HasProductNameError => HasFieldError("ProductName");
    public bool HasStockQuantityError => HasFieldError("StockQuantity");
    public bool HasMinimumStockError => HasFieldError("MinimumStock");
    public bool HasStockUnitError => HasFieldError("StockUnit");
    public bool HasManufacturerRefError => HasFieldError("ManufacturerRef");
    public bool HasAuxiliarRefError => HasFieldError("AuxiliarRef");
    public bool HasDimensionsError => HasFieldError("Dimensions");
    public bool HasBrandError => HasFieldError("Brand");
    public bool HasObservationsError => HasFieldError("Observations");

    public async Task InitializeAsync()
    {
        // A tela abre apenas com os cabeçalhos do grid.
        // Os produtos são carregados somente após uma pesquisa explícita.
        await Task.WhenAll(EnsureLookupsAsync(), LoadCatalogSummaryAsync());
    }

    public ProductCatalogSummaryDTO? CatalogSummary
    {
        get => _catalogSummary;
        private set
        {
            if (!SetField(ref _catalogSummary, value)) return;
            OnPropertyChanged(nameof(HasCatalogSummary));
            OnPropertyChanged(nameof(TotalProductsDisplay));
            OnPropertyChanged(nameof(ActiveProductsDisplay));
            OnPropertyChanged(nameof(OutOfStockProductsDisplay));
            OnPropertyChanged(nameof(LowStockProductsDisplay));
        }
    }

    public bool HasCatalogSummary => CatalogSummary is not null;
    public string TotalProductsDisplay => FormatCatalogCount(CatalogSummary?.TotalProducts);
    public string ActiveProductsDisplay => FormatCatalogCount(CatalogSummary?.ActiveProducts);
    public string OutOfStockProductsDisplay => FormatCatalogCount(CatalogSummary?.OutOfStockProducts);
    public string LowStockProductsDisplay => FormatCatalogCount(CatalogSummary?.LowStockProducts);

    private async Task LoadCatalogSummaryAsync()
    {
        if (_authorizationService?.CanView("Products") == false)
        {
            CatalogSummary = null;
            return;
        }

        try
        {
            CatalogSummary = await _productService.GetCatalogSummaryAsync();
        }
        catch (Exception exception)
        {
            _logger?.LogWarning(exception, "Não foi possível carregar o resumo do catálogo de produtos.");
            CatalogSummary = null;
        }
    }

    private static string FormatCatalogCount(long? count) =>
        count?.ToString("N0", CultureInfo.GetCultureInfo("pt-BR")) ?? "—";

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

        try
        {
            _recordCount = await RecordCodeDisplay.CountAllAsync(async (page, pageSize) =>
                await _productService.GetAllProductsAsync(page, pageSize));
        }
        catch
        {
            _recordCount = TotalCount;
        }
        OnPropertyChanged(nameof(ProductCodeDisplay));

        _isNew = true;
        IsEditing = true;
        SelectedProduct = null;
        ProductId = 0;
        Barcode = null;
        IsActive = true;
        _acquisitionMode = ProductAcquisitionMode.Estocado;
        OnPropertyChanged(nameof(IsMadeToOrder));
        ProductName = null;
        StockQuantity = 0;
        MinimumStock = 0;
        SelectedStockUnit = null;
        ManufacturerRef = null;
        AuxiliarRef = null;
        Dimensions = null;
        Observations = null;
        SelectedBrand = null;
        ResetClassificationSelection();
        ClearFieldErrors();
        StatusMessage = "Cadastrando um produto.";
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
        _acquisitionMode = SelectedProduct.AcquisitionMode;
        OnPropertyChanged(nameof(IsMadeToOrder));
        ProductName = SelectedProduct.ProductName;
        StockQuantity = SelectedProduct.StockQuantity;
        MinimumStock = SelectedProduct.MinimumStock;
        SelectedStockUnit = StockUnits.FirstOrDefault(unit => unit.UnitOfMeasureID == SelectedProduct.StockUnitID);
        ManufacturerRef = SelectedProduct.ManufacturerRef;
        AuxiliarRef = SelectedProduct.AuxiliarRef;
        Dimensions = SelectedProduct.Dimensions;
        Observations = SelectedProduct.Observations;
        SelectedBrand = Brands.FirstOrDefault(x => x.BrandID == SelectedProduct.BrandID);
        // Evita disparar um carregamento sem IDs e, em paralelo, outro com os IDs
        // do produto. Essa concorrência deixava as listas hierárquicas vazias.
        _selectedClass = Classes.FirstOrDefault(x => x.ClassID == SelectedProduct.ClassID);
        OnPropertyChanged(nameof(SelectedClass));

        StatusMessage = $"Editando o produto {ProductId}.";
        if (_selectedClass is not null)
        {
            await LoadFamiliesAsync(SelectedProduct.FamilyID, SelectedProduct.GroupID, SelectedProduct.SubgroupID);
        }
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
            var unitsTask = _unitOfMeasureService?.GetAllAsync(pageSize: 500);
            var lookupTasks = new List<Task> { brandsTask, classesTask };
            if (unitsTask is not null) lookupTasks.Add(unitsTask);
            await Task.WhenAll(lookupTasks);

            Replace(Brands, await brandsTask);
            Replace(Classes, await classesTask);
            if (unitsTask is not null)
            {
                var units = await unitsTask;
                Replace(StockUnits, units.Where(unit => unit.IsActive || unit.UnitOfMeasureID == SelectedProduct?.StockUnitID));
            }
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
            SelectedProduct = null;
            Listing.Load(await _productService.SearchProductsAsync(SearchText, 1, int.MaxValue));
            OnPropertyChanged(nameof(CanExport));
            OnPropertyChanged(nameof(ProductCodeDisplay));
            if (!IsEditing)
                StatusMessage = "Pesquisa concluída.";
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
                ProductName,
                0,
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
                MinimumStock,
                (int)ProductType.Good,
                null,
                null,
                null,
                null,
                SelectedStockUnit?.UnitOfMeasureID,
                SelectedStockUnit?.Code,
                AcquisitionMode: _acquisitionMode);

            await _productService.SaveProductAsync(dto);
            StatusMessage = _isNew ? "Produto incluído com sucesso." : "Produto atualizado com sucesso.";
            IsEditing = false;
            _isNew = false;
            await LoadProductsAsync();
            await LoadCatalogSummaryAsync();
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
        if (ClearSearchCommand is RelayCommand relayClear) relayClear.RaiseCanExecuteChanged();
        if (EditCommand is RelayCommand relayEdit) relayEdit.RaiseCanExecuteChanged();
        if (SaveCommand is RelayCommand relaySave) relaySave.RaiseCanExecuteChanged();
        if (CancelCommand is RelayCommand relayCancel) relayCancel.RaiseCanExecuteChanged();
        if (NewCommand is RelayCommand relayNew) relayNew.RaiseCanExecuteChanged();
        Listing?.Refresh();
        
        // Notify button visibility properties
        OnPropertyChanged(nameof(CanNew));
        OnPropertyChanged(nameof(CanEdit));
        OnPropertyChanged(nameof(CanSave));
        OnPropertyChanged(nameof(CanCancel));
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
        OnPropertyChanged(nameof(ProductNameError));
        OnPropertyChanged(nameof(StockQuantityError));
        OnPropertyChanged(nameof(MinimumStockError));
        OnPropertyChanged(nameof(StockUnitError));
        OnPropertyChanged(nameof(ManufacturerRefError));
        OnPropertyChanged(nameof(AuxiliarRefError));
        OnPropertyChanged(nameof(DimensionsError));
        OnPropertyChanged(nameof(BrandError));
        OnPropertyChanged(nameof(ObservationsError));
        OnPropertyChanged(nameof(HasBarcodeError));
        OnPropertyChanged(nameof(HasProductNameError));
        OnPropertyChanged(nameof(HasStockQuantityError));
        OnPropertyChanged(nameof(HasMinimumStockError));
        OnPropertyChanged(nameof(HasStockUnitError));
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
        if (errorMessage.Contains("produto", StringComparison.OrdinalIgnoreCase))
            return "ProductName";
        if (errorMessage.Contains("Estoque Inicial", StringComparison.OrdinalIgnoreCase)
            || errorMessage.Contains("Estoque Físico", StringComparison.OrdinalIgnoreCase))
            return "StockQuantity";
        if (errorMessage.Contains("Estoque Mínimo", StringComparison.OrdinalIgnoreCase))
            return "MinimumStock";
        if (errorMessage.Contains("Unidade de Medida", StringComparison.OrdinalIgnoreCase)
            || errorMessage.Contains("Unidade", StringComparison.OrdinalIgnoreCase))
            return "StockUnit";
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
