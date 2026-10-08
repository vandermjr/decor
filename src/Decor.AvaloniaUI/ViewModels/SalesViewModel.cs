using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Decor.Core.Common;
using Decor.Core.DTOs;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Services;

namespace Decor.AvaloniaUI.ViewModels;

public sealed record SaleListItem(int OrderID, int QuoteSectionID, int CustomerID, string OrderType,
    string Status, string DownPayment, DateTime CreatedAt);
public sealed record PaymentPlanEntry(int PaymentMethodID, string PaymentMethodName, decimal Amount, DateTime DueDate);

public sealed class SalesViewModel : IStatusBarSource
{
    private readonly IOrderService _orderService;
    private readonly IAuthorizationService _authorizationService;
    private readonly IOrderInstallmentService _installmentService;
    private readonly IPaymentMethodService _paymentMethodService;
    private readonly ObservableCollection<SaleListItem> _items = [];
    private readonly ObservableCollection<PaymentMethodDTO> _paymentMethods = [];
    private readonly ObservableCollection<OrderInstallmentDTO> _installments = [];
    private readonly ObservableCollection<PaymentPlanEntry> _planEntries = [];
    private readonly Dictionary<int, OrderDTO> _ordersById = [];
    private SaleListItem? _selectedItem;
    private string _searchText = string.Empty;
    private string _sectionIdInput = string.Empty;
    private string _statusMessage = string.Empty;
    private string _errorMessage = string.Empty;
    private bool _isBusy;
    private bool _requiresDownPayment;
    private DateTimeOffset? _manufacturingDeadline;
    private DateTimeOffset? _installationDeadline;
    private bool _showCancelConfirmation;
    private PaymentMethodDTO? _selectedPaymentMethod;
    private PaymentPlanEntry? _selectedPlanEntry;
    private string _installmentAmountInput = string.Empty;
    private DateTimeOffset? _installmentDueDate = DateTimeOffset.Now.Date;
    private OrderDTO? _selectedOrderDetails;
    private int _selectionLoadVersion;

    public SalesViewModel(IOrderService orderService, IAuthorizationService authorizationService,
        IOrderInstallmentService installmentService, IPaymentMethodService paymentMethodService)
    {
        _orderService = orderService;
        _authorizationService = authorizationService;
        _installmentService = installmentService;
        _paymentMethodService = paymentMethodService;
        Listing = new GridListState<SaleListItem>(_items, () => true);
        Listing.PropertyChanged += (_, args) => OnPropertyChanged(args.PropertyName);
        SearchCommand = new RelayCommand(async () => await LoadAsync(), () => CanSearch);
        ClearSearchCommand = new RelayCommand(ClearSearch, () => CanSearch);
        ConvertCommand = new RelayCommand(async () => await ConvertAsync(), () => CanConvert);
        ApproveCommand = new RelayCommand(async () => await ApproveAsync(), () => CanApprove);
        CancelCommand = new RelayCommand(BeginCancel, () => CanCancel);
        ConfirmCancelCommand = new RelayCommand(async () => await ConfirmCancelAsync(), () => !IsBusy);
        DismissCancelCommand = new RelayCommand(() => ShowCancelConfirmation = false);
        AddInstallmentCommand = new RelayCommand(AddInstallment, () => CanEditPaymentPlan);
        RemoveInstallmentCommand = new RelayCommand(RemoveInstallment, () => SelectedPlanEntry is not null && !IsBusy);
        CreatePaymentPlanCommand = new RelayCommand(async () => await CreatePaymentPlanAsync(), () => CanCreatePaymentPlan);
    }

