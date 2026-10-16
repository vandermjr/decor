using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Decor.AvaloniaUI.Services;
using Decor.AvaloniaUI.ViewModels;
using Decor.Core.DTOs;

namespace Decor.AvaloniaUI.Views;

public partial class ProductsView : UserControl
{
    public ProductsView()
    {
        InitializeComponent();
        ConfigureGridColumns();
        SizeChanged += (_, _) => UpdateCatalogLayout();
        UpdateCatalogLayout();
        ProductsGrid.AttachedToVisualTree += (_, _) => Dispatcher.UIThread.Post(ApplyProductColumnWidths);
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
                nameof(ProductDTO.Description),
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

    private void ApplyProductColumnWidths()
    {
        foreach (var propertyName in new[]
                 {
                     nameof(ProductDTO.ProductID),
                     nameof(ProductDTO.Description),
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
        nameof(ProductDTO.Description) => DataGridLength.Auto,
        nameof(ProductDTO.BrandName) => DataGridLength.Auto,
        nameof(ProductDTO.ManufacturerRef) => DataGridLength.Auto,
        nameof(ProductDTO.Dimensions) => DataGridLength.Auto,
        _ => DataGridLength.Auto
    };

    private void UpdateCatalogLayout()
    {
        var layout = this.FindControl<Grid>("CatalogLayout");
        if (layout is null) return;

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

        var compact = Bounds.Width < 1120;
        layout.ColumnDefinitions = compact ? new ColumnDefinitions("*") : new ColumnDefinitions("2*,360");
        layout.RowDefinitions = compact ? new RowDefinitions("3*,2*") : new RowDefinitions("*");
        Grid.SetColumn(ProductsGridFrame, 0);
        Grid.SetColumn(ProductDetailsPanel, compact ? 0 : 1);
        Grid.SetRow(ProductsGridFrame, 0);
        Grid.SetRow(ProductDetailsPanel, compact ? 1 : 0);
    }

    private async void ExportProductsClicked(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is not ProductsViewModel viewModel || TopLevel.GetTopLevel(this) is not { } topLevel)
            return;

        try
        {
            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Exportar produtos",
                SuggestedFileName = $"produtos-{DateTime.Now:yyyyMMdd-HHmm}.csv",
                FileTypeChoices =
                [
                    new FilePickerFileType("Arquivo CSV") { Patterns = ["*.csv"] }
                ]
            });
            if (file is null) return;

            var columns = ProductsGrid.Columns
                .Where(column => column.IsVisible && !string.IsNullOrWhiteSpace(column.SortMemberPath))
                .Select(column => (Header: column.Header?.ToString() ?? string.Empty,
                    Property: typeof(ProductDTO).GetProperty(column.SortMemberPath!)))
                .Where(column => column.Property is not null)
                .Select(column => (column.Header, Property: column.Property!))
                .ToArray();
            await using var stream = await file.OpenWriteAsync();
            await ProductCsvExporter.WriteAsync(stream, viewModel.Products, columns);

            viewModel.ReportProductsExported();
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
        foreach (var column in ProductsGrid.Columns)
        {
            if (column.Header is not string header) continue;
            var item = new CheckBox { Content = header, IsChecked = column.IsVisible };
            item.Click += (_, _) => column.IsVisible = item.IsChecked == true;
            items.Children.Add(item);
        }
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

}
