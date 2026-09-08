using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;
using Decor.AvaloniaUI.Views;
using Decor.Core.Common;
using Decor.Core.Interfaces.Services;
using System.Windows.Input;

namespace Decor.AvaloniaUI.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly IServiceProvider _services;
    private readonly IThemeService _themeService;
    private readonly IAuthenticatedUserContext _authenticatedUserContext;
    private WorkspaceDocumentViewModel? _activeDocument;
    private IStatusBarSource? _statusSource;

    public MainViewModel(
        IServiceProvider services,
        IThemeService themeService,
        IAuthorizationService authorizationService,
        IAuthenticatedUserContext authenticatedUserContext)
    {
        _services = services;
        _themeService = themeService;
        _authenticatedUserContext = authenticatedUserContext;

        ShowProductsCommand = new RelayCommand(() => OpenSingletonDocument("products", "Produtos", () => CreateView<ProductsView>()));
        NewProductCommand = new RelayCommand(async () => await OpenNewProductAsync());
        ShowBrandsCommand = new RelayCommand(() => OpenSingletonDocument("brands", "Marcas", () => CreateView<BrandsView>()));
        ShowTermDeliveryCommand = new RelayCommand(() => OpenSingletonDocument("term-delivery", "Termo de Entrega", () => CreateView<TermDeliveryView>()));
        ShowClassificationsCommand = new RelayCommand(() => OpenSingletonDocument("classifications", "Classificações", () => CreateView<ClassificationsView>()));
        ChangePasswordCommand = new RelayCommand(RequestPasswordChange);
        ShowUsersCommand = new RelayCommand(() => OpenSingletonDocument("users", "Usuários", () => CreateView<UsersView>()), () => authorizationService.HasPermission(DecorPermissions.UsersView));
        ShowDatabaseMaintenanceCommand = new RelayCommand(() => OpenSingletonDocument("database-maintenance", "Manutenção do banco", () => CreateView<DatabaseMaintenanceView>()));
        SignOutCommand = new RelayCommand(_authenticatedUserContext.SignOut);
        UseLightThemeCommand = new RelayCommand(() => IsDarkTheme = false);
        UseDarkThemeCommand = new RelayCommand(() => IsDarkTheme = true);
        ShowNotificationsCommand = new RelayCommand(() => { });

        var username = authenticatedUserContext.User?.Username ?? string.Empty;
        UserDisplayName = string.IsNullOrWhiteSpace(username)
            ? authenticatedUserContext.User?.DisplayName ?? string.Empty
            : username;
        UserInitials = InitialsOf(UserDisplayName);
        var primaryRole = authenticatedUserContext.User?.Roles.FirstOrDefault();
        UserDisplayNameWithRole = string.IsNullOrWhiteSpace(primaryRole)
            ? UserDisplayName
            : $"{UserDisplayName} ({primaryRole})";

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
    public ICommand ShowDatabaseMaintenanceCommand { get; }
    public ICommand SignOutCommand { get; }
    public ICommand UseLightThemeCommand { get; }
    public ICommand UseDarkThemeCommand { get; }
    public ICommand ShowNotificationsCommand { get; }
    public string UserDisplayName { get; }
    public string UserDisplayNameWithRole { get; }
    public string UserInitials { get; }
    public bool CanViewUsers => ((RelayCommand)ShowUsersCommand).CanExecute(null);

    public event EventHandler? PasswordChangeRequested;

    public bool IsDarkTheme
    {
        get => _themeService.CurrentTheme == DecorThemeStyle.Dark;
        set
        {
            var newTheme = value ? DecorThemeStyle.Dark : DecorThemeStyle.Light;
            if (_themeService.CurrentTheme == newTheme) return;

            _ = SetThemeAsync(newTheme);
        }
    }

    public bool IsLightTheme => _themeService.CurrentTheme != DecorThemeStyle.Dark;

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
        (TView)_services.GetService(typeof(TView))!;

    private void OpenDocument(string key, string title, Control content)
    {
        var document = new WorkspaceDocumentViewModel(key, title, content, CloseDocument, ActivateDocument);
        OpenDocuments.Add(document);
        ActiveDocument = document;
    }

    private void ActivateDocument(WorkspaceDocumentViewModel document) => ActiveDocument = document;

    private void RequestPasswordChange() => PasswordChangeRequested?.Invoke(this, EventArgs.Empty);

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
                ? ThemeVariant.Dark
                : ThemeVariant.Light;
    }

    private async Task SetThemeAsync(DecorThemeStyle theme)
    {
        await _themeService.SetThemeAsync(theme);

        if (global::Avalonia.Application.Current is not null)
        {
            global::Avalonia.Application.Current.RequestedThemeVariant =
                theme == DecorThemeStyle.Dark
                    ? ThemeVariant.Dark
                    : ThemeVariant.Light;
        }

        OnPropertyChanged(nameof(IsDarkTheme));
        OnPropertyChanged(nameof(IsLightTheme));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

}