    public GridListState<SaleListItem> Listing { get; }
    public ObservableCollection<SaleListItem> Items => _items;
    public ICommand SearchCommand { get; }
    public ICommand ClearSearchCommand { get; }
    public ICommand ConvertCommand { get; }
    public ICommand ApproveCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand ConfirmCancelCommand { get; }
    public ICommand DismissCancelCommand { get; }
    public ICommand AddInstallmentCommand { get; }
    public ICommand RemoveInstallmentCommand { get; }
    public ICommand CreatePaymentPlanCommand { get; }
    public string SearchText { get => _searchText; set => SetField(ref _searchText, value); }
    public string SectionIdInput { get => _sectionIdInput; set => SetField(ref _sectionIdInput, value); }
    public string StatusMessage { get => _statusMessage; private set => SetField(ref _statusMessage, value); }
    public string ErrorMessage { get => _errorMessage; private set { if (SetField(ref _errorMessage, value)) OnPropertyChanged(nameof(HasError)); } }
    public ObservableCollection<PaymentMethodDTO> PaymentMethods => _paymentMethods;
    public ObservableCollection<OrderInstallmentDTO> Installments => _installments;
    public ObservableCollection<PaymentPlanEntry> PlanEntries => _planEntries;
    public PaymentMethodDTO? SelectedPaymentMethod { get => _selectedPaymentMethod; set { if (SetField(ref _selectedPaymentMethod, value)) RefreshCommands(); } }
    public PaymentPlanEntry? SelectedPlanEntry { get => _selectedPlanEntry; set { if (SetField(ref _selectedPlanEntry, value)) RefreshCommands(); } }
    public string InstallmentAmountInput { get => _installmentAmountInput; set => SetField(ref _installmentAmountInput, value); }
    public DateTimeOffset? InstallmentDueDate { get => _installmentDueDate; set => SetField(ref _installmentDueDate, value); }
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool RequiresDownPayment { get => _requiresDownPayment; set => SetField(ref _requiresDownPayment, value); }
    public DateTimeOffset? ManufacturingDeadline { get => _manufacturingDeadline; set => SetField(ref _manufacturingDeadline, value); }
    public DateTimeOffset? InstallationDeadline { get => _installationDeadline; set => SetField(ref _installationDeadline, value); }
    public bool ShowCancelConfirmation { get => _showCancelConfirmation; private set => SetField(ref _showCancelConfirmation, value); }
    public SaleListItem? SelectedItem
    {
        get => _selectedItem;
        set
        {
            if (!SetField(ref _selectedItem, value)) return;
            var version = ++_selectionLoadVersion;
            var order = SelectedOrder;
            RequiresDownPayment = order?.RequiresDownPayment ?? false;
            ManufacturingDeadline = ToDatePickerValue(order?.ManufacturingDeadline);
            InstallationDeadline = ToDatePickerValue(order?.InstallationDeadline);
            _selectedOrderDetails = null;
            _installments.Clear();
            _paymentMethods.Clear();
            _planEntries.Clear();
            SelectedPaymentMethod = null;
            SelectedPlanEntry = null;
            OnPropertyChanged(nameof(HasSelectedOrder));
            OnPropertyChanged(nameof(SelectedOrderTotal));
            OnPropertyChanged(nameof(HasInstallmentPlan));
            OnPropertyChanged(nameof(PaymentPlanTotal));
            RefreshCommands();
            if (value is not null) _ = LoadSelectedOrderAsync(value.OrderID, version);
        }
    }
    public bool IsBusy { get => _isBusy; private set { if (SetField(ref _isBusy, value)) RefreshCommands(); } }
    private OrderDTO? SelectedOrder => SelectedItem is not null && _ordersById.TryGetValue(SelectedItem.OrderID, out var order) ? order : null;
    public bool CanSearch => _authorizationService.HasPermission(DecorPermissions.OrdersView) && !IsBusy;
    public bool CanConvert => _authorizationService.HasPermission(DecorPermissions.OrdersConvertFromQuote) && !IsBusy;
    public bool CanApprove => _authorizationService.HasPermission(DecorPermissions.OrdersApprove) && SelectedOrder?.Status == (int)OrderStatus.PendingApproval && !IsBusy;
    public bool CanCancel => _authorizationService.HasPermission(DecorPermissions.OrdersCancel)
        && SelectedOrder?.Status is (int)OrderStatus.PendingApproval or (int)OrderStatus.Approved && !IsBusy;
    public bool HasSelectedOrder => SelectedItem is not null;
    public decimal SelectedOrderTotal => (_selectedOrderDetails?.Items ?? []).Sum(item => item.Quantity * item.UnitPrice);
    public decimal PaymentPlanTotal => _planEntries.Sum(entry => entry.Amount);
    public bool HasInstallmentPlan => _installments.Count > 0;
    public bool HasSelectedPlanEntry => SelectedPlanEntry is not null;
    public bool CanConfigurePaymentPlan => SelectedOrder?.Status == (int)OrderStatus.Approved
        && _installments.Count == 0 && !IsBusy
        && _authorizationService.HasPermission(DecorPermissions.OrderInstallmentsCreatePlan)
        && _authorizationService.HasPermission(DecorPermissions.OrderInstallmentsView)
        && _authorizationService.HasPermission(DecorPermissions.PaymentMethodsView);
    public bool CanEditPaymentPlan => CanConfigurePaymentPlan && SelectedPaymentMethod is not null;
    public bool CanCreatePaymentPlan => CanConfigurePaymentPlan && _planEntries.Count > 0
        && Math.Abs(PaymentPlanTotal - SelectedOrderTotal) <= 0.01m;
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

