using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using Avalonia.Media;
using Decor.AvaloniaUI.Icons;
using Decor.Core.Interfaces.Services;
using System.Windows.Input;

namespace Decor.AvaloniaUI.ViewModels;

public sealed class IconCatalogViewModel : INotifyPropertyChanged
{
    private readonly IIconAppearanceService _iconAppearanceService;
    private readonly IStockSettingsService? _stockSettingsService;
    private int _iconWeight;
    private bool _allowNegativeStock;
    private string _selectedSettingsSection = "StockMovements";
    private string _stockSettingsStatus = string.Empty;

    public ObservableCollection<IconCatalogFamilyViewModel> FamilyGroups { get; } = [];

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
    public ICommand SaveStockSettingsCommand { get; }
    public bool IsIconSettingsSelected => _selectedSettingsSection == "Icons";
    public bool IsStockMovementsSelected => _selectedSettingsSection == "StockMovements";
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
            OnPropertyChanged(nameof(IsIconSettingsSelected));
            OnPropertyChanged(nameof(IsStockMovementsSelected));
        }
    }

    public bool AllowNegativeStock
    {
        get => _allowNegativeStock;
        set => SetField(ref _allowNegativeStock, value);
    }

    public IconCatalogViewModel(IIconAppearanceService iconAppearanceService, IStockSettingsService? stockSettingsService = null)
    {
        _iconAppearanceService = iconAppearanceService;
        _stockSettingsService = stockSettingsService;
        _iconWeight = iconAppearanceService.CurrentAppearance.MaterialSymbolWeight;
        ApplyCommand = new RelayCommand(async () => await ApplyAsync());
        SaveStockSettingsCommand = new RelayCommand(async () => await SaveStockSettingsAsync());
        foreach (var family in DecorIconCatalog.GetFamilyGroups())
        {
            var group = new IconCatalogFamilyViewModel(family.Name);
            foreach (var iconId in family.Ids)
                group.Items.Add(new IconCatalogItemViewModel(iconId, IconWeight));

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
    public string Name { get; } = name;
    public ObservableCollection<IconCatalogItemViewModel> Items { get; } = [];
}

public sealed class IconCatalogItemViewModel : INotifyPropertyChanged
{
    private Geometry _geometry;

    public IconCatalogItemViewModel(DecorIconId id, int iconWeight)
    {
        Id = id;
        _geometry = DecorIconCatalog.Get(id, iconWeight);
    }

    public DecorIconId Id { get; }
    public string Name => GetShortName(Id.Value);
    public string FullId => Id.Value;
    public Geometry Geometry => _geometry;

    public void UpdateGeometry(int weight)
    {
        _geometry = DecorIconCatalog.Get(Id, weight);
        OnPropertyChanged(nameof(Geometry));
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
