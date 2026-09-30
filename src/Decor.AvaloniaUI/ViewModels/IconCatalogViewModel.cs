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
    private int _iconWeight;
    private double _strokeThickness;

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

    public double StrokeThickness
    {
        get => _strokeThickness;
        set
        {
            var roundedValue = Math.Round(value, 1, MidpointRounding.AwayFromZero);
            if (!SetField(ref _strokeThickness, roundedValue))
                return;

            OnPropertyChanged(nameof(StrokeThicknessDisplay));
            foreach (var item in FamilyGroups.SelectMany(group => group.Items))
                item.StrokeThickness = roundedValue;
        }
    }

    public string IconWeightDisplay => IconWeight.ToString(CultureInfo.InvariantCulture);
    public string StrokeThicknessDisplay => StrokeThickness.ToString("0.0", CultureInfo.InvariantCulture);
    public ICommand ApplyCommand { get; }

    public IconCatalogViewModel(IIconAppearanceService iconAppearanceService)
    {
        _iconAppearanceService = iconAppearanceService;
        _iconWeight = iconAppearanceService.CurrentAppearance.MaterialSymbolWeight;
        _strokeThickness = iconAppearanceService.CurrentAppearance.StrokeThickness;
        ApplyCommand = new RelayCommand(async () => await ApplyAsync());
        foreach (var family in DecorIconCatalog.GetFamilyGroups())
        {
            var group = new IconCatalogFamilyViewModel(family.Name);
            foreach (var iconId in family.Ids)
                group.Items.Add(new IconCatalogItemViewModel(iconId, IconWeight, StrokeThickness));

            FamilyGroups.Add(group);
        }
    }

    private async Task ApplyAsync()
    {
        await _iconAppearanceService.SetAppearanceAsync(IconWeight, StrokeThickness);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        OnPropertyChanged(propertyName);
        OnPropertyChanged(propertyName == nameof(IconWeight) ? nameof(IconWeightDisplay) : null);
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
    private double _strokeThickness;

    public IconCatalogItemViewModel(DecorIconId id, int iconWeight, double strokeThickness)
    {
        Id = id;
        _geometry = DecorIconCatalog.Get(id, iconWeight);
        _strokeThickness = strokeThickness;
    }

    public DecorIconId Id { get; }
    public string Name => GetShortName(Id.Value);
    public string FullId => Id.Value;
    public Geometry Geometry => _geometry;

    public double StrokeThickness
    {
        get => _strokeThickness;
        set
        {
            if (Math.Abs(_strokeThickness - value) < double.Epsilon)
                return;

            _strokeThickness = value;
            OnPropertyChanged();
        }
    }

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
