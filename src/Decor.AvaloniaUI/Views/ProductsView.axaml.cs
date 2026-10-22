using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Decor.AvaloniaUI.Services;
using Decor.AvaloniaUI.ViewModels;
using Decor.Core.Configuration;
using Decor.Core.DTOs;
using Decor.Core.Interfaces.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Decor.AvaloniaUI.Views;

public partial class ProductsView : UserControl
{
    private readonly IUserSettingsService? _userSettingsService;
    private bool _columnOrderLoaded;

    public ProductsView()
    {
        InitializeComponent();
        _userSettingsService = (Avalonia.Application.Current as App)?.Services.GetService<IUserSettingsService>();
        ConfigureGridColumns();
        SizeChanged += (_, _) => UpdateCatalogLayout();
        UpdateCatalogLayout();
        ProductsGrid.AttachedToVisualTree += (_, _) => Dispatcher.UIThread.Post(async () =>
        {
            ApplyProductColumnWidths();
            await RestoreProductColumnOrderAsync();
        });
        AttachedToVisualTree += (_, _) => Dispatcher.UIThread.Post(() => SearchTextBox.FocusTextInput());
        ProductsGrid.ValueMatchChanged += (_, eventArgs) =>
        {
            if (DataContext is ProductsViewModel viewModel)
                viewModel.SetValueMatch(eventArgs.ColumnName, eventArgs.MatchCount);
        };
    }

    public ProductsView(ProductsViewModel viewModel)
        : this()
    {
        DataContext = viewModel;
        _ = viewModel.InitializeAsync();
    }

    private void ConfigureGridColumns()
    {
        ProductsGrid.InitializeColumns(
            dtoType: typeof(ProductDTO),
            getDisplayName: propertyName => (propertyName == nameof(ProductDTO.ProductID)
                ? "Código"
                : propertyName == nameof(ProductDTO.StockStatus)
                    ? "Situação"
                    : typeof(ProductDTO).GetProperty(propertyName)?.GetCustomAttribute<DisplayAttribute>()?.GetName() ?? propertyName)
                .ToUpperInvariant(),
            getColumnWidth: GetColumnWidth,
            propertyNames:
            [
                nameof(ProductDTO.ProductID),
                nameof(ProductDTO.ProductName),
                nameof(ProductDTO.BrandName),
                nameof(ProductDTO.StockUnitCode),
                nameof(ProductDTO.CurrentStockQuantity),
                nameof(ProductDTO.ReservedStockQuantity),
                nameof(ProductDTO.AvailableStockQuantity),
                nameof(ProductDTO.MinimumStock),
                nameof(ProductDTO.StockStatus)
            ]
        );
    }

    private async void ClassificationFilterFlyout_Opened(object? sender, EventArgs eventArgs)
    {
        if (DataContext is ProductsViewModel viewModel)
            await viewModel.EnsureClassificationTreeAsync();
    }

    private void ApplyProductColumnWidths()
    {
        foreach (var propertyName in new[]
                 {
                     nameof(ProductDTO.ProductID),
                     nameof(ProductDTO.ProductName),
                     nameof(ProductDTO.BrandName),
                     nameof(ProductDTO.StockUnitCode),
                     nameof(ProductDTO.CurrentStockQuantity),
                     nameof(ProductDTO.ReservedStockQuantity),
                     nameof(ProductDTO.AvailableStockQuantity),
                     nameof(ProductDTO.MinimumStock),
                     nameof(ProductDTO.StockStatus)
                 })
        {
            SetColumnWidth(propertyName, DataGridLength.Auto);
        }
    }

    private void SetColumnWidth(string propertyName, DataGridLength width)
    {
        var column = ProductsGrid.Columns.FirstOrDefault(candidate => candidate.SortMemberPath == propertyName);
        if (column is not null) column.Width = width;
    }

    private static DataGridLength GetColumnWidth(string propertyName) => propertyName switch
    {
        nameof(ProductDTO.ProductID) => DataGridLength.Auto,
        nameof(ProductDTO.IsActive) => DataGridLength.Auto,
        nameof(ProductDTO.StockQuantity) => DataGridLength.Auto,
        nameof(ProductDTO.MinimumStock) => DataGridLength.Auto,
        nameof(ProductDTO.CurrentStockQuantity) => DataGridLength.Auto,
        nameof(ProductDTO.ReservedStockQuantity) => DataGridLength.Auto,
        nameof(ProductDTO.AvailableStockQuantity) => DataGridLength.Auto,
        nameof(ProductDTO.StockStatus) => DataGridLength.Auto,
        nameof(ProductDTO.StockUnitCode) => DataGridLength.Auto,
        nameof(ProductDTO.Barcode) => DataGridLength.Auto,
        nameof(ProductDTO.ProductName) => DataGridLength.Auto,
        nameof(ProductDTO.BrandName) => DataGridLength.Auto,
        nameof(ProductDTO.ManufacturerRef) => DataGridLength.Auto,
        nameof(ProductDTO.Dimensions) => DataGridLength.Auto,
        _ => DataGridLength.Auto
    };

