using System.Collections.ObjectModel;
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
    private WorkspaceDocumentViewModel? _activeDocument;
    private IStatusBarSource? _statusSource;
    private string _themeToolTip = "Selecionar tema";

    public MainViewModel(
        INavigationService navigationService,
        IThemeService themeService,
        IAuthorizationService authorizationService,
        IAuthenticatedUserContext authenticatedUserContext)
    {
        _navigationService = navigationService;
        _themeService = themeService;
        _authenticatedUserContext = authenticatedUserContext;

        ShowProductsCommand = new RelayCommand(() => OpenSingletonDocument("products", "Produtos", () => CreateView<ProductsView>()));
        NewProductCommand = new RelayCommand(async () => await OpenNewProductAsync());
        ShowBrandsCommand = new RelayCommand(() => OpenSingletonDocument("brands", "Marcas", () => CreateView<BrandsView>()));
        ShowTermDeliveryCommand = new RelayCommand(() => OpenSingletonDocument("term-delivery", "Termo de Entrega", () => CreateView<TermDeliveryView>()));
        ShowClassificationsCommand = new RelayCommand(() => OpenSingletonDocument("classifications", "Classificações", () => CreateView<ClassificationsView>()));
        ChangePasswordCommand = new RelayCommand(RequestPasswordChange);
        ShowUsersCommand = new RelayCommand(() => OpenSingletonDocument("users", "Usuários", () => CreateView<UsersView>()), () => authorizationService.HasPermission(DecorPermissions.UsersView));
        ShowRolesCommand = new RelayCommand(() => OpenSingletonDocument("roles", "Grupos de Permissões", () => CreateView<RolesView>()), () => authorizationService.HasPermission(DecorPermissions.RolesView));
        ShowDatabaseMaintenanceCommand = new RelayCommand(() => OpenSingletonDocument("database-maintenance", "Manutenção do banco", () => CreateView<DatabaseMaintenanceView>()), () => authorizationService.HasPermission(DecorPermissions.DatabaseMaintenanceView));
        ShowIconCatalogCommand = new RelayCommand(() => OpenSingletonDocument("icon-catalog", "Catálogo de Ícones", () => CreateView<IconCatalogView>()), () => IsAdministrator());
        ShowUserOptionsCommand = new RelayCommand(() => OpenSingletonDocument("user-options", "Preferências", () => CreateView<UserOptionsView>()));
        UseLightThemeCommand = new RelayCommand(async () => await SetThemeAsync(DecorThemeStyle.Light));
        UseDarkThemeCommand = new RelayCommand(async () => await SetThemeAsync(DecorThemeStyle.Dark));
        SignOutCommand = new RelayCommand(_authenticatedUserContext.SignOut);

        var user = authenticatedUserContext.User;
        var username = user?.Username ?? string.Empty;
        UserUsername = username;
        UserPresentationName = IsAdministrator()
            ? "Administrador"
            : !string.IsNullOrWhiteSpace(user?.EmployeeName) ? user.EmployeeName : username;
        UserInitials = InitialsOf(UserPresentationName);

        OpenDocuments.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasOpenDocuments));
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
            OnPropertyChanged(nameof(IsHomeVisible));
            StatusSource = value?.StatusSource;
        }
    }

    public bool IsHomeVisible => ActiveDocument is null;

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
    public ICommand NewProductCommand { get; }
    public ICommand ShowBrandsCommand { get; }
    public ICommand ShowTermDeliveryCommand { get; }
    public ICommand ShowClassificationsCommand { get; }
    public ICommand ChangePasswordCommand { get; }
    public ICommand ShowUsersCommand { get; }
    public ICommand ShowRolesCommand { get; }
    public ICommand ShowDatabaseMaintenanceCommand { get; }
    public ICommand ShowIconCatalogCommand { get; }
    public ICommand ShowUserOptionsCommand { get; }
    public ICommand UseLightThemeCommand { get; }
    public ICommand UseDarkThemeCommand { get; }
    public ICommand SignOutCommand { get; }
    public string UserPresentationName { get; }
    public string UserUsername { get; }
    public string ThemeDisplayName => _themeService.CurrentTheme == DecorThemeStyle.Dark ? "Tema: Escuro" : "Tema: Claro";
    public string ThemeToolTip => _themeToolTip;
    public string UserInitials { get; }
    public bool CanViewUsers => ((RelayCommand)ShowUsersCommand).CanExecute(null);
    public bool CanViewRoles => ((RelayCommand)ShowRolesCommand).CanExecute(null);
    public bool CanViewDatabaseMaintenance => ((RelayCommand)ShowDatabaseMaintenanceCommand).CanExecute(null);
    public bool CanViewIconCatalog => ((RelayCommand)ShowIconCatalogCommand).CanExecute(null);

    public event EventHandler? PasswordChangeRequested;

    public event PropertyChangedEventHandler? PropertyChanged;

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

    private async Task OpenNewProductAsync()
    {
        var productDocument = OpenDocuments.FirstOrDefault(document => document.Key == "products");
        if (productDocument is null)
        {
            var productView = CreateView<ProductsView>();
            productDocument = new WorkspaceDocumentViewModel("products", "Produtos", productView, CloseDocument, ActivateDocument);
            OpenDocuments.Add(productDocument);
        }

        ActiveDocument = productDocument;
        await ((ProductsViewModel)productDocument.Content.DataContext!).BeginNewAsync();
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
        _themeToolTip = "Selecionar tema";
        try
        {
            await _themeService.SetThemeAsync(theme);
            ApplyCurrentTheme();
            OnPropertyChanged(nameof(ThemeDisplayName));
        }
        catch (Exception)
        {
            _themeToolTip = "Não foi possível salvar o tema.";
        }
        OnPropertyChanged(nameof(ThemeToolTip));
    }

    private bool IsAdministrator()
    {
        var roles = _authenticatedUserContext.User?.Roles ?? [];
        return roles.Contains(SystemRoleDefaults.Administrators, StringComparer.OrdinalIgnoreCase);
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

        OpenDocuments.RemoveAt(documentIndex);
        if (ReferenceEquals(ActiveDocument, document))
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
