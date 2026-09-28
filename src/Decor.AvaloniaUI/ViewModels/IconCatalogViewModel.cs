using System.Collections.ObjectModel;
using Avalonia.Media;
using Decor.AvaloniaUI.Icons;

namespace Decor.AvaloniaUI.ViewModels;

public sealed class IconCatalogViewModel
{
    public ObservableCollection<IconCatalogFamilyViewModel> FamilyGroups { get; } = [];

    public IconCatalogViewModel()
    {
        foreach (var family in DecorIconCatalog.GetFamilyGroups())
        {
            var group = new IconCatalogFamilyViewModel(family.Name);
            foreach (var iconId in family.Ids)
                group.Items.Add(new IconCatalogItemViewModel(iconId));

            FamilyGroups.Add(group);
        }
    }
}

public sealed class IconCatalogFamilyViewModel(string name)
{
    public string Name { get; } = name;
    public ObservableCollection<IconCatalogItemViewModel> Items { get; } = [];
}

public sealed class IconCatalogItemViewModel(DecorIconId id)
{
    public string Name => GetShortName(id.Value);
    public string FullId => id.Value;
    public Geometry Geometry => DecorIconCatalog.Get(id);

    private static string GetShortName(string value)
    {
        var separatorIndex = value.LastIndexOf('.');
        return separatorIndex >= 0 && separatorIndex < value.Length - 1
            ? value[(separatorIndex + 1)..]
            : value;
    }
}