    private void UpdateCatalogLayout()
    {
        var layout = this.FindControl<Grid>("CatalogLayout");
        if (layout is null) return;

        var summaryExpander = this.FindControl<Expander>("CatalogSummaryExpander");
        var searchToolsFrame = this.FindControl<Border>("SearchToolsFrame");
        var summaryLayout = this.FindControl<Grid>("CatalogSummaryLayout");
        var compactSummary = Bounds.Width < 760;
        if (summaryLayout is not null)
        {
            summaryLayout.ColumnDefinitions = compactSummary ? new ColumnDefinitions("*,*") : new ColumnDefinitions("*,*,*,*");
            summaryLayout.RowDefinitions = compactSummary ? new RowDefinitions("Auto,Auto") : new RowDefinitions("Auto");
            var cards = new[] { TotalProductsCard, ActiveProductsCard, OutOfStockProductsCard, LowStockProductsCard };
            for (var index = 0; index < cards.Length; index++)
            {
                Grid.SetColumn(cards[index], compactSummary ? index % 2 : index);
                Grid.SetRow(cards[index], compactSummary ? index / 2 : 0);
            }
        }

        ConfigureCatalogLayout(layout, summaryExpander, searchToolsFrame, ProductsGridFrame, ProductDetailsPanel, Bounds.Width);
    }

    private static void ConfigureCatalogLayout(Grid layout, Expander? summaryExpander, Border? searchToolsFrame, Border productsGridFrame, Border productDetailsPanel, double width)
    {
        var compact = width < 1120;
        layout.ColumnDefinitions = compact ? new ColumnDefinitions("*") : new ColumnDefinitions("2*,420");
        layout.RowDefinitions = compact
            ? new RowDefinitions("Auto,Auto,3*,2*")
            : new RowDefinitions("Auto,Auto,*");
        if (summaryExpander is not null)
        {
            Grid.SetColumn(summaryExpander, 0);
            Grid.SetRow(summaryExpander, 0);
            Grid.SetRowSpan(summaryExpander, 1);
        }
        if (searchToolsFrame is not null)
        {
            Grid.SetColumn(searchToolsFrame, 0);
            Grid.SetRow(searchToolsFrame, 1);
            Grid.SetRowSpan(searchToolsFrame, 1);
        }
        Grid.SetColumn(productsGridFrame, 0);
        Grid.SetRow(productsGridFrame, 2);
        Grid.SetRowSpan(productsGridFrame, 1);
        Grid.SetColumn(productDetailsPanel, compact ? 0 : 1);
        Grid.SetRow(productDetailsPanel, compact ? 3 : 0);
        Grid.SetRowSpan(productDetailsPanel, 1);
        if (!compact)
            Grid.SetRowSpan(productDetailsPanel, 3);
    }

    private async void ExportProductsCsvClicked(object? sender, RoutedEventArgs eventArgs) =>
        await ExportProductsAsync(ProductExportFormat.Csv);

    private async void ExportProductsXlsClicked(object? sender, RoutedEventArgs eventArgs) =>
        await ExportProductsAsync(ProductExportFormat.Xls);

    private async void ExportProductsXlsxClicked(object? sender, RoutedEventArgs eventArgs) =>
        await ExportProductsAsync(ProductExportFormat.Xlsx);

    private async void ExportProductsOdtClicked(object? sender, RoutedEventArgs eventArgs) =>
        await ExportProductsAsync(ProductExportFormat.Odt);

