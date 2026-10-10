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

namespace Decor.AvaloniaUI.ViewModels;

public sealed record QuoteListItem(int QuoteID, int CustomerID, int CreatedByEmployeeID, string Source,
    int? SourcePartnerID, DateTime CreatedAt, string? Notes, string CustomerName = "", string CreatedByEmployeeName = "",
    string Status = "", decimal Total = 0m)
{
    public string TotalDisplay => Total.ToString("C2", CultureInfo.GetCultureInfo("pt-BR"));
}
public sealed record QuoteCustomerOption(int CustomerID, string Name, string? Document, string? Phone = null)
{
    public override string ToString() => string.Join(" · ", new[] { Name, Document, Phone }.Where(value => !string.IsNullOrWhiteSpace(value)));
}
public sealed record QuoteEmployeeOption(int EmployeeID, string Name, int? UserID)
{
    public override string ToString() => Name;
}
public sealed record QuotePartnerOption(int PartnerID, string Name)
{
    public override string ToString() => Name;
}
public sealed record QuoteSectionOption(QuoteSectionDTO DTO, string Label)
{
    public override string ToString() => Label;
}
public sealed record QuoteProductOption(ProductDTO? DTO, string Label, string Unit = "", ServiceDTO? Service = null)
{
    public QuoteProductOption(ServiceDTO service, string label) : this(null, label, "", service) { }
    public int? ProductID => DTO?.ProductID;
    public int? ServiceID => Service?.ServiceID;
    public int Code => ServiceID ?? ProductID ?? 0;
    public string Description => Service?.Description ?? DTO?.Description ?? "Sem descrição";
    public string Category => Service is not null ? "Serviço" : "Produto";
    public decimal Price => Service?.SalePrice ?? DTO?.SalePrice ?? 0m;
    public override string ToString() => Label;
}
public sealed record QuoteLineOption(QuoteItemDTO DTO, string ProductName, decimal Total, int Item = 0, string Category = "")
{
    public decimal UnitPrice => DTO.UnitPrice ?? 0m;
    public decimal Quantity => DTO.Quantity;
}
public sealed record QuoteReviewLine(string Section, string Item, decimal Quantity, decimal UnitPrice, decimal Total);

public sealed class QuotesViewModel : IStatusBarSource, IWorkspaceDocumentState
{
    private readonly IQuoteService _quoteService;
    private readonly IAuthorizationService _authorizationService;
    private readonly ICustomerService _customerService;
    private readonly IEmployeeService _employeeService;
    private readonly IPartnerService _partnerService;
    private readonly IProductService _productService;
    private readonly IServiceCatalogService? _serviceCatalogService;
    private readonly IAuthenticatedUserContext _authenticatedUserContext;
    private readonly IOrderService? _orderService;
    private Dictionary<int, UnitOfMeasureDTO> _units = [];
    private readonly Dictionary<int, QuoteProductOption> _knownProducts = [];
    private readonly Dictionary<int, QuoteProductOption> _knownServices = [];
    private string _discountInput = "0";
    private readonly ObservableCollection<QuoteListItem> _items = [];
    private QuoteListItem? _selectedItem;
    private QuoteDTO? _editingQuote;
    private string _searchText = string.Empty;
    private string _statusMessage = string.Empty;
    private string _errorMessage = string.Empty;
    private string _customerSearchText = string.Empty;
    private string _employeeSearchText = string.Empty;
    private string _partnerSearchText = string.Empty;
    private string _productSearchText = string.Empty;
    private string _quantityInput = "1";
    private string _unitPriceInput = string.Empty;
    private string _sectionTypeName = "Catálogo";
    private string _notes = string.Empty;
    private string _sourceType = "Própria";
    private DateTime _quoteDate = DateTime.UtcNow;
    private CancellationTokenSource? _catalogCancellation;
    private int _catalogGeneration;
    private int _catalogTabIndex;
    private int _catalogPage = 1;
    private bool _hasCatalogNext;
    private bool _isCatalogBusy;
    private bool _isBusy;
    private bool _isEditing;
    private bool _isNew;
    private QuoteCustomerOption? _selectedCustomer;
    private QuoteEmployeeOption? _selectedEmployee;
    private QuotePartnerOption? _selectedPartner;
    private QuoteSectionOption? _selectedSection;
    private QuoteProductOption? _selectedProduct;
    private QuoteLineOption? _selectedLine;
    private bool _hasInstallationService;

    public QuotesViewModel(IQuoteService quoteService, IAuthorizationService authorizationService,
        ICustomerService customerService, IEmployeeService employeeService, IPartnerService partnerService,
        IProductService productService,
        IAuthenticatedUserContext authenticatedUserContext, IOrderService? orderService = null,
        IUnitOfMeasureService? unitOfMeasureService = null, IServiceCatalogService? serviceCatalogService = null)
    {
        _quoteService = quoteService;
        _authorizationService = authorizationService;
        _customerService = customerService;
        _employeeService = employeeService;
        _partnerService = partnerService;
        _productService = productService;
        _serviceCatalogService = serviceCatalogService;
        _authenticatedUserContext = authenticatedUserContext;
        _orderService = orderService;
        Listing = new GridListState<QuoteListItem>(_items, () => !IsEditing);
        Listing.PropertyChanged += (_, args) => OnPropertyChanged(args.PropertyName);
        SearchCommand = new RelayCommand(async () => await LoadAsync(), () => CanSearch);
        ClearSearchCommand = new RelayCommand(async () => { ClearSearch(); await LoadAsync(); }, () => CanSearch);
        ClearCustomerCommand = new RelayCommand(() => SelectedCustomer = null, () => !IsBusy);
        ClearEmployeeCommand = new RelayCommand(() => SelectedEmployee = null, () => !IsBusy);
        SearchCustomersCommand = new RelayCommand(async () => await SearchCustomersAsync());
        SearchEmployeesCommand = new RelayCommand(async () => await SearchEmployeesAsync());
        SearchPartnersCommand = new RelayCommand(async () => await SearchPartnersAsync());
        SearchProductsCommand = new RelayCommand(async () => await SearchProductsAsync(), () => IsEditing && !IsCatalogBusy);
        ClearProductsCommand = new RelayCommand(ClearCatalog, () => !IsBusy);
        CatalogPreviousCommand = new RelayCommand(async () => await SearchCatalogPageAsync(_catalogPage - 1), () => IsEditing && !IsCatalogBusy && HasCatalogPrevious);
        CatalogNextCommand = new RelayCommand(async () => await SearchCatalogPageAsync(_catalogPage + 1), () => IsEditing && !IsCatalogBusy && HasCatalogNext);
        CatalogPagination = new CatalogPaginationState(this);
        NewLineCommand = new RelayCommand(ResetLine, () => !IsBusy);
        GeneratePdfCommand = new RelayCommand(async () => { if (await SaveAsync()) PdfRequested?.Invoke(this, EventArgs.Empty); }, () => CanSave && ItemCount > 0);
        CancelQuoteCommand = new RelayCommand(async () => await CancelQuoteAsync(), () => IsEditing && !IsBusy && IsQuoteOpen && _authorizationService.HasPermission(DecorPermissions.QuotesApprove));
        ConvertToOrderCommand = new RelayCommand(async () => await ConvertToOrderAsync(), () => CanSave && ItemCount > 0 && _orderService is not null && _authorizationService.HasPermission(DecorPermissions.OrdersConvertFromQuote) && _authorizationService.HasPermission(DecorPermissions.QuotesSend) && _authorizationService.HasPermission(DecorPermissions.QuotesApprove));
        CreateSectionCommand = new RelayCommand(async () => await CreateSectionAsync(), () => CanManageSections);
        SaveLineCommand = new RelayCommand(async () => await SaveLineAsync(), () => CanManageLines);
        RequestQuotationCommand = new RelayCommand(async () => await UpdateSectionStatusAsync(QuoteSectionStatus.AwaitingQuotation), () => CanRequestQuotation);
        SendSectionCommand = new RelayCommand(async () => await UpdateSectionStatusAsync(QuoteSectionStatus.Sent), () => CanSendSection);
        ApproveSectionCommand = new RelayCommand(async () => await UpdateSectionStatusAsync(QuoteSectionStatus.Approved), () => CanApproveSection);
        RejectSectionCommand = new RelayCommand(async () => await UpdateSectionStatusAsync(QuoteSectionStatus.Rejected), () => CanRejectSection);
        NewCommand = new RelayCommand(async () => await BeginNewAsync(), () => CanNew);
        EditCommand = new RelayCommand(async () => await BeginEditAsync(), () => CanEdit);
        SaveCommand = new RelayCommand(async () => await SaveAsync(), () => CanSave);
        CancelCommand = new RelayCommand(async () => await ReturnToListAsync(), () => CanCancel);
    }

