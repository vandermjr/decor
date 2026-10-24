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
    private ClassificationTreeNode? _selectedCatalogClassificationNode;
    private ClassificationTreeNode? _selectedEditorClassificationNode;
    private string _classificationSearchText = string.Empty;
    private string _editorClassificationSearchText = string.Empty;
    private List<ProductDTO> _loadedSearchResults = [];
    private bool _hasLoadedSearchResults;
    private string _catalogSortDisplay = "CÓDIGO: Crescente";
    private UnitOfMeasureDTO? _selectedStockUnit;
    private readonly Dictionary<string, string> _fieldErrors = [];
    private Task? _lookupsLoadTask;
    private Task? _classificationTreeLoadTask;
    private int _recordCount;
    private ProductCatalogSummaryDTO? _catalogSummary;
    private bool _catalogSummaryLoaded;
    private bool _isCatalogSummaryLoading;
    private DateTimeOffset? _catalogSummaryUpdatedAt;
    private bool _showTotalProductsIndicator = true;
    private bool _showActiveProductsIndicator = true;
    private bool _showInactiveProductsIndicator;
    private bool _showOutOfStockProductsIndicator = true;
    private bool _showLowStockProductsIndicator = true;
    private string? _catalogIndicatorFilter;

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
        IncludeEditorClassificationCommand = new RelayCommand(IncludeEditorClassification, () => CanIncludeEditorClassification);
        ClearClassificationFilterCommand = new RelayCommand(ClearClassificationFilter);
        ClearCatalogIndicatorFilterCommand = new RelayCommand(async () => await ClearCatalogIndicatorFilterAsync());
        RefreshCatalogSummaryCommand = new RelayCommand(async () => await RefreshCatalogSummaryAsync(), () => !IsCatalogSummaryLoading);
    }

    public ObservableCollection<ProductDTO> Products { get; } = [];
    public ObservableCollection<BrandDTO> Brands { get; } = [];
    public ObservableCollection<ClassDTO> Classes { get; } = [];
    public ObservableCollection<FamilyDTO> Families { get; } = [];
    public ObservableCollection<GroupDTO> Groups { get; } = [];
    public ObservableCollection<SubgroupDTO> Subgroups { get; } = [];
    public ObservableCollection<ClassificationTreeNode> ClassificationTreeNodes { get; } = [];
    public ObservableCollection<ClassificationTreeNode> FilteredClassificationTreeNodes { get; } = [];
    public ObservableCollection<ClassificationTreeNode> FilteredEditorClassificationTreeNodes { get; } = [];
    public ObservableCollection<UnitOfMeasureDTO> StockUnits { get; } = [];

    public ICommand SearchCommand { get; }
    public ICommand ClearSearchCommand { get; }
    public ICommand NewCommand { get; }
    public ICommand EditCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand IncludeEditorClassificationCommand { get; }
    public ICommand ClearClassificationFilterCommand { get; }
    public ICommand ClearCatalogIndicatorFilterCommand { get; }
    public ICommand RefreshCatalogSummaryCommand { get; }
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

    public string ClassificationSearchText
    {
        get => _classificationSearchText;
        set
        {
            if (SetField(ref _classificationSearchText, value))
                RefreshFilteredClassificationTree(FilteredClassificationTreeNodes, ClassificationSearchText);
        }
    }

    public string EditorClassificationSearchText
    {
        get => _editorClassificationSearchText;
        set
        {
            if (SetField(ref _editorClassificationSearchText, value))
                RefreshFilteredClassificationTree(FilteredEditorClassificationTreeNodes, EditorClassificationSearchText);
        }
    }

    public ClassificationTreeNode? SelectedCatalogClassificationNode
    {
        get => _selectedCatalogClassificationNode;
        set
        {
            if (!SetField(ref _selectedCatalogClassificationNode, value)) return;
            OnPropertyChanged(nameof(ClassificationFilterDisplay));
            OnPropertyChanged(nameof(ClassificationFilterLabel));
            OnPropertyChanged(nameof(ClassificationFilterPathSegments));
            OnPropertyChanged(nameof(HasClassificationFilter));
            OnPropertyChanged(nameof(HasActiveCatalogFilters));
            if (_hasLoadedSearchResults)
                ApplyProductFilters();
            else
                _ = LoadProductsAsync();
        }
    }

    public ClassificationTreeNode? SelectedEditorClassificationNode
    {
        get => _selectedEditorClassificationNode;
        set
        {
            if (!SetField(ref _selectedEditorClassificationNode, value)) return;
            OnPropertyChanged(nameof(CanIncludeEditorClassification));
            if (IncludeEditorClassificationCommand is RelayCommand command)
                command.RaiseCanExecuteChanged();
        }
    }

    public bool CanIncludeEditorClassification => IsEditing && !IsBusy && SelectedEditorClassificationNode?.SubgroupItem is not null;
    public string ClassificationFilterDisplay => SelectedCatalogClassificationNode is null
        ? string.Empty
        : GetClassificationPath(SelectedCatalogClassificationNode);
    public string ClassificationFilterLabel => string.IsNullOrEmpty(ClassificationFilterDisplay)
        ? string.Empty
        : $"Classificação: {ClassificationFilterDisplay}";
    public IReadOnlyList<ClassificationPathSegment> ClassificationFilterPathSegments =>
        CreateClassificationPathSegments(SelectedCatalogClassificationNode);
    public bool HasClassificationFilter => SelectedCatalogClassificationNode is not null;
    public string CatalogSortDisplay => _catalogSortDisplay;
    public bool HasCatalogSort => !string.IsNullOrWhiteSpace(_catalogSortDisplay);
    public bool HasCatalogIndicatorFilter => !string.IsNullOrWhiteSpace(_catalogIndicatorFilter);
    public string CatalogIndicatorFilterDisplay => _catalogIndicatorFilter switch
    {
        "active" => "Indicador: ativos",
        "inactive" => "Indicador: inativos",
        "out-of-stock" => "Indicador: sem estoque",
        "low-stock" => "Indicador: estoque baixo",
        _ => string.Empty
    };
    public bool HasActiveCatalogFilters => HasClassificationFilter || HasCatalogSort || HasCatalogIndicatorFilter;

    public void SetCatalogSortDisplay(string? display)
    {
        var normalized = display ?? string.Empty;
        if (!SetField(ref _catalogSortDisplay, normalized)) return;
        OnPropertyChanged(nameof(HasCatalogSort));
        OnPropertyChanged(nameof(HasActiveCatalogFilters));
    }
    public string SelectedClassificationPath => string.Join(" / ", new[]
    {
        SelectedClass?.ClassName,
        SelectedFamily?.FamilyName,
        SelectedGroup?.GroupName,
        SelectedSubgroup?.SubgroupName
    }.Where(name => !string.IsNullOrWhiteSpace(name)));

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
        _loadedSearchResults = [];
        _hasLoadedSearchResults = false;
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
    public string SelectedProductClassName => SelectedProduct?.ClassName ?? "Não informado";
    public string SelectedProductFamilyName => SelectedProduct?.FamilyName ?? "Não informado";
    public string SelectedProductGroupName => SelectedProduct?.GroupName ?? "Não informado";
    public string SelectedProductSubgroupName => SelectedProduct?.SubgroupName ?? "Não informado";
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
    public string SubgroupError => GetFieldError("Subgroup");
    public bool HasSubgroupError => HasFieldError("Subgroup");

    public async Task InitializeAsync()
    {
        // A tela abre apenas com os cabeçalhos do grid.
        // Os produtos são carregados somente após uma pesquisa explícita.
        await EnsureLookupsAsync();
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
            OnPropertyChanged(nameof(InactiveProductsDisplay));
            OnPropertyChanged(nameof(OutOfStockProductsDisplay));
            OnPropertyChanged(nameof(LowStockProductsDisplay));
            OnPropertyChanged(nameof(ActiveProductsPercentage));
            OnPropertyChanged(nameof(OutOfStockProductsPercentage));
            OnPropertyChanged(nameof(LowStockProductsPercentage));
            OnPropertyChanged(nameof(ActiveProductsPercentageDisplay));
            OnPropertyChanged(nameof(OutOfStockProductsPercentageDisplay));
            OnPropertyChanged(nameof(LowStockProductsPercentageDisplay));
        }
    }

    public bool HasCatalogSummary => CatalogSummary is not null;
    public bool IsCatalogSummaryLoading
    {
        get => _isCatalogSummaryLoading;
        private set
        {
            if (!SetField(ref _isCatalogSummaryLoading, value)) return;
            if (RefreshCatalogSummaryCommand is RelayCommand command)
                command.RaiseCanExecuteChanged();
        }
    }
    public string CatalogSummaryUpdatedDisplay => _catalogSummaryUpdatedAt is null
        ? "Ainda não atualizado"
        : $"Atualizado às {_catalogSummaryUpdatedAt.Value.LocalDateTime:HH:mm}";
    public string TotalProductsDisplay => FormatCatalogCount(CatalogSummary?.TotalProducts);
    public string ActiveProductsDisplay => FormatCatalogCount(CatalogSummary?.ActiveProducts);
    public string InactiveProductsDisplay => FormatCatalogCount(CatalogSummary is null
        ? null
        : Math.Max(0, CatalogSummary.TotalProducts - CatalogSummary.ActiveProducts));
    public string OutOfStockProductsDisplay => FormatCatalogCount(CatalogSummary?.OutOfStockProducts);
    public string LowStockProductsDisplay => FormatCatalogCount(CatalogSummary?.LowStockProducts);
    public double ActiveProductsPercentage => GetSummaryPercentage(CatalogSummary?.ActiveProducts);
    public double OutOfStockProductsPercentage => GetSummaryPercentage(CatalogSummary?.OutOfStockProducts);
    public double LowStockProductsPercentage => GetSummaryPercentage(CatalogSummary?.LowStockProducts);
    public string ActiveProductsPercentageDisplay => $"{ActiveProductsPercentage:N0}% ativos";
    public string OutOfStockProductsPercentageDisplay => $"{OutOfStockProductsPercentage:N0}% do catálogo";
    public string LowStockProductsPercentageDisplay => $"{LowStockProductsPercentage:N0}% do catálogo";

    public bool ShowTotalProductsIndicator
    {
        get => _showTotalProductsIndicator;
        set => SetField(ref _showTotalProductsIndicator, value);
    }

    public bool ShowActiveProductsIndicator
    {
        get => _showActiveProductsIndicator;
        set => SetField(ref _showActiveProductsIndicator, value);
    }

    public bool ShowInactiveProductsIndicator
    {
        get => _showInactiveProductsIndicator;
        set => SetField(ref _showInactiveProductsIndicator, value);
    }

    public bool ShowOutOfStockProductsIndicator
    {
        get => _showOutOfStockProductsIndicator;
        set => SetField(ref _showOutOfStockProductsIndicator, value);
    }

    public bool ShowLowStockProductsIndicator
    {
        get => _showLowStockProductsIndicator;
        set => SetField(ref _showLowStockProductsIndicator, value);
    }

    public async Task EnsureCatalogSummaryAsync()
    {
        if (_catalogSummaryLoaded || IsCatalogSummaryLoading)
            return;

        await LoadCatalogSummaryAsync();
    }

    public Task RefreshCatalogSummaryAsync() => LoadCatalogSummaryAsync();

    private async Task LoadCatalogSummaryAsync()
    {
        if (IsCatalogSummaryLoading)
            return;

        IsCatalogSummaryLoading = true;
        if (_authorizationService?.CanView("Products") == false)
        {
            CatalogSummary = null;
            _catalogSummaryLoaded = true;
            IsCatalogSummaryLoading = false;
            return;
        }

        try
        {
            CatalogSummary = await _productService.GetCatalogSummaryAsync();
            _catalogSummaryUpdatedAt = DateTimeOffset.Now;
            _catalogSummaryLoaded = true;
            OnPropertyChanged(nameof(CatalogSummaryUpdatedDisplay));
        }
        catch (Exception exception)
        {
            _logger?.LogWarning(exception, "Não foi possível carregar o resumo do catálogo de produtos.");
            CatalogSummary = null;
            _catalogSummaryLoaded = true;
        }
        finally
        {
            IsCatalogSummaryLoading = false;
        }
    }

    private static string FormatCatalogCount(long? count) =>
        count?.ToString("N0", CultureInfo.GetCultureInfo("pt-BR")) ?? "—";

    private double GetSummaryPercentage(long? count) => CatalogSummary is { TotalProducts: > 0 }
        ? Math.Round((count ?? 0) * 100d / CatalogSummary.TotalProducts, 1)
        : 0;

    public async Task BeginNewAsync()
    {
        if (IsEditing)
        {
            return;
        }

        // O formulário exige marca e classificação. Aberturas pelo menu podem
        // acontecer enquanto a inicialização assíncrona da tela ainda está em curso.
        await EnsureLookupsAsync();
        await EnsureClassificationTreeAsync();

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
        SelectedEditorClassificationNode = null;
        EditorClassificationSearchText = string.Empty;
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
        await EnsureClassificationTreeAsync();

        if (Brands.Count == 0 || Classes.Count == 0)
        {
            StatusMessage = "Não foi possível carregar os dados necessários para editar o produto.";
            return;
        }

        _isNew = false;
        IsEditing = true;
        EditorClassificationSearchText = string.Empty;
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
            SelectedEditorClassificationNode = FindSubgroupNode(SelectedProduct.SubgroupID);
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
            _loadedSearchResults = (await _productService.SearchProductsAsync(SearchText, 1, int.MaxValue)).ToList();
            _hasLoadedSearchResults = true;
            ApplyProductFilters();
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

    public async Task EnsureClassificationTreeAsync()
    {
        await EnsureLookupsAsync();
        if (ClassificationTreeNodes.Count > 0 || Classes.Count == 0)
            return;

        if (_classificationTreeLoadTask is null || _classificationTreeLoadTask.IsCompleted)
            _classificationTreeLoadTask = LoadClassificationTreeAsync();

        try
        {
            await _classificationTreeLoadTask;
        }
        catch (Exception exception)
        {
            _classificationTreeLoadTask = null;
            _logger?.LogWarning(exception, "Não foi possível carregar a árvore de classificações.");
        }
    }

    private async Task LoadClassificationTreeAsync()
    {
        var roots = new List<ClassificationTreeNode>();
        var classes = await LoadAllPagesAsync((page, pageSize) => _classService.GetAllAsync(page, pageSize));
        Replace(Classes, classes);
        foreach (var classItem in classes)
        {
            var classNode = new ClassificationTreeNode(classItem.ClassName ?? string.Empty, ClassificationTreeLevel.Class, classItem);
            var families = await LoadAllPagesAsync((page, pageSize) => _familyService.GetByClassIdAsync(classItem.ClassID, page, pageSize));
            foreach (var familyItem in families)
            {
                var familyNode = new ClassificationTreeNode(familyItem.FamilyName ?? string.Empty, ClassificationTreeLevel.Family, classItem, familyItem);
                var groups = await LoadAllPagesAsync((page, pageSize) => _groupService.GetByFamilyIdAsync(familyItem.FamilyID, page, pageSize));
                foreach (var groupItem in groups)
                {
                    var groupNode = new ClassificationTreeNode(groupItem.GroupName ?? string.Empty, ClassificationTreeLevel.Group, classItem, familyItem, groupItem);
                    var subgroups = await LoadAllPagesAsync((page, pageSize) => _subgroupService.GetByGroupIdAsync(groupItem.GroupID, page, pageSize));
                    foreach (var subgroupItem in subgroups)
                        groupNode.Children.Add(new ClassificationTreeNode(subgroupItem.SubgroupName ?? string.Empty, ClassificationTreeLevel.Subgroup, classItem, familyItem, groupItem, subgroupItem));
                    familyNode.Children.Add(groupNode);
                }
                classNode.Children.Add(familyNode);
            }
            roots.Add(classNode);
        }

        Replace(ClassificationTreeNodes, roots);
        RefreshFilteredClassificationTree(FilteredClassificationTreeNodes, ClassificationSearchText);
        RefreshFilteredClassificationTree(FilteredEditorClassificationTreeNodes, EditorClassificationSearchText);
    }

    private static async Task<List<T>> LoadAllPagesAsync<T>(Func<int, int, Task<IEnumerable<T>>> loadPage)
    {
        const int pageSize = 500;
        var items = new List<T>();
        for (var page = 1; ; page++)
        {
            var currentPage = (await loadPage(page, pageSize)).ToList();
            items.AddRange(currentPage);
            if (currentPage.Count < pageSize)
                return items;
        }
    }

    private void RefreshFilteredClassificationTree(ObservableCollection<ClassificationTreeNode> target, string searchText)
    {
        var terms = searchText.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var filtered = ClassificationTreeNodes
            .Select(node => FilterClassificationNode(node, terms, string.Empty))
            .Where(node => node is not null)
            .Cast<ClassificationTreeNode>()
            .ToList();
        Replace(target, filtered);
    }

    private static ClassificationTreeNode? FilterClassificationNode(ClassificationTreeNode node, IReadOnlyList<string> terms, string parentPath)
    {
        var nodePath = string.IsNullOrEmpty(parentPath) ? node.Name : $"{parentPath} {node.Name}";
        var matches = terms.Count == 0 || terms.All(term => nodePath.Contains(term, StringComparison.CurrentCultureIgnoreCase));
        if (matches)
            return node.CopyWithChildren(node.Children.Select(child => FilterClassificationNode(child, [], nodePath)!).Where(child => child is not null));

        var children = node.Children
            .Select(child => FilterClassificationNode(child, terms, nodePath))
            .Where(child => child is not null)
            .Cast<ClassificationTreeNode>()
            .ToList();
        if (children.Count == 0)
            return null;

        var copy = node.CopyWithChildren(children);
        copy.IsExpanded = true;
        return copy;
    }

    public void ClearClassificationFilter() => SelectedCatalogClassificationNode = null;

    public async Task ShowCatalogIndicatorAsync(string? indicator)
    {
        var normalized = indicator is "active" or "inactive" or "out-of-stock" or "low-stock"
            ? indicator
            : null;
        SetCatalogIndicatorFilter(normalized);

        // Os indicadores descrevem o catálogo inteiro, não apenas uma pesquisa ou classificação anterior.
        SearchText = string.Empty;
        ClearClassificationFilterState();
        _loadedSearchResults = [];
        _hasLoadedSearchResults = false;
        await LoadProductsAsync();
    }

    private async Task ClearCatalogIndicatorFilterAsync()
    {
        if (!HasCatalogIndicatorFilter)
            return;

        SetCatalogIndicatorFilter(null);
        if (_hasLoadedSearchResults)
            ApplyProductFilters();
        else
            await LoadProductsAsync();
    }

    private void SetCatalogIndicatorFilter(string? indicator)
    {
        if (SetField(ref _catalogIndicatorFilter, indicator, nameof(CatalogIndicatorFilterDisplay)))
        {
            OnPropertyChanged(nameof(HasCatalogIndicatorFilter));
            OnPropertyChanged(nameof(HasActiveCatalogFilters));
        }
    }

    private void ClearClassificationFilterState()
    {
        if (!SetField(ref _selectedCatalogClassificationNode, null, nameof(SelectedCatalogClassificationNode)))
            return;

        OnPropertyChanged(nameof(ClassificationFilterDisplay));
        OnPropertyChanged(nameof(ClassificationFilterLabel));
        OnPropertyChanged(nameof(ClassificationFilterPathSegments));
        OnPropertyChanged(nameof(HasClassificationFilter));
        OnPropertyChanged(nameof(HasActiveCatalogFilters));
    }

    private void ApplyProductFilters()
    {
        SelectedProduct = null;
        IEnumerable<ProductDTO> filtered = _loadedSearchResults;
        if (SelectedCatalogClassificationNode is { } node)
        {
            filtered = node.Level switch
            {
                ClassificationTreeLevel.Class => filtered.Where(product => product.ClassID == node.ClassItem.ClassID),
                ClassificationTreeLevel.Family => filtered.Where(product => product.FamilyID == node.FamilyItem?.FamilyID),
                ClassificationTreeLevel.Group => filtered.Where(product => product.GroupID == node.GroupItem?.GroupID),
                ClassificationTreeLevel.Subgroup => filtered.Where(product => product.SubgroupID == node.SubgroupItem?.SubgroupID),
                _ => filtered
            };
        }
        filtered = _catalogIndicatorFilter switch
        {
            "active" => filtered.Where(product => product.IsActive),
            "inactive" => filtered.Where(product => !product.IsActive),
            "out-of-stock" => filtered.Where(product => product.AcquisitionMode == ProductAcquisitionMode.Estocado
                && product.AvailableStockQuantity == 0),
            "low-stock" => filtered.Where(product => product.AcquisitionMode == ProductAcquisitionMode.Estocado
                && product.CurrentStockQuantity >= 0
                && product.AvailableStockQuantity > 0
                && product.AvailableStockQuantity < product.MinimumStock),
            _ => filtered
        };
        Listing.Load(filtered);
        OnPropertyChanged(nameof(CanExport));
    }

    private void IncludeEditorClassification()
    {
        if (SelectedEditorClassificationNode is not { SubgroupItem: { } subgroup } node)
            return;

        _selectedClass = node.ClassItem;
        _selectedFamily = node.FamilyItem;
        _selectedGroup = node.GroupItem;
        SetField(ref _selectedSubgroup, subgroup, nameof(SelectedSubgroup));
        _fieldErrors.Remove("Subgroup");
        OnPropertyChanged(nameof(SelectedClass));
        OnPropertyChanged(nameof(SelectedFamily));
        OnPropertyChanged(nameof(SelectedGroup));
        OnPropertyChanged(nameof(SelectedClassificationPath));
        OnPropertyChanged(nameof(SubgroupError));
        OnPropertyChanged(nameof(HasSubgroupError));
        if (IncludeEditorClassificationCommand is RelayCommand command)
            command.RaiseCanExecuteChanged();
    }

    private ClassificationTreeNode? FindSubgroupNode(int? subgroupId) => subgroupId is null
        ? null
        : FilteredEditorClassificationTreeNodes.SelectMany(classNode => classNode.Children)
            .SelectMany(familyNode => familyNode.Children)
            .SelectMany(groupNode => groupNode.Children)
            .FirstOrDefault(node => node.SubgroupItem?.SubgroupID == subgroupId);

    private static string GetClassificationPath(ClassificationTreeNode node) => string.Join(" / ", new[]
    {
        node.ClassItem.ClassName,
        node.FamilyItem?.FamilyName,
        node.GroupItem?.GroupName,
        node.SubgroupItem?.SubgroupName
    }.Where(name => !string.IsNullOrWhiteSpace(name)));

    private static IReadOnlyList<ClassificationPathSegment> CreateClassificationPathSegments(ClassificationTreeNode? node)
    {
        if (node is null)
            return [];

        var levels = new (string? Name, ClassificationTreeLevel Level)[]
        {
            (node.ClassItem.ClassName, ClassificationTreeLevel.Class),
            (node.FamilyItem?.FamilyName, ClassificationTreeLevel.Family),
            (node.GroupItem?.GroupName, ClassificationTreeLevel.Group),
            (node.SubgroupItem?.SubgroupName, ClassificationTreeLevel.Subgroup)
        };
        var populatedLevels = levels.Where(item => !string.IsNullOrWhiteSpace(item.Name)).ToArray();
        return populatedLevels
            .Select((item, index) => new ClassificationPathSegment(
                item.Name!, item.Level, index < populatedLevels.Length - 1))
            .ToArray();
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
            if (HasCatalogSummary)
                await RefreshCatalogSummaryAsync();
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
        OnPropertyChanged(nameof(SubgroupError));
        OnPropertyChanged(nameof(HasSubgroupError));
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
        if (errorMessage.Contains("Subgrupo", StringComparison.OrdinalIgnoreCase))
            return "Subgroup";
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