    private async Task ExportProductsAsync(ProductExportFormat format)
    {
        if (DataContext is not ProductsViewModel viewModel || TopLevel.GetTopLevel(this) is not { } topLevel)
            return;

        var (extension, description) = format switch
        {
            ProductExportFormat.Csv => ("csv", "Arquivo CSV"),
            ProductExportFormat.Xls => ("xls", "Planilha Excel 97-2003"),
            ProductExportFormat.Xlsx => ("xlsx", "Planilha Excel"),
            ProductExportFormat.Odt => ("odt", "Planilha OpenDocument"),
            _ => throw new ArgumentOutOfRangeException(nameof(format))
        };

        try
        {
            var columns = ProductsGrid.Columns
                .Where(column => column.IsVisible && !string.IsNullOrWhiteSpace(column.SortMemberPath))
                .Select(column => (Header: column.Header?.ToString() ?? string.Empty,
                    Property: typeof(ProductDTO).GetProperty(column.SortMemberPath!)))
                .Where(column => column.Property is not null)
                .Select(column => (column.Header, Property: column.Property!))
                .ToArray();

            var exportDirectory = Path.Combine(Path.GetTempPath(), "Decor", "Exportacoes");
            Directory.CreateDirectory(exportDirectory);
            var exportPath = Path.Combine(exportDirectory, $"produtos-{DateTime.Now:yyyyMMdd-HHmmss-fff}.{extension}");
            await using (var stream = new FileStream(exportPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
                await ProductExporter.WriteAsync(stream, viewModel.Listing.AllItems, columns, format);

            Process.Start(new ProcessStartInfo
            {
                FileName = exportPath,
                UseShellExecute = true
            });

            viewModel.ReportProductsExported(exportPath);
        }
        catch
        {
            viewModel.ReportProductsExportFailure();
        }
    }

    private void ConfigureColumnsClicked(object? sender, RoutedEventArgs eventArgs)
    {
        if (sender is not Control anchor) return;

        var items = new StackPanel { Spacing = 4 };
        PopulateColumnMenu(items);
        var flyout = new Flyout
        {
            Content = new Border
            {
                Classes = { "column-menu" },
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(10),
                Child = items
            }
        };
        flyout.ShowAt(anchor);
    }

    private void PopulateColumnMenu(StackPanel items)
    {
        items.Children.Clear();
        foreach (var column in ProductsGrid.Columns.ToArray())
        {
            if (column.Header is not string header || string.IsNullOrWhiteSpace(column.SortMemberPath)) continue;
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), ColumnSpacing = 3 };
            var visibility = new CheckBox { Content = header, IsChecked = column.IsVisible, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
            visibility.Click += (_, _) => column.IsVisible = visibility.IsChecked == true;
            row.Children.Add(visibility);

            var columnIndex = ProductsGrid.Columns.IndexOf(column);
            var moveUp = CreateColumnMoveButton("↑", "Mover coluna para cima", columnIndex > 0);
            Grid.SetColumn(moveUp, 1);
            moveUp.Click += async (_, _) => await MoveColumnAsync(column, -1, items);
            row.Children.Add(moveUp);

            var moveDown = CreateColumnMoveButton("↓", "Mover coluna para baixo", columnIndex < ProductsGrid.Columns.Count - 1);
            Grid.SetColumn(moveDown, 2);
            moveDown.Click += async (_, _) => await MoveColumnAsync(column, 1, items);
            row.Children.Add(moveDown);
            items.Children.Add(row);
        }
    }

    private static Button CreateColumnMoveButton(string glyph, string description, bool enabled)
    {
        var button = new Button
        {
            Content = new TextBlock { Text = glyph, FontSize = 16, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center },
            Width = 28,
            Height = 28,
            MinHeight = 28,
            Padding = new Thickness(0),
            IsEnabled = enabled
        };
        button.Classes.Add("product-secondary-action");
        ToolTip.SetTip(button, description);
        AutomationProperties.SetName(button, description);
        return button;
    }

    private async Task MoveColumnAsync(DataGridColumn column, int offset, StackPanel menuItems)
    {
        var columns = ProductsGrid.Columns;
        var index = columns.IndexOf(column);
        var targetIndex = index + offset;
        if (index < 0 || targetIndex < 0 || targetIndex >= columns.Count) return;

        columns.RemoveAt(index);
        columns.Insert(targetIndex, column);
        PopulateColumnMenu(menuItems);
        await SaveProductColumnOrderAsync();
    }

    private async Task RestoreProductColumnOrderAsync()
    {
        if (_columnOrderLoaded || _userSettingsService is null) return;
        _columnOrderLoaded = true;
        try
        {
            var json = await _userSettingsService.GetValueAsync(DecorUserSettings.ProductGridColumnOrder);
            var savedOrder = string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<string[]>(json);
            if (savedOrder is null || savedOrder.Length == 0) return;

            var byProperty = ProductsGrid.Columns
                .Where(column => !string.IsNullOrWhiteSpace(column.SortMemberPath))
                .ToDictionary(column => column.SortMemberPath!, StringComparer.Ordinal);
            var ordered = savedOrder.Where(byProperty.ContainsKey).Select(name => byProperty[name])
                .Concat(ProductsGrid.Columns.Where(column => string.IsNullOrWhiteSpace(column.SortMemberPath)
                    || !savedOrder.Contains(column.SortMemberPath, StringComparer.Ordinal)))
                .ToArray();
            for (var index = 0; index < ordered.Length; index++)
            {
                var currentIndex = ProductsGrid.Columns.IndexOf(ordered[index]);
                if (currentIndex == index) continue;
                ProductsGrid.Columns.RemoveAt(currentIndex);
                ProductsGrid.Columns.Insert(index, ordered[index]);
            }
        }
        catch (Exception)
        {
            _columnOrderLoaded = false;
        }
    }

    private async Task SaveProductColumnOrderAsync()
    {
        if (_userSettingsService is null) return;
        try
        {
            var order = ProductsGrid.Columns.Where(column => !string.IsNullOrWhiteSpace(column.SortMemberPath))
                .Select(column => column.SortMemberPath).ToArray();
            await _userSettingsService.SetValueAsync(DecorUserSettings.ProductGridColumnOrder, JsonSerializer.Serialize(order));
        }
        catch (InvalidOperationException)
        {
            // The grid remains usable when no authenticated user can persist settings.
        }
    }

}
