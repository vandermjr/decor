using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using Avalonia.Media;
using Decor.AvaloniaUI.Icons;
using Decor.Core.Common;
using Decor.Core.Interfaces.Services;
using System.Windows.Input;
using Decor.Core.Configuration;
using Avalonia.Styling;

namespace Decor.AvaloniaUI.ViewModels;

public sealed class IconCatalogViewModel : INotifyPropertyChanged
{
    private readonly IIconAppearanceService _iconAppearanceService;
    private readonly IThemeService _themeService;
    private readonly IStockSettingsService? _stockSettingsService;
    private int _iconWeight;
    private bool _allowNegativeStock;
    private string _selectedSettingsSection = "Theme";
    private string _selectedThemeOption = string.Empty;
    private string _stockSettingsStatus = string.Empty;

    public ObservableCollection<IconCatalogFamilyViewModel> FamilyGroups { get; } = [];
    public ObservableCollection<string> ThemeOptions { get; } = ["Claro", "Escuro"];

    public int IconWeight
    {
        get => _iconWeight;
        set
        {
            if (!DecorIconCatalog.SupportedWeights.Contains(value) || !SetField(ref _iconWeight, value))
                return;

            foreach (var item in FamilyGroups.SelectMany(group => group.Items))
                item.UpdateGeometry(value);
        }
    }

    public string IconWeightDisplay => IconWeight.ToString(CultureInfo.InvariantCulture);
    public ICommand ApplyCommand { get; }
    public ICommand ResetIconColorsCommand { get; }
    public ICommand SaveStockSettingsCommand { get; }
    public ICommand ApplyThemeCommand { get; }
    public ICommand UseLightThemeCommand { get; }
    public ICommand UseDarkThemeCommand { get; }
    public bool IsThemeSettingsSelected => _selectedSettingsSection == "Theme";
    public bool IsIconSettingsSelected => _selectedSettingsSection == "Icons";
    public bool IsStockMovementsSelected => _selectedSettingsSection == "StockMovements";
    public string CurrentThemeName => _themeService.CurrentTheme == DecorThemeStyle.Light ? "Claro" : "Escuro";
    public string SelectedThemeOption
    {
        get => _selectedThemeOption;
        set => SetField(ref _selectedThemeOption, value);
    }
    public string StockSettingsStatus
    {
        get => _stockSettingsStatus;
        private set => SetField(ref _stockSettingsStatus, value);
    }

    public string SelectedSettingsSection
    {
        get => _selectedSettingsSection;
        set
        {
            if (!SetField(ref _selectedSettingsSection, value)) return;
            OnPropertyChanged(nameof(IsThemeSettingsSelected));
            OnPropertyChanged(nameof(IsIconSettingsSelected));
            OnPropertyChanged(nameof(IsStockMovementsSelected));
        }
    }

    public bool AllowNegativeStock
    {
        get => _allowNegativeStock;
        set => SetField(ref _allowNegativeStock, value);
    }

    public IconCatalogViewModel(IIconAppearanceService iconAppearanceService, IThemeService themeService, IStockSettingsService? stockSettingsService = null)
    {
        _iconAppearanceService = iconAppearanceService;
        _themeService = themeService;
        _stockSettingsService = stockSettingsService;
        _iconWeight = iconAppearanceService.CurrentAppearance.MaterialSymbolWeight;
        _themeService.ThemeChanged += OnThemeChanged;
        ApplyCommand = new RelayCommand(async () => await ApplyAsync());
        ResetIconColorsCommand = new RelayCommand(async () => await ResetIconColorsAsync());
        SaveStockSettingsCommand = new RelayCommand(async () => await SaveStockSettingsAsync());
        ApplyThemeCommand = new RelayCommand(async () => await ApplyThemeSelectionAsync());
        UseLightThemeCommand = new RelayCommand(async () => await SetThemeAsync(DecorThemeStyle.Light));
        UseDarkThemeCommand = new RelayCommand(async () => await SetThemeAsync(DecorThemeStyle.Dark));
        _selectedThemeOption = CurrentThemeName;
        foreach (var family in DecorIconCatalog.GetFamilyGroups())
        {
            var group = new IconCatalogFamilyViewModel(family.Name);
            foreach (var iconId in family.Ids)
                group.Items.Add(new IconCatalogItemViewModel(iconId, IconWeight, _iconAppearanceService, ThemeVariantFor(_themeService.CurrentTheme)));

            FamilyGroups.Add(group);
        }
    }

