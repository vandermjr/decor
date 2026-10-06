using System.Collections.Specialized;
using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using Decor.AvaloniaUI.Icons;

namespace Decor.AvaloniaUI.Controls;

public sealed class DecorGridHeader : IDataTemplate
{
    public static string NormalizeLabel(string label) =>
        label.Equals("Código de barras", StringComparison.OrdinalIgnoreCase) ? "EAN" :
        Regex.Replace(label, @"\bID\b", "Código", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static DecorIconId GetIconId(string label) => NormalizeLabel(label) switch
    {
        "EAN" => DecorIconId.Common.Barcode,
        var code when code == "Código" || code.StartsWith("Código ", StringComparison.Ordinal) => DecorIconId.Common.Code,
        "Estado" => DecorIconId.Common.Status,
        _ => default
    };

    public bool Match(object? data) => data is string;

    public Control? Build(object? data)
    {
        if (data is not string label)
            return null;

        label = NormalizeLabel(label);
        var iconId = GetIconId(label);
        var hasIcon = !string.IsNullOrEmpty(iconId.Value);
        var panel = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions(hasIcon ? "Auto,*,Auto" : "*,Auto"),
            ColumnSpacing = 6,
            VerticalAlignment = VerticalAlignment.Center,
            MinHeight = 20
        };
        if (hasIcon)
        {
            var icon = new PathIcon
            {
                Width = 16,
                Height = 16,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                IsHitTestVisible = false
            };
            BindHeaderForeground(icon);
            DecorIcon.SetId(icon, iconId);
            panel.Children.Add(icon);
        }
        var text = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
        Grid.SetColumn(text, hasIcon ? 1 : 0);
        panel.Children.Add(text);

        var sortIcon = new PathIcon
        {
            Width = 12,
            Height = 12,
            Margin = new Avalonia.Thickness(0, 0, 8, 0),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false,
            Opacity = 0
        };
        BindHeaderForeground(sortIcon);
        Grid.SetColumn(sortIcon, hasIcon ? 2 : 1);
        panel.Children.Add(sortIcon);
        TrackSortState(sortIcon);
        return panel;
    }

    private static void BindHeaderForeground(PathIcon icon) =>
        icon.Bind(PathIcon.ForegroundProperty,
            new Binding("Foreground") { RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor) { AncestorType = typeof(DataGridColumnHeader) } });

    // The header's :sortascending/:sortdescending pseudo-classes drive the indicator on the right side.
    private static void TrackSortState(PathIcon icon)
    {
        DataGridColumnHeader? header = null;

        void Update()
        {
            if (header is null)
                return;
            var ascending = header.Classes.Contains(":sortascending");
            var descending = header.Classes.Contains(":sortdescending");
            icon.Opacity = ascending || descending ? 1 : 0;
            DecorIcon.SetId(icon, ascending ? DecorIconId.Navigation.SortAscending
                : descending ? DecorIconId.Navigation.SortDescending : default);
        }

        void OnClassesChanged(object? sender, NotifyCollectionChangedEventArgs args) => Update();

        icon.AttachedToVisualTree += (_, _) =>
        {
            header = icon.FindAncestorOfType<DataGridColumnHeader>();
            if (header is null)
                return;
            header.Classes.CollectionChanged += OnClassesChanged;
            Update();
        };
        icon.DetachedFromVisualTree += (_, _) =>
        {
            if (header is not null)
                header.Classes.CollectionChanged -= OnClassesChanged;
            header = null;
        };
    }
}