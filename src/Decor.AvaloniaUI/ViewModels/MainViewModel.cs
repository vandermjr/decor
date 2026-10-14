using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Decor.AvaloniaUI.Views;
using Decor.AvaloniaUI.Services;
using Decor.Core.Common;
using Decor.Core.Interfaces.Services;
using System.Windows.Input;

namespace Decor.AvaloniaUI.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly INavigationService _navigationService;
    private readonly IThemeService _themeService;
    private readonly IAuthenticatedUserContext _authenticatedUserContext;
    private readonly INotificationCenter? _notificationCenter;
    private WorkspaceDocumentViewModel? _activeDocument;
    private IStatusBarSource? _statusSource;
    private string _themeToolTip = string.Empty;

    public MainViewModel(
        INavigationService navigationService,
        IThemeService themeService,
        IAuthorizationService authorizationService,
        IAuthenticatedUserContext authenticatedUserContext,
        INotificationCenter? notificationCenter = null)
    {
        _navigationService = navigationService;
        _themeService = themeService;
        _authenticatedUserContext = authenticatedUserContext;
        _notificationCenter = notificationCenter;

        ShowProductsCommand = new RelayCommand(() => OpenSingletonDocument("products", "Produtos", () => CreateView<ProductsView>()));
        ShowServicesCommand = new RelayCommand(() =>
        {
            if (authorizationService.HasPermission(DecorPermissions.ServicesView))
                OpenSingletonDocument("services", "Servi\u00e7os", () => CreateView<ServicesView>());
        }, () => authorizationService.HasPermission(DecorPermissions.ServicesView));
        ShowBrandsCommand = new RelayCommand(() => OpenSingletonDocument("brands", "Marcas", () => CreateView<BrandsView>()));
        ShowUnitsOfMeasureCommand = new RelayCommand(() =>
        {
            if (authorizationService.HasPermission(DecorPermissions.UnitsOfMeasureView))
                OpenSingletonDocument("units-of-measure", "Unidades de Medida", () => CreateView<UnitsOfMeasureView>());
        }, () => authorizationService.HasPermission(DecorPermissions.UnitsOfMeasureView));
        ShowCustomersCommand = new RelayCommand(() => OpenSingletonDocument("customers", "Clientes", () => CreateView<CustomersView>()), () => authorizationService.HasPermission(DecorPermissions.CustomersView));
        ShowSuppliersCommand = new RelayCommand(() => OpenSingletonDocument("suppliers", "Fornecedores", () => CreateView<SuppliersView>()), () => authorizationService.HasPermission(DecorPermissions.SuppliersView));
        ShowPartnersCommand = new RelayCommand(() => OpenSingletonDocument("partners", "Parceiros", () => CreateView<PartnersView>()), () => authorizationService.HasPermission(DecorPermissions.PartnersView));
        ShowStockCommand = new RelayCommand(() => OpenSingletonDocument("stock", "Estoque", () => CreateView<StockView>()), () => authorizationService.HasPermission(DecorPermissions.StockMovementsView));
        ShowStockTransfersCommand = new RelayCommand(() => OpenSingletonDocument("stock-transfers", "Transferências", () => CreateView<StockTransfersView>()), () => authorizationService.HasPermission(DecorPermissions.StockMovementsTransfer));
        ShowStockAdjustmentsCommand = new RelayCommand(() => OpenSingletonDocument("stock-adjustments", "Ajustes", () => CreateView<StockAdjustmentsView>()), () => authorizationService.HasPermission(DecorPermissions.StockMovementsAdjust));
        ShowPurchaseOrdersCommand = new RelayCommand(() => OpenSingletonDocument("purchase-orders", "Pedidos de Compra", () => CreateView<PurchaseOrdersView>()), () => authorizationService.HasPermission(DecorPermissions.PurchaseOrdersView));
        ShowGoodsReceiptsCommand = new RelayCommand(() => OpenSingletonDocument("goods-receipts", "Recebimentos", () => CreateView<GoodsReceiptsView>()), () => authorizationService.HasPermission(DecorPermissions.GoodsReceiptsView));
        ShowQuotesCommand = new RelayCommand(() => OpenSingletonDocument("quotes", "Orçamentos", () => CreateView<QuotesView>()), () => authorizationService.HasPermission(DecorPermissions.QuotesView));
        ShowSalesCommand = new RelayCommand(() => OpenSingletonDocument("sales", "Vendas", () => CreateView<SalesView>()), () => authorizationService.HasPermission(DecorPermissions.OrdersView));
        ShowEmployeesCommand = new RelayCommand(() => OpenSingletonDocument("employees", "Funcionários", () => CreateView<EmployeesView>()), () => authorizationService.HasPermission(DecorPermissions.EmployeesView));
        ShowTermDeliveryCommand = new RelayCommand(() => OpenSingletonDocument("term-delivery", "Termo de Entrega", () => CreateView<TermDeliveryView>()));
        ShowClassificationsCommand = new RelayCommand(() => OpenSingletonDocument("classifications", "Classificações", () => CreateView<ClassificationsView>()));
        ChangePasswordCommand = new RelayCommand(RequestPasswordChange);
        ShowUsersCommand = new RelayCommand(() => OpenSingletonDocument("users", "Usuários", () => CreateView<UsersView>()), () => authorizationService.HasPermission(DecorPermissions.UsersView));
        ShowRolesCommand = new RelayCommand(() => OpenSingletonDocument("roles", "Grupos", () => CreateView<GroupsView>()), () => authorizationService.HasPermission(DecorPermissions.RolesView));
        ShowPermissionsCommand = new RelayCommand(() => OpenSingletonDocument("permissions", "Permissões", () => CreateView<PermissionsView>()), () => authorizationService.HasPermission(DecorPermissions.RolesView));
        ShowDatabaseMaintenanceCommand = new RelayCommand(() => OpenSingletonDocument("database-maintenance", "Backup e restauração", () => CreateView<DatabaseMaintenanceView>()), () => IsAdministrator() && authorizationService.HasPermission(DecorPermissions.DatabaseMaintenanceView));
        ShowSystemSettingsCommand = new RelayCommand(() => OpenSingletonDocument("system-options", "Opções", () => CreateView<IconCatalogView>()), () => IsAdministrator());
        ShowUserOptionsCommand = new RelayCommand(() => OpenSingletonDocument("user-options", "Preferências", () => CreateView<UserOptionsView>()));
        ShowAboutCommand = new RelayCommand(() => AboutRequested?.Invoke(this, EventArgs.Empty));
        UseLightThemeCommand = new RelayCommand(async () => await SetThemeAsync(DecorThemeStyle.Light));
        UseDarkThemeCommand = new RelayCommand(async () => await SetThemeAsync(DecorThemeStyle.Dark));
        ToggleThemeCommand = new RelayCommand(async () => await SetThemeAsync(_themeService.CurrentTheme == DecorThemeStyle.Dark ? DecorThemeStyle.Light : DecorThemeStyle.Dark));
        SignOutCommand = new RelayCommand(_authenticatedUserContext.SignOut);
        ClearNotificationsCommand = new RelayCommand(() => _notificationCenter?.Clear());

        var user = authenticatedUserContext.User;
        var username = user?.Username ?? string.Empty;
        UserUsername = username;
        UserPresentationName = SystemAccountDefaults.IsAdministrator(username)
            ? SystemAccountDefaults.AdministratorName
            : !string.IsNullOrWhiteSpace(user?.EmployeeName) ? user.EmployeeName : username;
        UserInitials = InitialsOf(UserPresentationName);

        OpenDocuments.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasOpenDocuments));
        if (_notificationCenter is not null)
            _notificationCenter.Notifications.CollectionChanged += OnNotificationsChanged;
        ApplyCurrentTheme();
    }

    public ObservableCollection<WorkspaceDocumentViewModel> OpenDocuments { get; } = [];

    public bool HasOpenDocuments => OpenDocuments.Count > 0;

    public WorkspaceDocumentViewModel? ActiveDocument
    {
        get => _activeDocument;
        set
        {
            if (ReferenceEquals(_activeDocument, value)) return;
            if (_activeDocument is not null)
                _activeDocument.IsActive = false;
            _activeDocument = value;
            if (_activeDocument is not null)
                _activeDocument.IsActive = true;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ActiveContent));
            OnPropertyChanged(nameof(IsHomeVisible));
            StatusSource = value?.StatusSource;
        }
    }

    public bool IsHomeVisible => ActiveDocument is null;
    public Control? ActiveContent => ActiveDocument?.Content;

    public IStatusBarSource? StatusSource
    {
        get => _statusSource;
        private set
        {
            if (ReferenceEquals(_statusSource, value)) return;
            if (_statusSource is not null)
                _statusSource.PropertyChanged -= OnStatusSourcePropertyChanged;
            _statusSource = value;
            if (_statusSource is not null)
                _statusSource.PropertyChanged += OnStatusSourcePropertyChanged;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsPaginationVisible));
        }
    }

    public bool IsPaginationVisible => StatusSource?.HasPagination == true;

    private void OnStatusSourcePropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName is nameof(IStatusBarSource.HasPagination) or nameof(IStatusBarSource.PaginationStatus) or nameof(IStatusBarSource.PaginationPageStatus))
            OnPropertyChanged(nameof(IsPaginationVisible));
    }

    public ICommand ShowProductsCommand { get; }
    public ICommand ShowServicesCommand { get; }
    public ICommand ShowBrandsCommand { get; }
    public ICommand ShowUnitsOfMeasureCommand { get; }
    public ICommand ShowCustomersCommand { get; }
    public ICommand ShowSuppliersCommand { get; }
    public ICommand ShowPartnersCommand { get; }
    public ICommand ShowStockCommand { get; }
    public ICommand ShowStockTransfersCommand { get; }
    public ICommand ShowStockAdjustmentsCommand { get; }
    public ICommand ShowPurchaseOrdersCommand { get; }
    public ICommand ShowGoodsReceiptsCommand { get; }
    public ICommand ShowQuotesCommand { get; }
    public ICommand ShowSalesCommand { get; }
    public ICommand ShowEmployeesCommand { get; }
    public ICommand ShowTermDeliveryCommand { get; }
    public ICommand ShowClassificationsCommand { get; }
    public ICommand ChangePasswordCommand { get; }
    public ICommand ShowUsersCommand { get; }
    public ICommand ShowRolesCommand { get; }
    public ICommand ShowPermissionsCommand { get; }
    public ICommand ShowDatabaseMaintenanceCommand { get; }
    public ICommand ShowSystemSettingsCommand { get; }
    public ICommand ShowUserOptionsCommand { get; }
    public ICommand ShowAboutCommand { get; }
    public ICommand UseLightThemeCommand { get; }
    public ICommand UseDarkThemeCommand { get; }
    public ICommand ToggleThemeCommand { get; }
    public ICommand SignOutCommand { get; }
    public ICommand ClearNotificationsCommand { get; }
    public ObservableCollection<AppNotification> Notifications => _notificationCenter?.Notifications ?? _emptyNotifications;
    public bool HasNotifications => Notifications.Count > 0;
    public string NotificationCountText => Notifications.Count > 99 ? "99+" : Notifications.Count.ToString();
    public string UserPresentationName { get; }
    public string UserUsername { get; }
    public string ThemeToolTip => string.IsNullOrEmpty(_themeToolTip)
        ? _themeService.CurrentTheme == DecorThemeStyle.Dark ? "Mudar para tema claro" : "Mudar para tema escuro"
        : _themeToolTip;
    public string UserInitials { get; }
    public bool CanViewUsers => ((RelayCommand)ShowUsersCommand).CanExecute(null);
    public bool CanViewUnitsOfMeasure => ((RelayCommand)ShowUnitsOfMeasureCommand).CanExecute(null);
    public bool CanViewServices => ShowServicesCommand.CanExecute(null);
    public bool CanViewEmployees => ((RelayCommand)ShowEmployeesCommand).CanExecute(null);
    public bool CanViewCustomers => ((RelayCommand)ShowCustomersCommand).CanExecute(null);
    public bool CanViewSuppliers => ((RelayCommand)ShowSuppliersCommand).CanExecute(null);
    public bool CanViewPartners => ((RelayCommand)ShowPartnersCommand).CanExecute(null);
    public bool CanViewStock => ((RelayCommand)ShowStockCommand).CanExecute(null);
    public bool CanViewStockTransfers => ((RelayCommand)ShowStockTransfersCommand).CanExecute(null);
    public bool CanViewStockAdjustments => ((RelayCommand)ShowStockAdjustmentsCommand).CanExecute(null);
    public bool CanViewPurchaseOrders => ((RelayCommand)ShowPurchaseOrdersCommand).CanExecute(null);
    public bool CanViewGoodsReceipts => ((RelayCommand)ShowGoodsReceiptsCommand).CanExecute(null);
    public bool CanViewQuotes => ((RelayCommand)ShowQuotesCommand).CanExecute(null);
    public bool CanViewSales => ((RelayCommand)ShowSalesCommand).CanExecute(null);
    public bool CanViewRoles => ((RelayCommand)ShowRolesCommand).CanExecute(null);
    public bool CanViewPermissions => ((RelayCommand)ShowPermissionsCommand).CanExecute(null);
    public bool CanViewAccess => CanViewUsers || CanViewRoles || CanViewPermissions;
    public bool CanViewDatabaseMaintenance => ((RelayCommand)ShowDatabaseMaintenanceCommand).CanExecute(null);
    public bool CanViewSystemSettings => ((RelayCommand)ShowSystemSettingsCommand).CanExecute(null);

    public event EventHandler? PasswordChangeRequested;
    public event EventHandler? AboutRequested;

    public event PropertyChangedEventHandler? PropertyChanged;

    private static readonly ObservableCollection<AppNotification> _emptyNotifications = [];

    private void OnNotificationsChanged(object? sender, NotifyCollectionChangedEventArgs eventArgs)
    {
        OnPropertyChanged(nameof(HasNotifications));
        OnPropertyChanged(nameof(NotificationCountText));
    }

    private void OpenSingletonDocument(string key, string title, Func<Control> createContent)
    {
        var existingDocument = OpenDocuments.FirstOrDefault(document => document.Key == key);
        if (existingDocument is not null)
        {
            ActiveDocument = existingDocument;
            return;
        }

        OpenDocument(key, title, createContent());
    }

    private TView CreateView<TView>() where TView : Control =>
        _navigationService.Resolve<TView>();

    private void OpenDocument(string key, string title, Control content)
    {
        var document = new WorkspaceDocumentViewModel(key, title, content, CloseDocument, ActivateDocument);
        OpenDocuments.Add(document);
        ActiveDocument = document;
    }

    private void ActivateDocument(WorkspaceDocumentViewModel document) => ActiveDocument = document;

    private void RequestPasswordChange() => PasswordChangeRequested?.Invoke(this, EventArgs.Empty);

    private async Task SetThemeAsync(DecorThemeStyle theme)
    {
        _themeToolTip = string.Empty;
        try
        {
            await _themeService.SetThemeAsync(theme);
            ApplyCurrentTheme();
        }
        catch (Exception)
        {
            _themeToolTip = "Não foi possível salvar o tema.";
        }
        OnPropertyChanged(nameof(ThemeToolTip));
    }

    private bool IsAdministrator()
    {
        var user = _authenticatedUserContext.User;
        return SystemAccountDefaults.IsAdministrator(user?.Username)
            || (user?.Roles ?? []).Contains(SystemRoleDefaults.Administrators, StringComparer.OrdinalIgnoreCase);
    }

    private static string InitialsOf(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "?";

        var parts = name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 1
            ? parts[0][..Math.Min(2, parts[0].Length)].ToUpperInvariant()
            : string.Concat(char.ToUpperInvariant(parts[0][0]), char.ToUpperInvariant(parts[^1][0]));
    }

    private void CloseDocument(WorkspaceDocumentViewModel document)
    {
        var documentIndex = OpenDocuments.IndexOf(document);
        if (documentIndex < 0)
            return;

        var wasActive = ReferenceEquals(ActiveDocument, document);
        if (wasActive)
            ActiveDocument = null;

        OpenDocuments.RemoveAt(documentIndex);
        if (wasActive)
            ActiveDocument = OpenDocuments.ElementAtOrDefault(Math.Min(documentIndex, OpenDocuments.Count - 1));
    }

    private void ApplyCurrentTheme()
    {
        if (global::Avalonia.Application.Current is null) return;

        global::Avalonia.Application.Current.RequestedThemeVariant =
            _themeService.CurrentTheme == DecorThemeStyle.Dark
                ? Avalonia.Styling.ThemeVariant.Dark
                : Avalonia.Styling.ThemeVariant.Light;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

}