    public async Task InitializeAsync()
    {
        if (_stockSettingsService is null) return;
        AllowNegativeStock = await _stockSettingsService.GetAllowNegativeStockAsync();
    }

    private async Task ApplyAsync()
    {
        await _iconAppearanceService.SetAppearanceAsync(IconWeight, _iconAppearanceService.CurrentAppearance.StrokeThickness);
    }

    private async Task ResetIconColorsAsync()
    {
        await _iconAppearanceService.ResetIconColorsAsync();
        foreach (var item in FamilyGroups.SelectMany(group => group.Items))
            item.RefreshColor();
    }

    private void OnThemeChanged(DecorThemeStyle theme)
    {
        var themeVariant = ThemeVariantFor(theme);
        foreach (var item in FamilyGroups.SelectMany(group => group.Items))
            item.RefreshColor(themeVariant);
    }

    private static ThemeVariant ThemeVariantFor(DecorThemeStyle theme) =>
        theme == DecorThemeStyle.Dark ? ThemeVariant.Dark : ThemeVariant.Light;

    private async Task SaveStockSettingsAsync()
    {
        if (_stockSettingsService is null)
        {
            StockSettingsStatus = "As configurações de estoque não estão disponíveis.";
            return;
        }

        try
        {
            await _stockSettingsService.SetAllowNegativeStockAsync(AllowNegativeStock);
            StockSettingsStatus = "Configuração de estoque salva.";
        }
        catch
        {
            StockSettingsStatus = "Não foi possível salvar a configuração de estoque.";
        }
    }

    private async Task SetThemeAsync(DecorThemeStyle theme)
    {
        if (_themeService.CurrentTheme == theme)
            return;

        await _themeService.SetThemeAsync(theme);
        _selectedThemeOption = theme == DecorThemeStyle.Light ? "Claro" : "Escuro";
        if (global::Avalonia.Application.Current is not null)
        {
            global::Avalonia.Application.Current.RequestedThemeVariant = theme == DecorThemeStyle.Dark
                ? global::Avalonia.Styling.ThemeVariant.Dark
                : global::Avalonia.Styling.ThemeVariant.Light;
        }
        OnPropertyChanged(nameof(CurrentThemeName));
        OnPropertyChanged(nameof(SelectedThemeOption));
    }

    private async Task ApplyThemeSelectionAsync()
    {
        var desiredTheme = SelectedThemeOption == "Claro" ? DecorThemeStyle.Light : DecorThemeStyle.Dark;
        await SetThemeAsync(desiredTheme);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        OnPropertyChanged(propertyName);
        if (propertyName == nameof(IconWeight)) OnPropertyChanged(nameof(IconWeightDisplay));
        return true;
    }