    public GridListState<QuoteListItem> Listing { get; }
    public ObservableCollection<QuoteListItem> Items => _items;
    public ICommand SearchCommand { get; }
    public ICommand ClearSearchCommand { get; }
    public ICommand ClearCustomerCommand { get; }
    public ICommand ClearEmployeeCommand { get; }
    public ICommand SearchCustomersCommand { get; }
    public ICommand SearchEmployeesCommand { get; }
    public ICommand SearchPartnersCommand { get; }
    public ICommand SearchProductsCommand { get; }
    public ICommand ClearProductsCommand { get; }
    public ICommand CatalogPreviousCommand { get; }
    public ICommand CatalogNextCommand { get; }
    public CatalogPaginationState CatalogPagination { get; }
    public string CatalogPageDisplay => $"Página {_catalogPage}";
    public bool HasCatalogPrevious => _catalogPage > 1;
    public bool HasCatalogNext => _hasCatalogNext;
    public bool IsCatalogBusy
    {
        get => _isCatalogBusy;
        private set { if (SetField(ref _isCatalogBusy, value)) RefreshCommands(); }
    }
    public int CatalogTabIndex
    {
        get => _catalogTabIndex;
        set
        {
            if (value is < 0 or > 1 || !SetField(ref _catalogTabIndex, value)) return;
            _ = SearchProductsAsync();
        }
    }
    public ICommand NewLineCommand { get; }
    public ICommand GeneratePdfCommand { get; }
    public ICommand CancelQuoteCommand { get; }
    public ICommand ConvertToOrderCommand { get; }
    public event EventHandler? PdfRequested;
    public DateTime QuoteDate => _quoteDate.ToLocalTime();
    public DateTime? SelectedQuoteDate
    {
        get => QuoteDate;
        set
        {
            if (value is null) return;
            var localDate = DateTime.SpecifyKind(value.Value.Date, DateTimeKind.Local);
            var date = _quoteDate.Kind == DateTimeKind.Utc ? localDate.ToUniversalTime()
                : DateTime.SpecifyKind(localDate, _quoteDate.Kind);
            if (!SetField(ref _quoteDate, date)) return;
            OnPropertyChanged(nameof(QuoteDate));
            OnPropertyChanged(nameof(SelectedQuoteDate));
        }
    }
    public string QuoteState => Sections.Count > 0 && Sections.All(section => section.DTO.Status == (int)QuoteSectionStatus.Rejected)
        ? "CANCELADO" : Sections.Count > 0
            && Sections.Any(section => section.DTO.Status == (int)QuoteSectionStatus.ConvertedToOrder)
            && Sections.All(section => section.DTO.Status is (int)QuoteSectionStatus.Rejected or (int)QuoteSectionStatus.ConvertedToOrder)
                ? FormatOrderReferences(Sections.Where(section => section.DTO.Status == (int)QuoteSectionStatus.ConvertedToOrder)
                    .Select(section => section.DTO.OrderID)) : "ABERTO";
    public bool IsQuoteOpen => QuoteState == "ABERTO";
    public bool IsQuoteCancelled => QuoteState == "CANCELADO";
    public bool IsQuoteConverted => QuoteState.StartsWith("Ref. Pedido Nº ", StringComparison.Ordinal)
        || QuoteState == "PEDIDO CONVERTIDO";
    public string CurrencySymbol => string.IsNullOrWhiteSpace(CultureInfo.CurrentCulture.NumberFormat.CurrencySymbol)
        || CultureInfo.CurrentCulture.Equals(CultureInfo.InvariantCulture)
        ? CultureInfo.GetCultureInfo("pt-BR").NumberFormat.CurrencySymbol
        : CultureInfo.CurrentCulture.NumberFormat.CurrencySymbol;
    public string DiscountInput
    {
        get => _discountInput;
        set
        {
            if (!SetField(ref _discountInput, value)) return;
            OnPropertyChanged(nameof(DiscountValue));
            OnPropertyChanged(nameof(DiscountAmount));
            OnPropertyChanged(nameof(NetTotal));
        }
    }
    public decimal? DiscountValue
    {
        get => TryParseDecimal(DiscountInput, out var value) ? value : null;
        set
        {
            if (value is null) DiscountInput = string.Empty;
            else if (value >= 0 && value <= QuoteTotal) DiscountInput = decimal.Round(value.Value, 2).ToString(CultureInfo.CurrentCulture);
        }
    }
    public decimal DiscountAmount => TryParseDecimal(DiscountInput, out var discount) ? discount : 0m;
    public decimal NetTotal => QuoteTotal - DiscountAmount;
    public ObservableCollection<QuoteProductOption> CatalogProducts { get; } = [];
    public ObservableCollection<QuoteProductOption> CatalogServices { get; } = [];
    public ObservableCollection<QuoteLineOption> AllLines { get; } = [];
    public ICommand CreateSectionCommand { get; }
    public ICommand SaveLineCommand { get; }
    public bool CanDeleteLine(QuoteLineOption line) => CanManageSections
        && Sections.Any(section => section.DTO.QuoteSectionID == line.DTO.QuoteSectionID
            && section.DTO.Status == (int)QuoteSectionStatus.Draft);