    private void ClearSearch()
    {
        SearchText = string.Empty;
        SelectedItem = null;
        Listing.Clear();
        _ordersById.Clear();
        StatusMessage = "Pesquisa limpa.";
    }

    private async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            var all = new List<OrderDTO>();
            var page = 1;
            while (true)
            {
                var results = (await _orderService.SearchOrdersAsync(SearchText, page++, 500)).ToArray();
                all.AddRange(results);
                if (results.Length < 500) break;
            }
            _ordersById.Clear();
            foreach (var order in all) _ordersById[order.OrderID] = order;
            SelectedItem = null;
            Listing.Load(all.Select(order => new SaleListItem(order.OrderID, order.QuoteSectionID, order.CustomerID,
                order.OrderType == (int)OrderType.Custom ? "Sob encomenda" : "Catálogo", FormatStatus(order.Status),
                order.RequiresDownPayment is null ? "Pendente" : order.RequiresDownPayment.Value ? "Sim" : "Não", order.CreatedAt)));
            StatusMessage = all.Count == 0 ? "Nenhuma venda encontrada." : $"{all.Count} venda(s).";
        }
        catch (UnauthorizedAccessException) { StatusMessage = "Você não possui permissão para consultar vendas."; }
        catch (Exception) { StatusMessage = "Não foi possível carregar as vendas."; }
        finally { IsBusy = false; }
    }

    private async Task LoadSelectedOrderAsync(int orderId, int version)
    {
        try
        {
            var order = await _orderService.GetOrderByIdAsync(orderId);
            if (version != _selectionLoadVersion) return;
            _selectedOrderDetails = order;
            _ordersById[orderId] = order;
            OnPropertyChanged(nameof(SelectedOrderTotal));

            if (_authorizationService.HasPermission(DecorPermissions.OrderInstallmentsView))
            {
                var installments = await _installmentService.GetInstallmentsByOrderIdAsync(orderId);
                if (version != _selectionLoadVersion) return;
                Replace(_installments, installments);
            }
            if (_authorizationService.HasPermission(DecorPermissions.PaymentMethodsView))
            {
                var methods = await _paymentMethodService.SearchPaymentMethodsAsync(string.Empty, 1, 500);
                if (version != _selectionLoadVersion) return;
                Replace(_paymentMethods, methods.Where(method => method.IsActive));
            }
            RefreshCommands();
            OnPropertyChanged(nameof(HasInstallmentPlan));
            OnPropertyChanged(nameof(CanConfigurePaymentPlan));
        }
        catch (UnauthorizedAccessException)
        {
            if (version == _selectionLoadVersion)
                ErrorMessage = "Sem permissão para consultar detalhes financeiros desta venda.";
        }
        catch (Exception)
        {
            if (version == _selectionLoadVersion)
                ErrorMessage = "Não foi possível carregar os detalhes da venda.";
        }
    }

    private void AddInstallment()
    {
        if (SelectedPaymentMethod is null || InstallmentDueDate is null
            || !TryParseAmount(InstallmentAmountInput, out var amount) || amount <= 0)
        {
            ErrorMessage = "Informe forma de pagamento, valor positivo e vencimento.";
            return;
        }
        _planEntries.Add(new PaymentPlanEntry(SelectedPaymentMethod.PaymentMethodID, SelectedPaymentMethod.Name,
            amount, InstallmentDueDate.Value.DateTime));
        InstallmentAmountInput = string.Empty;
        InstallmentDueDate = InstallmentDueDate.Value.AddMonths(1);
        ErrorMessage = string.Empty;
        OnPropertyChanged(nameof(PaymentPlanTotal));
        RefreshCommands();
    }

    private void RemoveInstallment()
    {
        if (SelectedPlanEntry is null) return;
        _planEntries.Remove(SelectedPlanEntry);
        SelectedPlanEntry = null;
        OnPropertyChanged(nameof(PaymentPlanTotal));
        RefreshCommands();
    }

    private async Task CreatePaymentPlanAsync()
    {
        if (SelectedOrder is not { } order) return;
        IsBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            var inputs = _planEntries.Select(entry => new InstallmentItemInputDTO(entry.PaymentMethodID, entry.Amount, entry.DueDate));
            var created = await _installmentService.CreateInstallmentPlanAsync(order.OrderID, inputs);
            Replace(_installments, created);
            _planEntries.Clear();
            StatusMessage = "Plano de pagamento criado.";
            OnPropertyChanged(nameof(HasInstallmentPlan));
            OnPropertyChanged(nameof(PaymentPlanTotal));
        }
        catch (System.ComponentModel.DataAnnotations.ValidationException exception) { ErrorMessage = exception.Message; }
        catch (UnauthorizedAccessException) { ErrorMessage = "Você não possui permissão para criar o plano de pagamento."; }
        catch (Exception) { ErrorMessage = "Não foi possível criar o plano de pagamento."; }
        finally { IsBusy = false; RefreshCommands(); }
    }

    private async Task ConvertAsync()
    {
        if (!int.TryParse(SectionIdInput, NumberStyles.Integer, CultureInfo.InvariantCulture, out var sectionId) || sectionId <= 0)
        {
            ErrorMessage = "Informe um código de seção válido.";
            return;
        }
        IsBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            var order = await _orderService.ConvertFromQuoteAsync(sectionId);
            StatusMessage = $"Seção convertida no pedido {order.OrderID}.";
            SectionIdInput = string.Empty;
            await LoadAsync();
        }
        catch (System.ComponentModel.DataAnnotations.ValidationException exception) { ErrorMessage = exception.Message; }
        catch (UnauthorizedAccessException) { ErrorMessage = "Você não possui permissão para converter seções."; }
        catch (Exception) { ErrorMessage = "Não foi possível converter a seção em venda."; }
        finally { IsBusy = false; }
    }

    private async Task ApproveAsync()
    {
        if (SelectedOrder is not { } order) return;
        IsBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            await _orderService.ApproveOrderAsync(order.OrderID, RequiresDownPayment,
                ManufacturingDeadline?.DateTime, InstallationDeadline?.DateTime);
            StatusMessage = $"Venda {order.OrderID} aprovada.";
            await LoadAsync();
        }
        catch (System.ComponentModel.DataAnnotations.ValidationException exception) { ErrorMessage = exception.Message; }
        catch (UnauthorizedAccessException) { ErrorMessage = "Você não possui permissão para aprovar vendas."; }
        catch (Exception) { ErrorMessage = "Não foi possível aprovar a venda."; }
        finally { IsBusy = false; }
    }

    private void BeginCancel()
    {
        if (SelectedOrder is not null) ShowCancelConfirmation = true;
    }

    private async Task ConfirmCancelAsync()
    {
        if (SelectedOrder is not { } order) return;
        IsBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            await _orderService.CancelOrderAsync(order.OrderID);
            ShowCancelConfirmation = false;
            StatusMessage = $"Venda {order.OrderID} cancelada.";
            await LoadAsync();
        }
        catch (System.ComponentModel.DataAnnotations.ValidationException exception) { ErrorMessage = exception.Message; }
        catch (UnauthorizedAccessException) { ErrorMessage = "Você não possui permissão para cancelar vendas."; }
        catch (Exception) { ErrorMessage = "Não foi possível cancelar a venda."; }
        finally { IsBusy = false; }
    }

    private void RefreshCommands()
    {
        foreach (var command in new[] { SearchCommand, ClearSearchCommand, ConvertCommand, ApproveCommand, CancelCommand,
                     ConfirmCancelCommand, AddInstallmentCommand, RemoveInstallmentCommand, CreatePaymentPlanCommand })
            if (command is RelayCommand relayCommand) relayCommand.RaiseCanExecuteChanged();
        OnPropertyChanged(nameof(CanSearch)); OnPropertyChanged(nameof(CanConvert));
        OnPropertyChanged(nameof(CanApprove)); OnPropertyChanged(nameof(CanCancel));
        OnPropertyChanged(nameof(CanConfigurePaymentPlan)); OnPropertyChanged(nameof(CanEditPaymentPlan));
        OnPropertyChanged(nameof(CanCreatePaymentPlan));
        OnPropertyChanged(nameof(HasSelectedPlanEntry));
    }

    private static bool TryParseAmount(string input, out decimal amount) =>
        decimal.TryParse(input, NumberStyles.Number, CultureInfo.CurrentCulture, out amount)
        || decimal.TryParse(input, NumberStyles.Number, CultureInfo.InvariantCulture, out amount);

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> source)
    {
        target.Clear();
        foreach (var item in source) target.Add(item);
    }

    private static string FormatStatus(int status) => status switch
    {
        (int)OrderStatus.PendingApproval => "Aguardando aprovação",
        (int)OrderStatus.Approved => "Aprovada",
        (int)OrderStatus.InProduction => "Em produção",
        (int)OrderStatus.ReadyForDelivery => "Pronta para entrega",
        (int)OrderStatus.PartiallyDelivered => "Parcialmente entregue",
        (int)OrderStatus.Delivered => "Entregue",
        (int)OrderStatus.Cancelled => "Cancelada",
        _ => "Desconhecido"
    };
    private static DateTimeOffset? ToDatePickerValue(DateTime? value) => value is null
        ? null
        : new DateTimeOffset(value.Value);
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