    private void OnPropertyChanged(string? propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public sealed class IconCatalogFamilyViewModel(string name)
{
    public string Name { get; } = name switch
    {
        "Application" => "Aplicativo",
        "Modules" => "Módulos",
        "Forms" => "Formulários",
        "Actions" => "Ações",
        "User" => "Usuário",
        "Common" => "Geral",
        "Navigation" => "Navegação",
        _ => name
    };
    public ObservableCollection<IconCatalogItemViewModel> Items { get; } = [];
}

public sealed class IconCatalogItemViewModel : INotifyPropertyChanged
{
    private static readonly IReadOnlyDictionary<string, string> DisplayNames = new Dictionary<string, string>
    {
        ["Application.Home"] = "Início",
        ["Application.Settings"] = "Configurações",
        ["Application.Help"] = "Ajuda",
        ["Application.Info"] = "Informações",
        ["Application.ThemeToggle"] = "Alternar tema",
        ["Modules.Cadastros"] = "Cadastros",
        ["Modules.Compras"] = "Compras",
        ["Modules.Estoque"] = "Estoque",
        ["Modules.Comercial"] = "Comercial",
        ["Modules.Servicos"] = "Serviços",
        ["Modules.Financeiro"] = "Financeiro",
        ["Modules.Configuracoes"] = "Configurações",
        ["Forms.Products"] = "Produtos",
        ["Forms.Brands"] = "Marcas",
        ["Forms.Classifications"] = "Classificações",
        ["Forms.TermDelivery"] = "Entrega",
        ["Forms.Users"] = "Usuários",
        ["Forms.PermissionGroups"] = "Grupos de permissões",
        ["Forms.Permissions"] = "Permissões",
        ["Forms.DatabaseMaintenance"] = "Manutenção do banco de dados",
        ["Actions.Search"] = "Pesquisar",
        ["Actions.View"] = "Visualizar",
        ["Actions.Create"] = "Adicionar",
        ["Actions.Edit"] = "Editar",
        ["Actions.Delete"] = "Excluir",
        ["Actions.Save"] = "Salvar",
        ["Actions.Cancel"] = "Cancelar",
        ["Actions.Add"] = "Adicionar",
        ["Actions.Remove"] = "Remover",
        ["Actions.Report"] = "Relatório",
        ["Actions.Close"] = "Fechar",
        ["Actions.Clear"] = "Limpar",
        ["Actions.Filter"] = "Filtrar",
        ["Actions.Copy"] = "Copiar",
        ["Actions.Cart"] = "Carrinho",
        ["User.Profile"] = "Perfil",
        ["User.Preferences"] = "Preferências",
        ["User.ChangePassword"] = "Alterar senha",
        ["User.Notifications"] = "Notificações",
        ["User.SignIn"] = "Entrar",
        ["User.SignOut"] = "Sair",
        ["Common.Calendar"] = "Calendário",
        ["Common.Clock"] = "Relógio",
        ["Common.Folder"] = "Pasta",
        ["Common.Database"] = "Banco de dados",
        ["Common.Backup"] = "Cópia de segurança",
        ["Common.Code"] = "Código",
        ["Common.Barcode"] = "Código de barras",
        ["Common.Status"] = "Bloqueado",
        ["Common.Unlocked"] = "Desbloqueado",
        ["Navigation.FirstPage"] = "Primeira página",
        ["Navigation.PreviousPage"] = "Página anterior",
        ["Navigation.NextPage"] = "Próxima página",
        ["Navigation.LastPage"] = "Última página",
        ["Navigation.Dropdown"] = "Abrir opções",
        ["Navigation.SortAscending"] = "Ordenar crescente",
        ["Navigation.SortDescending"] = "Ordenar decrescente"
    };

    private Geometry _geometry;
    private readonly IIconAppearanceService _iconAppearanceService;
    private ThemeVariant? _themeVariant;

    public IconCatalogItemViewModel(DecorIconId id, int iconWeight, IIconAppearanceService iconAppearanceService, ThemeVariant? themeVariant = null)
    {
        Id = id;
        _iconAppearanceService = iconAppearanceService;
        _themeVariant = themeVariant;
        _geometry = DecorIconCatalog.Get(id, iconWeight);
    }

    public IconCatalogItemViewModel(DecorIconId id, int iconWeight)
        : this(id, iconWeight, new StubIconAppearanceService())
    {
    }

    public DecorIconId Id { get; }
    public string Name => DisplayNames.GetValueOrDefault(Id.Value, GetShortName(Id.Value));
    public string FullId => Id.Value;
    public Geometry Geometry => _geometry;
    public string? CurrentColor => _iconAppearanceService.CurrentAppearance.GetColor(Id.Value);
    public IBrush ColorPreviewBrush => DecorIconColors.GetBrush(Id, _iconAppearanceService.CurrentAppearance, _themeVariant);

    public void RefreshColor(ThemeVariant? themeVariant = null)
    {
        if (themeVariant is not null)
            _themeVariant = themeVariant;
        OnPropertyChanged(nameof(CurrentColor));
        OnPropertyChanged(nameof(ColorPreviewBrush));
    }

    public void SetColor(string? colorHex)
    {
        _iconAppearanceService.SetIconColorOverrideAsync(Id.Value, colorHex).GetAwaiter().GetResult();
        RefreshColor();
    }

    public void UpdateGeometry(int weight)
    {
        _geometry = DecorIconCatalog.Get(Id, weight);
        OnPropertyChanged(nameof(Geometry));
    }

    private sealed class StubIconAppearanceService : IIconAppearanceService
    {
        public IconAppearance CurrentAppearance { get; } = IconAppearance.Default;
        public event Action<IconAppearance>? AppearanceChanged
        {
            add { }
            remove { }
        }

        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SetAppearanceAsync(int materialSymbolWeight, double strokeThickness, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SetIconColorOverrideAsync(string iconId, string? colorHex, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ResetIconColorsAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private static string GetShortName(string value)
    {
        var separatorIndex = value.LastIndexOf('.');
        return separatorIndex >= 0 && separatorIndex < value.Length - 1
            ? value[(separatorIndex + 1)..]
            : value;
    }
}