    public async Task DeleteLineAsync(QuoteLineOption line)
    {
        if (!CanDeleteLine(line)) return;
        IsBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            await _quoteService.DeleteQuoteItemAsync(CurrentQuoteId, line.DTO.QuoteItemID);
            await ReloadQuoteAsync(line.DTO.QuoteSectionID);
            ResetLine();
            StatusMessage = "Item removido do orçamento.";
        }
        catch (Exception exception) { ErrorMessage = exception.Message; }
        finally { IsBusy = false; }
    }
    public ICommand RequestQuotationCommand { get; }
    public ICommand SendSectionCommand { get; }
    public ICommand ApproveSectionCommand { get; }
    public ICommand RejectSectionCommand { get; }
    public ICommand NewCommand { get; }
    public ICommand EditCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }
    public string SearchText { get => _searchText; set => SetField(ref _searchText, value); }
    public string StatusMessage { get => _statusMessage; private set => SetField(ref _statusMessage, value); }
    public string ErrorMessage { get => _errorMessage; private set { if (SetField(ref _errorMessage, value)) OnPropertyChanged(nameof(HasError)); } }
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public ObservableCollection<QuoteCustomerOption> Customers { get; } = [];
    public ObservableCollection<QuoteEmployeeOption> Employees { get; } = [];
    public ObservableCollection<QuotePartnerOption> Partners { get; } = [];
    public ObservableCollection<QuoteSectionOption> Sections { get; } = [];
    public ObservableCollection<QuoteProductOption> Products { get; } = [];
    public ObservableCollection<QuoteLineOption> SectionItems { get; } = [];
    public ObservableCollection<QuoteReviewLine> ReviewLines { get; } = [];
    public string CustomerSearchText { get => _customerSearchText; set => SetField(ref _customerSearchText, value); }
    public string EmployeeSearchText { get => _employeeSearchText; set => SetField(ref _employeeSearchText, value); }
    public string PartnerSearchText { get => _partnerSearchText; set => SetField(ref _partnerSearchText, value); }
    public string ProductSearchText
    {
        get => _productSearchText;
        set
        {
            if (!SetField(ref _productSearchText, value)) return;
            if (string.IsNullOrWhiteSpace(value)) ResetCatalogResults();
            else _ = DebounceCatalogAsync();
        }
    }
    public string QuantityInput
    {
        get => _quantityInput;
        set
        {
            if (!SetField(ref _quantityInput, value)) return;
            OnPropertyChanged(nameof(QuantityValue));
            OnPropertyChanged(nameof(SaleValue));
        }
    }
    private UnitOfMeasureDTO? SelectedQuantityUnit => SelectedProduct?.DTO?.StockUnitID is int unitId
        ? _units.GetValueOrDefault(unitId) : null;
    public int QuantityDecimalPlaces => QuoteQuantityRules.DecimalPlaces(SelectedQuantityUnit?.AllowsFraction);
    public string QuantityFormat => $"N{QuantityDecimalPlaces}";
    public decimal QuantityMinimum => QuantityDecimalPlaces == 0 ? 1m : 0.001m;
    public decimal QuantityMaximum => QuantityDecimalPlaces == 0 ? decimal.Truncate(QuoteQuantityRules.Maximum) : QuoteQuantityRules.Maximum;
    public string QuantityUnitLabel => SelectedQuantityUnit?.Code ?? string.Empty;
    public string QuantityUnitDescription => SelectedQuantityUnit?.Description ?? string.Empty;
    public decimal? QuantityValue
    {
        get => TryParseDecimal(QuantityInput, out var value) ? value : null;
        set
        {
            if (value is null) QuantityInput = string.Empty;
            else if (value > 0 && value <= QuantityMaximum) QuantityInput = value.Value.ToString(CultureInfo.CurrentCulture);
        }
    }
    public string UnitPriceInput
    {
        get => _unitPriceInput;
        set
        {
            if (!SetField(ref _unitPriceInput, value)) return;
            OnPropertyChanged(nameof(UnitPriceValue));
            OnPropertyChanged(nameof(SaleValue));
        }
    }
    public decimal? SaleValue
    {
        get => QuantityValue is decimal quantity && UnitPriceValue is decimal price
            ? decimal.Round(quantity * price, 2, MidpointRounding.AwayFromZero) : null;
        set
        {
            if (value is null) UnitPriceInput = string.Empty;
            else if (value >= 0 && QuantityValue is > 0)
                UnitPriceValue = value.Value / QuantityValue.Value;
        }
    }
    public decimal? UnitPriceValue
    {
        get => TryParseDecimal(UnitPriceInput, out var value) ? value : null;
        set
        {
            if (value is null) UnitPriceInput = string.Empty;
            else if (value >= 0 && value <= 999999999.99m) UnitPriceInput = decimal.Round(value.Value, 2).ToString(CultureInfo.CurrentCulture);
        }
    }
    public string SectionTypeName { get => _sectionTypeName; set => SetField(ref _sectionTypeName, value); }
    public IReadOnlyList<string> SectionTypes { get; } = ["Catálogo", "Sob medida"];
    public bool HasInstallationService { get => _hasInstallationService; set => SetField(ref _hasInstallationService, value); }
    public QuoteCustomerOption? SelectedCustomer
    {
        get => _selectedCustomer;
        set
        {
            if (!SetField(ref _selectedCustomer, value)) return;
            OnPropertyChanged(nameof(CustomerSummary));
            OnPropertyChanged(nameof(HasSelectedCustomer));
            OnPropertyChanged(nameof(CustomerSelectionDetail));
            RefreshCommands();
        }
    }
    public QuoteEmployeeOption? SelectedEmployee
    {
        get => _selectedEmployee;
        set
        {
            if (!SetField(ref _selectedEmployee, value)) return;
            OnPropertyChanged(nameof(EmployeeSummary));
            OnPropertyChanged(nameof(HasSelectedEmployee));
            OnPropertyChanged(nameof(EmployeeSelectionDetail));
            RefreshCommands();
        }
    }
    public QuotePartnerOption? SelectedPartner
    {
        get => _selectedPartner;
        set
        {
            if (!SetField(ref _selectedPartner, value)) return;
            OnPropertyChanged(nameof(PartnerSelectionDetail));
            RefreshCommands();
        }
    }
    public QuoteSectionOption? SelectedSection
    {
        get => _selectedSection;
        set
        {
            if (!SetField(ref _selectedSection, value)) return;
            LoadSectionLines();
            RefreshCommands();
        }
    }
    public QuoteProductOption? SelectedProduct
    {
        get => _selectedProduct;
        set
        {
            if (!SetField(ref _selectedProduct, value)) return;
            if (_selectedLine is not null && value is not null
                && (_selectedLine.DTO.ProductID != value.ProductID || _selectedLine.DTO.ServiceID != value.ServiceID))
                SelectedLine = null;
            OnPropertyChanged(nameof(ProductSelectionDetail));
            NotifyQuantityUnit();
            if (_selectedLine is null && (value?.Service?.SalePrice ?? value?.DTO?.SalePrice) is decimal salePrice)
            {
                QuantityInput = "1";
                UnitPriceInput = salePrice.ToString(CultureInfo.CurrentCulture);
            }
            RefreshCommands();
        }
    }
    public QuoteLineOption? SelectedLine
    {
        get => _selectedLine;
        set
        {
            if (!SetField(ref _selectedLine, value)) return;
            OnPropertyChanged(nameof(LineButtonText));
            if (value is null) return;
            var section = Sections.FirstOrDefault(option => option.DTO.QuoteSectionID == value.DTO.QuoteSectionID);
            if (SelectedSection != section) SelectedSection = section;
            _selectedLine = value;
            QuantityInput = value.DTO.Quantity.ToString(CultureInfo.CurrentCulture);
            UnitPriceInput = value.DTO.UnitPrice?.ToString(CultureInfo.CurrentCulture) ?? string.Empty;
            HasInstallationService = value.DTO.HasInstallationService;
            SelectedProduct = GetKnownOption(value.DTO)
                ?? Products.FirstOrDefault(product => product.ProductID == value.DTO.ProductID
                    && product.ServiceID == value.DTO.ServiceID);
        }
    }
    public string CurrentUsername => _authenticatedUserContext.User?.DisplayName
        is { Length: > 0 } displayName ? displayName
        : _authenticatedUserContext.User?.EmployeeName
        ?? _authenticatedUserContext.User?.Username
        ?? "Sessão não identificada";
    public string CreatedByDisplay => _editingQuote is null
        ? CurrentUsername
        : _editingQuote.CreatedByUserID is int userId
            ? userId == _authenticatedUserContext.User?.UserID ? CurrentUsername : $"Conta #{userId}"
            : "Não registrado (orçamento legado)";
    public int NotesLimit => QuoteNotesRules.MaximumBytes;
    public string NotesCounter => $"{Notes.EnumerateRunes().Count()}/{NotesLimit} caracteres";
    public string Notes
    {
        get => _notes;
        set { if (SetField(ref _notes, QuoteNotesRules.Truncate(value))) OnPropertyChanged(nameof(NotesCounter)); }
    }
    public string SourceType
    {
        get => _sourceType;
        set
        {
            if (!SetField(ref _sourceType, value)) return;
            OnPropertyChanged(nameof(IsPartnerOrigin));
            RefreshCommands();
        }
    }
    public IReadOnlyList<string> SourceTypes { get; } = ["Própria", "Loja", "Outra"];
    public bool IsPartnerOrigin => false;
    public int CurrentQuoteId => _editingQuote?.QuoteID ?? 0;
    public decimal SectionTotal => SectionItems.Sum(item => item.Total);
    public decimal QuoteTotal => Sections.SelectMany(section => section.DTO.Items ?? []).Sum(item => item.Quantity * (item.UnitPrice ?? 0m));
    public string LineButtonText => SelectedLine is null ? "Adicionar item" : "Atualizar item";
    public bool CanManageSections => IsEditing && CurrentQuoteId > 0 && !IsBusy && _authorizationService.HasPermission(DecorPermissions.QuotesEdit);
    public bool CanManageLines => CanManageSections && SelectedSection?.DTO.Status == (int)QuoteSectionStatus.Draft && SelectedProduct is not null;
    public bool CanRequestQuotation => CanManageSections && SelectedSection?.DTO.Status == (int)QuoteSectionStatus.Draft;
    public bool CanSendSection => IsEditing && !IsBusy && _authorizationService.HasPermission(DecorPermissions.QuotesSend)
        && SelectedSection?.DTO.Status == (int)QuoteSectionStatus.AwaitingQuotation;
    public bool CanApproveSection => IsEditing && !IsBusy && _authorizationService.HasPermission(DecorPermissions.QuotesApprove)
        && SelectedSection?.DTO.Status == (int)QuoteSectionStatus.Sent;
    public bool CanRejectSection => IsEditing && !IsBusy && _authorizationService.HasPermission(DecorPermissions.QuotesApprove)
        && SelectedSection?.DTO.Status is (int)QuoteSectionStatus.AwaitingQuotation or (int)QuoteSectionStatus.Sent;
    public string CodeDisplay => RecordCodeDisplay.ForExistingRecord(CurrentQuoteId);
    public QuoteListItem? SelectedItem
    {
        get => _selectedItem;
        set { if (SetField(ref _selectedItem, value)) RefreshCommands(); }
    }
    public bool IsBusy { get => _isBusy; private set { if (SetField(ref _isBusy, value)) RefreshCommands(); } }
    public bool IsEditing
    {
        get => _isEditing;
        private set
        {
            if (!SetField(ref _isEditing, value)) return;
            Listing.Refresh();
            OnPropertyChanged(nameof(IsAdding));
            OnPropertyChanged(nameof(CodeDisplay));
            RefreshCommands();
        }
    }
    public bool IsAdding => IsEditing && _isNew;
    public int ItemCount => Sections.SelectMany(section => section.DTO.Items ?? []).Count();
    public string CustomerSummary => SelectedCustomer?.Name ?? "Selecione um cliente";
    public string EmployeeSummary => SelectedEmployee?.Name ?? "Selecione um vendedor";
    public bool HasSelectedCustomer => SelectedCustomer is not null;
    public bool HasSelectedEmployee => SelectedEmployee is not null;
    public string CustomerSelectionDetail => SelectedCustomer is null
        ? "Nenhum cliente selecionado"
        : string.Join(" · ", new[] { SelectedCustomer.Document, SelectedCustomer.Phone }.Where(value => !string.IsNullOrWhiteSpace(value)));
    public string EmployeeSelectionDetail => SelectedEmployee is null ? "Nenhum vendedor selecionado" : SelectedEmployee.Name;
    public string PartnerSelectionDetail => SelectedPartner is null ? "Nenhum parceiro selecionado" : SelectedPartner.Name;
    public string ProductSelectionDetail => SelectedProduct?.Label ?? "Nenhum produto ou serviço selecionado";
    public bool CanSearch => _authorizationService.HasPermission(DecorPermissions.QuotesView) && !IsBusy && !IsEditing;
    public bool CanNew => _authorizationService.HasPermission(DecorPermissions.QuotesCreate) && !IsBusy && !IsEditing;
    public bool CanEdit => _authorizationService.HasPermission(DecorPermissions.QuotesEdit) && SelectedItem is not null && !IsBusy && !IsEditing;
    public bool CanSave => IsEditing && !IsBusy && IsQuoteOpen
        && _authorizationService.HasPermission(CurrentQuoteId > 0 ? DecorPermissions.QuotesEdit : DecorPermissions.QuotesCreate);
    public bool CanCancel => IsEditing && !IsBusy;
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

    public void ApplyLookupSelection(LookupSearchContext context, object? value)
    {
        switch (context, value)
        {
            case (LookupSearchContext.Customer, CustomerDTO customer):
                SelectedCustomer = new QuoteCustomerOption(customer.CustomerID, customer.Name ?? "(sem nome)", customer.Document, customer.Phone);
                break;
            case (LookupSearchContext.Employee, EmployeeDTO employee):
                SelectedEmployee = new QuoteEmployeeOption(employee.EmployeeID, employee.Name ?? "(sem nome)", employee.UserID);
                break;
            case (LookupSearchContext.Partner, PartnerDTO partner):
                SelectedPartner = new QuotePartnerOption(partner.PartnerID, partner.Name ?? "(sem nome)");
                break;
            case (LookupSearchContext.Product, ProductDTO product) when product.ProductType == (int)ProductType.Good:
                SelectedProduct = ToProductOption(product);
                _knownProducts[product.ProductID] = SelectedProduct;
                break;
            case (LookupSearchContext.Service, ServiceDTO service):
                SelectedProduct = ToServiceOption(service);
                _knownServices[service.ServiceID] = SelectedProduct;
                break;
        }
    }

    public Task InitializeAsync() => LoadAsync();

    private async Task SearchCustomersAsync()
    {
        try
        {
            var results = await _customerService.SearchCustomersAsync(CustomerSearchText, 1, 100);
            Replace(Customers, results.Where(customer => customer.IsActive)
                .Select(customer => new QuoteCustomerOption(customer.CustomerID, customer.Name ?? "(sem nome)", customer.Document, customer.Phone)));
        }
        catch (Exception) { ErrorMessage = "Não foi possível pesquisar clientes."; }
    }

    private async Task SearchEmployeesAsync()
    {
        try
        {
            var results = await _employeeService.SearchEmployeesAsync(EmployeeSearchText, 1, 100);
            Replace(Employees, results.Where(employee => employee.IsActive)
                .Select(employee => new QuoteEmployeeOption(employee.EmployeeID, employee.Name ?? "(sem nome)", employee.UserID)));
        }
        catch (Exception) { ErrorMessage = "Não foi possível pesquisar funcionários."; }
    }

    private async Task SearchPartnersAsync()
    {
        try
        {
            var results = await _partnerService.SearchPartnersAsync(PartnerSearchText, 1, 100);
            Replace(Partners, results.Where(partner => partner.IsActive)
                .Select(partner => new QuotePartnerOption(partner.PartnerID, partner.Name ?? "(sem nome)")));
        }
        catch (Exception) { ErrorMessage = "Não foi possível pesquisar parceiros."; }
    }

    private Task SearchProductsAsync() => SearchCatalogPageAsync(1);

    private CancellationTokenSource StartCatalogRequest()
    {
        _catalogCancellation?.Cancel();
        _catalogCancellation?.Dispose();
        _catalogCancellation = new CancellationTokenSource();
        _catalogGeneration++;
        return _catalogCancellation;
    }

    private async Task DebounceCatalogAsync()
    {
        var cancellation = StartCatalogRequest();
        var token = cancellation.Token;
        var generation = _catalogGeneration;
        IsCatalogBusy = false;
        if (!IsEditing) return;
        try
        {
            await Task.Delay(300, token);
            if (token.IsCancellationRequested || generation != _catalogGeneration || !IsEditing) return;
            await SearchProductsAsync();
        }
        catch (OperationCanceledException) { }
    }

    private void ClearCatalog()
    {
        ProductSearchText = string.Empty;
        ResetCatalogResults();
    }

    private void ResetCatalogResults()
    {
        StartCatalogRequest();
        Products.Clear();
        CatalogProducts.Clear();
        CatalogServices.Clear();
        _catalogPage = 1;
        _hasCatalogNext = false;
        IsCatalogBusy = false;
        NotifyCatalogPage();
    }

    private void NotifyCatalogPage()
    {
        OnPropertyChanged(nameof(CatalogPageDisplay));
        OnPropertyChanged(nameof(HasCatalogPrevious));
        OnPropertyChanged(nameof(HasCatalogNext));
        RefreshCommands();
    }

    private async Task SearchCatalogPageAsync(int page)
    {
        var cancellation = StartCatalogRequest();
        var token = cancellation.Token;
        var generation = _catalogGeneration;
        var text = ProductSearchText.Trim();
        var tab = CatalogTabIndex;
        var pageSize = CatalogPagination.SelectedPageSize;
        if (!IsEditing || string.IsNullOrWhiteSpace(text))
        {
            ResetCatalogResults();
            return;
        }
        if (page < 1) return;
        var query = tab == 0 ? $"{text} tipo:produto" : text;
        async Task<QuoteProductOption[]> SearchPageAsync(int requestedPage)
        {
            if (tab == 0)
                return (await _productService.SearchProductsAsync(query, requestedPage, pageSize, token))
                    .Select(ToProductOption).ToArray();
            var catalog = _serviceCatalogService ?? throw new InvalidOperationException("O catálogo de serviços não está disponível.");
            return (await catalog.SearchServicesAsync(query, requestedPage, pageSize, token))
                .Select(ToServiceOption).ToArray();
        }
        bool IsCurrent() => !token.IsCancellationRequested && generation == _catalogGeneration
            && IsEditing && ProductSearchText.Trim() == text && CatalogTabIndex == tab
            && CatalogPagination.SelectedPageSize == pageSize;
        IsCatalogBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            var results = await SearchPageAsync(page);
            if (!IsCurrent()) return;
            var next = results.Length == pageSize
                ? (await SearchPageAsync(page + 1)).Any()
                : false;
            if (!IsCurrent()) return;
            Replace(Products, results.Where(option => option.Service?.IsActive
                ?? (option.DTO is { IsActive: true, ProductType: (int)ProductType.Good })));
            foreach (var option in Products)
            {
                if (option.ServiceID is int serviceId) _knownServices[serviceId] = option;
                else if (option.ProductID is int productId) _knownProducts[productId] = option;
            }
            Replace(CatalogProducts, tab == 0 ? Products : []);
            Replace(CatalogServices, tab == 1 ? Products : []);
            _catalogPage = page;
            _hasCatalogNext = next;
            NotifyCatalogPage();
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception) { if (IsCurrent()) ErrorMessage = tab == 1 ? "Não foi possível pesquisar serviços." : "Não foi possível pesquisar produtos."; }
        finally { if (generation == _catalogGeneration) IsCatalogBusy = false; }
    }

    private QuoteProductOption ToProductOption(ProductDTO product)
    {
        var reference = product.Barcode ?? product.ProductID.ToString(CultureInfo.InvariantCulture);
        var unit = product.StockUnitID is int unitId ? _units.GetValueOrDefault(unitId)?.Code ?? string.Empty : string.Empty;
        return new QuoteProductOption(product, $"{product.Description ?? "Sem descrição"} · Produto · {reference}", unit);
    }

    private static QuoteProductOption ToServiceOption(ServiceDTO service) =>
        new(service, $"{service.Description ?? "Sem descrição"} · Serviço · {service.ServiceID}");

    private QuoteProductOption? GetKnownOption(QuoteItemDTO item) => item.ServiceID is int serviceId
        ? _knownServices.GetValueOrDefault(serviceId)
        : item.ProductID is int productId ? _knownProducts.GetValueOrDefault(productId) : null;

    private async Task CreateSectionAsync()
    {
        IsBusy = true;
        try
        {
            var sectionType = SectionTypeName == "Sob medida" ? QuoteSectionType.Custom : QuoteSectionType.Catalog;
            await _quoteService.CreateSectionAsync(CurrentQuoteId, sectionType);
            await ReloadQuoteAsync(SelectedSection?.DTO.QuoteSectionID);
            StatusMessage = "Seção criada.";
        }
        catch (UnauthorizedAccessException) { ErrorMessage = "Você não possui permissão para criar seções."; }
        catch (Exception) { ErrorMessage = "Não foi possível criar a seção."; }
        finally { IsBusy = false; }
    }

    private async Task UpdateSectionStatusAsync(QuoteSectionStatus status)
    {
        if (SelectedSection is null) return;
        IsBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            var sectionId = SelectedSection.DTO.QuoteSectionID;
            await _quoteService.UpdateSectionStatusAsync(CurrentQuoteId, sectionId, status);
            await ReloadQuoteAsync(sectionId);
            StatusMessage = status switch
            {
                QuoteSectionStatus.AwaitingQuotation => "Seção encaminhada para cotação.",
                QuoteSectionStatus.Sent => "Seção enviada ao cliente.",
                QuoteSectionStatus.Approved => "Seção aprovada e pronta para conversão em venda.",
                QuoteSectionStatus.Rejected => "Seção rejeitada.",
                _ => "Estado da seção atualizado."
            };
        }
        catch (System.ComponentModel.DataAnnotations.ValidationException exception) { ErrorMessage = exception.Message; }
        catch (UnauthorizedAccessException) { ErrorMessage = "Você não possui permissão para alterar o estado da seção."; }
        catch (Exception) { ErrorMessage = "Não foi possível alterar o estado da seção."; }
        finally { IsBusy = false; }
    }

    private async Task SaveLineAsync()
    {
        if (SelectedSection is null || SelectedProduct is null) return;
        if (SelectedProduct.DTO?.StockUnitID is not null && SelectedQuantityUnit is null)
        {
            ErrorMessage = "A unidade de medida do produto não foi encontrada.";
            return;
        }
        if (!TryParseDecimal(QuantityInput, out var quantity) || !QuoteQuantityRules.IsValid(quantity, QuantityDecimalPlaces))
        {
            ErrorMessage = $"Informe uma quantidade positiva, no máximo {QuantityMaximum}, com até {QuantityDecimalPlaces} casas decimais.";
            return;
        }
        if (!TryParseDecimal(UnitPriceInput, out var unitPrice) || unitPrice < 0 || unitPrice > 999999999.99m || decimal.Round(unitPrice, 2) != unitPrice)
        {
            ErrorMessage = "Informe um preço unitário válido, igual ou maior que zero.";
            return;
        }

        IsBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            await _quoteService.SaveQuoteItemAsync(CurrentQuoteId,
                new QuoteItemDTO(SelectedLine?.DTO.QuoteItemID ?? 0, SelectedSection.DTO.QuoteSectionID,
                    SelectedProduct.ProductID, quantity, unitPrice, HasInstallationService, ServiceID: SelectedProduct.ServiceID));
            var sectionId = SelectedSection.DTO.QuoteSectionID;
            await ReloadQuoteAsync(sectionId);
            SelectedLine = null;
            QuantityInput = "1";
            UnitPriceInput = string.Empty;
            HasInstallationService = false;
            StatusMessage = "Item do orçamento salvo.";
        }
        catch (System.ComponentModel.DataAnnotations.ValidationException exception) { ErrorMessage = exception.Message; }
        catch (UnauthorizedAccessException) { ErrorMessage = "Você não possui permissão para alterar itens."; }
        catch (Exception) { ErrorMessage = "Não foi possível salvar o item do orçamento."; }
        finally { IsBusy = false; }
    }

    private async Task ReloadQuoteAsync(int? sectionId = null)
    {
        _editingQuote = await _quoteService.GetQuoteByIdAsync(CurrentQuoteId);
        await LoadKnownProductsAsync(_editingQuote);
        LoadSections(_editingQuote, sectionId);
        OnPropertyChanged(nameof(CurrentQuoteId));
        OnPropertyChanged(nameof(QuoteTotal));
    }

    private async Task LoadKnownProductsAsync(QuoteDTO quote)
    {
        foreach (var productId in (quote.Sections ?? []).SelectMany(section => section.Items ?? [])
                     .Select(item => item.ProductID).OfType<int>().Distinct().Where(productId => !_knownProducts.ContainsKey(productId)))
        {
            var product = await _productService.GetProductByIdAsync(productId);
            if (product is not null)
                _knownProducts[product.ProductID] = ToProductOption(product);
        }
            foreach (var serviceId in (quote.Sections ?? []).SelectMany(section => section.Items ?? [])
                     .Select(item => item.ServiceID).OfType<int>().Distinct().Where(serviceId => !_knownServices.ContainsKey(serviceId)))
            {
                var catalog = _serviceCatalogService ?? throw new InvalidOperationException("O catálogo de serviços não está disponível.");
                var service = await catalog.GetServiceByIdAsync(serviceId);
                if (service is not null) _knownServices[service.ServiceID] = ToServiceOption(service);
            }
    }

    private void LoadSections(QuoteDTO quote, int? sectionId = null)
    {
        Sections.Clear();
        ReviewLines.Clear();
        AllLines.Clear();
        foreach (var section in quote.Sections ?? [])
        {
            var sectionType = section.SectionType == (int)QuoteSectionType.Custom ? "Sob medida" : "Catálogo";
            var sectionLabel = $"Seção {section.QuoteSectionID} · {sectionType} · {FormatSectionStatus(section.Status)}";
            Sections.Add(new QuoteSectionOption(section, sectionLabel));
            foreach (var item in section.Items ?? [])
            {
                var productName = GetKnownOption(item)?.Description
                    ?? $"{(item.ServiceID is not null ? "Serviço" : "Produto")} #{item.ServiceID ?? item.ProductID}";
                var unitPrice = item.UnitPrice ?? 0m;
                ReviewLines.Add(new QuoteReviewLine(sectionLabel, productName, item.Quantity, unitPrice, item.Quantity * unitPrice));
                var category = item.ServiceID is not null ? "Serviço" : "Produto";
                AllLines.Add(new QuoteLineOption(item, productName, item.Quantity * unitPrice, AllLines.Count + 1, category));
            }
        }
        SelectedSection = Sections.FirstOrDefault(section => section.DTO.QuoteSectionID == sectionId)
            ?? Sections.FirstOrDefault();
        OnPropertyChanged(nameof(QuoteTotal));
        OnPropertyChanged(nameof(ItemCount));
        OnPropertyChanged(nameof(NetTotal));
        OnPropertyChanged(nameof(IsQuoteOpen));
        OnPropertyChanged(nameof(IsQuoteCancelled));
        OnPropertyChanged(nameof(IsQuoteConverted));
        OnPropertyChanged(nameof(QuoteState));
        OnPropertyChanged(nameof(QuoteDate));
        RefreshCommands();
    }

    private void LoadSectionLines()
    {
        SectionItems.Clear();
        SelectedLine = null;
        SelectedProduct = null;
        QuantityInput = "1";
        UnitPriceInput = string.Empty;
        HasInstallationService = false;
        foreach (var item in SelectedSection?.DTO.Items ?? [])
        {
            var category = item.ServiceID is not null ? "Serviço" : "Produto";
            var productName = GetKnownOption(item)?.Description ?? $"{category} #{item.ServiceID ?? item.ProductID}";
            SectionItems.Add(new QuoteLineOption(item, productName, item.Quantity * (item.UnitPrice ?? 0m),
                SectionItems.Count + 1, category));
        }
        OnPropertyChanged(nameof(SectionTotal));
    }

    private static bool TryParseDecimal(string input, out decimal value) =>
        decimal.TryParse(input, NumberStyles.Number, CultureInfo.CurrentCulture, out value)
        || decimal.TryParse(input, NumberStyles.Number, CultureInfo.InvariantCulture, out value);

    private static string FormatSectionStatus(int status) => (QuoteSectionStatus)status switch
    {
        QuoteSectionStatus.Draft => "Rascunho",
        QuoteSectionStatus.AwaitingQuotation => "Aguardando cotação",
        QuoteSectionStatus.Sent => "Enviada",
        QuoteSectionStatus.Approved => "Aprovada",
        QuoteSectionStatus.Rejected => "Rejeitada",
        QuoteSectionStatus.ConvertedToOrder => "Convertida em pedido",
        _ => "Desconhecido"
    };

    private void NotifyQuantityUnit()
    {
        OnPropertyChanged(nameof(QuantityDecimalPlaces));
        OnPropertyChanged(nameof(QuantityFormat));
        OnPropertyChanged(nameof(QuantityMinimum));
        OnPropertyChanged(nameof(QuantityMaximum));
        OnPropertyChanged(nameof(QuantityUnitLabel));
        OnPropertyChanged(nameof(QuantityUnitDescription));
    }

    private async Task LoadUnitsAsync()
    {
        var units = new Dictionary<int, UnitOfMeasureDTO>();
        const int pageSize = 100;
        for (var page = 1; ; page++)
        {
            var batch = (await _quoteService.GetQuantityUnitsAsync(page, pageSize)).ToArray();
            foreach (var unit in batch) units[unit.UnitOfMeasureID] = unit;
            if (batch.Length < pageSize) break;
        }
        _units = units;
        NotifyQuantityUnit();
    }

    public async Task BeginNewAsync()
    {
        _editingQuote = null;
        _isNew = true;
        CustomerSearchText = EmployeeSearchText = PartnerSearchText = Notes = string.Empty;
        SelectedCustomer = null;
        SelectedEmployee = null;
        SelectedPartner = null;
        Sections.Clear();
        SectionItems.Clear();
        SourceType = "Própria";
        ClearCatalog();
        ErrorMessage = string.Empty;
        SelectedItem = null;
        DiscountInput = "0";
        AllLines.Clear();
        IsBusy = true;
        try
        {
            await LoadUnitsAsync();
            await SearchEmployeesAsync();
            SelectedEmployee = Employees.FirstOrDefault(employee => employee.UserID == _authenticatedUserContext.User?.UserID);
            _editingQuote = await _quoteService.CreateOpenQuoteAsync(SelectedEmployee?.EmployeeID, _authenticatedUserContext.User?.UserID);
            SetQuoteDate(_editingQuote.CreatedAt);
            await LoadKnownProductsAsync(_editingQuote);
            LoadSections(_editingQuote);
            OnPropertyChanged(nameof(CurrentQuoteId));
            IsEditing = true;
            StatusMessage = $"Lançando orçamento {CurrentQuoteId}.";
        }
        catch (Exception exception) { ErrorMessage = exception.Message; StatusMessage = "Não foi possível abrir o orçamento."; }
        finally { IsBusy = false; }
    }

    public async Task BeginEditAsync()
    {
        if (SelectedItem is null) return;
        ClearCatalog();
        IsBusy = true;
        try
        {
            await LoadUnitsAsync();
            _editingQuote = await _quoteService.GetQuoteByIdAsync(SelectedItem.QuoteID);
            OnPropertyChanged(nameof(CreatedByDisplay));
            _isNew = false;
            CustomerSearchText = _editingQuote.CustomerID.ToString(CultureInfo.InvariantCulture);
            EmployeeSearchText = _editingQuote.CreatedByEmployeeID.ToString(CultureInfo.InvariantCulture);
            PartnerSearchText = _editingQuote.SourcePartnerID?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
            await SearchCustomersAsync();
            await SearchEmployeesAsync();
            if (_editingQuote.SourcePartnerID is not null)
                await SearchPartnersAsync();
            SelectedCustomer = Customers.FirstOrDefault(item => item.CustomerID == _editingQuote.CustomerID);
            SelectedEmployee = Employees.FirstOrDefault(item => item.EmployeeID == _editingQuote.CreatedByEmployeeID);
            SelectedPartner = _editingQuote.SourcePartnerID is int partnerId
                ? Partners.FirstOrDefault(item => item.PartnerID == partnerId)
                : null;
            SourceType = FormatSourceType(_editingQuote.SourceType);
            SetQuoteDate(_editingQuote.CreatedAt);
            Notes = _editingQuote.Notes ?? string.Empty;
            DiscountInput = _editingQuote.DiscountAmount.ToString(CultureInfo.CurrentCulture);
            await ReloadQuoteAsync();
            OnPropertyChanged(nameof(CurrentQuoteId));
            ErrorMessage = string.Empty;
            IsEditing = true;
            StatusMessage = $"Editando orçamento {_editingQuote.QuoteID}.";
        }
        catch (Exception exception) { ErrorMessage = exception.Message; }
        finally { IsBusy = false; }
    }

    public void CancelEdit()
    {
        ClearCatalog();
        IsEditing = false;
        _isNew = false;
        _editingQuote = null;
        SelectedItem = null;
        ErrorMessage = string.Empty;
        StatusMessage = string.Empty;
    }

    public async Task ReturnToListAsync()
    {
        CancelEdit();
        SearchText = string.Empty;
        await LoadAsync();
    }

    private void SetQuoteDate(DateTime date)
    {
        _quoteDate = date;
        OnPropertyChanged(nameof(QuoteDate));
        OnPropertyChanged(nameof(SelectedQuoteDate));
    }

    private static string FormatSourceType(int source) => source switch
    {
        2 => "Loja",
        3 => "Outra",
        _ => "Própria"
    };

    private static string FormatOrderReferences(IEnumerable<int?> orderIds)
    {
        var references = orderIds.Where(orderId => orderId is > 0).Select(orderId => orderId!.Value).Distinct().Order().ToArray();
        return references.Length == 0 ? "PEDIDO CONVERTIDO" : $"Ref. Pedido Nº {string.Join(", ", references)}";
    }

    private void ClearSearch()
    {
        SearchText = string.Empty;
        SelectedItem = null;
    }

    private async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            await LoadListingAsync();
            StatusMessage = Items.Count == 0 ? "Nenhum orçamento encontrado." : $"{Items.Count} orçamento(s).";
        }
        catch (UnauthorizedAccessException) { StatusMessage = "Você não possui permissão para consultar orçamentos."; }
        catch (Exception) { StatusMessage = "Não foi possível carregar os orçamentos."; }
        finally { IsBusy = false; }
    }

    private async Task LoadListingAsync()
    {
        var all = new List<QuoteDTO>();
        var page = 1;
        while (true)
        {
            var results = (await _quoteService.SearchQuotesAsync(SearchText, page++, 500)).ToArray();
            all.AddRange(results);
            if (results.Length < 500) break;
        }
        SelectedItem = null;
        Listing.Load(all.Select(quote => new QuoteListItem(quote.QuoteID, quote.CustomerID,
            quote.CreatedByEmployeeID, FormatSourceType(quote.SourceType),
            quote.SourcePartnerID, quote.CreatedAt, quote.Notes, quote.CustomerName ?? "(sem nome)",
            quote.CreatedByEmployeeName ?? "(sem nome)", quote.ListStatus ?? "ABERTO", quote.ListTotal ?? 0m)));
    }

    private async Task<bool> SaveAsync()
    {
        if (!TryParseDecimal(DiscountInput, out var discount) || discount < 0 || discount > QuoteTotal || decimal.Round(discount, 2) != discount)
        {
            ErrorMessage = "Informe um desconto entre zero e o subtotal do orçamento.";
            return false;
        }
        if (SelectedCustomer is null || SelectedEmployee is null)
        {
            ErrorMessage = "Selecione o cliente e o vendedor nas opções pesquisadas.";
            return false;
        }

        IsBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            var dto = new QuoteDTO(_editingQuote?.QuoteID ?? 0, SelectedCustomer.CustomerID, SelectedEmployee.EmployeeID,
                SelectedPartner?.PartnerID ?? _editingQuote?.SourcePartnerID,
                SourceType == "Loja" ? 2 : SourceType == "Outra" ? 3 : 1, _quoteDate,
                string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim(), null,
                _editingQuote is null ? _authenticatedUserContext.User?.UserID : _editingQuote.CreatedByUserID,
                discount);
            var quoteId = await _quoteService.SaveQuoteAsync(dto);
            _editingQuote = await _quoteService.GetQuoteByIdAsync(quoteId);
            OnPropertyChanged(nameof(CreatedByDisplay));
            LoadSections(_editingQuote);
            await LoadListingAsync();
            OnPropertyChanged(nameof(CurrentQuoteId));
            OnPropertyChanged(nameof(QuoteTotal));
            StatusMessage = "Orçamento salvo.";
            _isNew = false;
            return true;
        }
        catch (System.ComponentModel.DataAnnotations.ValidationException exception) { ErrorMessage = exception.Message; StatusMessage = "Verifique os dados informados."; return false; }
        catch (UnauthorizedAccessException) { ErrorMessage = "Você não possui permissão para salvar orçamentos."; return false; }
        catch (Exception) { ErrorMessage = "Não foi possível salvar o orçamento."; return false; }
        finally { IsBusy = false; }
    }

    private void RefreshCommands()
    {
        CatalogPagination.Refresh();
        foreach (var command in new[] { SearchCommand, ClearSearchCommand, NewCommand, EditCommand,
             SaveCommand, CancelCommand, CreateSectionCommand, SaveLineCommand,
                 RequestQuotationCommand, SendSectionCommand, ApproveSectionCommand, RejectSectionCommand,
                 ClearProductsCommand, SearchProductsCommand, CatalogPreviousCommand, CatalogNextCommand,
                 ClearCustomerCommand, ClearEmployeeCommand, NewLineCommand,
                 GeneratePdfCommand, CancelQuoteCommand, ConvertToOrderCommand })
            if (command is RelayCommand relayCommand) relayCommand.RaiseCanExecuteChanged();
        OnPropertyChanged(nameof(CanSearch)); OnPropertyChanged(nameof(CanNew)); OnPropertyChanged(nameof(CanEdit));
        OnPropertyChanged(nameof(CanSave)); OnPropertyChanged(nameof(CanCancel));
        OnPropertyChanged(nameof(CanManageSections)); OnPropertyChanged(nameof(CanManageLines));
        OnPropertyChanged(nameof(CanRequestQuotation)); OnPropertyChanged(nameof(CanSendSection));
        OnPropertyChanged(nameof(CanApproveSection)); OnPropertyChanged(nameof(CanRejectSection));
        OnPropertyChanged(nameof(ItemCount));
    }

    public sealed class CatalogPaginationState : IStatusBarSource
    {
        private readonly QuotesViewModel _owner;
        private int _pageSize = 25;

        internal CatalogPaginationState(QuotesViewModel owner)
        {
            _owner = owner;
            FirstPageCommand = new RelayCommand(async () => await owner.SearchCatalogPageAsync(1), () => HasFirstPage);
            PreviousPageCommand = new RelayCommand(async () => await owner.SearchCatalogPageAsync(owner._catalogPage - 1), () => HasPreviousPage);
            NextPageCommand = new RelayCommand(async () => await owner.SearchCatalogPageAsync(owner._catalogPage + 1), () => HasNextPage);
            LastPageCommand = new RelayCommand(() => { }, () => false);
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private bool CanNavigate => _owner.IsEditing && !_owner.IsBusy && !_owner.IsCatalogBusy;
        public string StatusMessage => string.Empty;
        public string? StatusPrimary => PaginationStatus;
        public string? StatusSecondary => null;
        public bool HasStatusPrimary => HasPagination;
        public bool HasStatusSecondary => false;
        public bool HasPagination => _owner.IsEditing;
        public string? PaginationStatus => HasPagination
            ? _owner.Products.Count == 0 ? "Página 0 de 0" : _owner.CatalogPageDisplay
            : null;
        public string? PaginationPageStatus => PaginationStatus;
        public bool HasPreviousPage => CanNavigate && _owner.Products.Count > 0 && _owner.HasCatalogPrevious;
        public bool HasNextPage => CanNavigate && _owner.Products.Count > 0 && _owner.HasCatalogNext;
        public bool HasFirstPage => HasPreviousPage;
        public bool HasLastPage => false;
        public ICommand FirstPageCommand { get; }
        public ICommand PreviousPageCommand { get; }
        public ICommand NextPageCommand { get; }
        public ICommand LastPageCommand { get; }
        public IReadOnlyList<int> PageSizeOptions { get; } = new[] { 10, 25, 50, 100 };
        public int SelectedPageSize
        {
            get => _pageSize;
            set
            {
                if (_owner.IsBusy || !PageSizeOptions.Contains(value) || value == _pageSize) return;
                _pageSize = value;
                _owner._catalogPage = 1;
                _owner._hasCatalogNext = false;
                _owner.NotifyCatalogPage();
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedPageSize)));
                if (_owner.IsEditing && !string.IsNullOrWhiteSpace(_owner.ProductSearchText))
                    _ = _owner.SearchProductsAsync();
            }
        }

        internal void Refresh()
        {
            foreach (var name in new[] { nameof(HasPagination), nameof(PaginationStatus), nameof(PaginationPageStatus),
                         nameof(StatusPrimary), nameof(HasStatusPrimary), nameof(HasPreviousPage), nameof(HasNextPage),
                         nameof(HasFirstPage), nameof(HasLastPage) })
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
            foreach (var command in new[] { FirstPageCommand, PreviousPageCommand, NextPageCommand, LastPageCommand })
                ((RelayCommand)command).RaiseCanExecuteChanged();
        }
    }

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> source)
    {
        target.Clear();
        foreach (var item in source) target.Add(item);
    }

    private void ResetLine()
    {
        SelectedLine = null;
        SelectedProduct = null;
        QuantityInput = "1";
        UnitPriceInput = string.Empty;
        HasInstallationService = false;
        SelectedSection = Sections.FirstOrDefault(section => section.DTO.Status == (int)QuoteSectionStatus.Draft);
    }

    public void ReportPdfResult(string? error = null)
    {
        ErrorMessage = error ?? string.Empty;
        StatusMessage = error is null ? "PDF gerado." : "Não foi possível gerar o PDF.";
    }

    private async Task CancelQuoteAsync()
    {
        IsBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            await _quoteService.CancelQuoteAsync(CurrentQuoteId);
            await ReloadQuoteAsync();
            await LoadListingAsync();
            StatusMessage = "Orçamento cancelado.";
        }
        catch (Exception exception) { ErrorMessage = exception.Message; }
        finally { IsBusy = false; }
    }

    private async Task ConvertToOrderAsync()
    {
        if (_orderService is null || !await SaveAsync()) return;
        IsBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            var orders = new List<int>();
            foreach (var section in Sections.ToArray().Where(section => section.DTO.Status != (int)QuoteSectionStatus.Rejected
                         && section.DTO.Status != (int)QuoteSectionStatus.ConvertedToOrder))
            {
                if (!(section.DTO.Items ?? []).Any()) continue;
                var status = (QuoteSectionStatus)section.DTO.Status;
                if (status == QuoteSectionStatus.Draft)
                    await _quoteService.UpdateSectionStatusAsync(CurrentQuoteId, section.DTO.QuoteSectionID, QuoteSectionStatus.AwaitingQuotation);
                if (status is QuoteSectionStatus.Draft or QuoteSectionStatus.AwaitingQuotation)
                    await _quoteService.UpdateSectionStatusAsync(CurrentQuoteId, section.DTO.QuoteSectionID, QuoteSectionStatus.Sent);
                if (status is not QuoteSectionStatus.Approved)
                    await _quoteService.UpdateSectionStatusAsync(CurrentQuoteId, section.DTO.QuoteSectionID, QuoteSectionStatus.Approved);
                var order = await _orderService.ConvertFromQuoteAsync(section.DTO.QuoteSectionID);
                orders.Add(order.OrderID);
            }
            await ReloadQuoteAsync();
            await LoadListingAsync();
            StatusMessage = $"Pedido(s) criado(s): {string.Join(", ", orders)}.";
        }
        catch (Exception exception) { ErrorMessage = exception.Message; await ReloadQuoteAsync(); }
        finally { IsBusy = false; }
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