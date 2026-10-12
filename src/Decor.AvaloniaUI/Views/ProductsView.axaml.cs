using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Decor.AvaloniaUI.ViewModels;
using Decor.Core.DTOs;

namespace Decor.AvaloniaUI.Views;

public partial class ProductsView : UserControl
{
    public ProductsView()
    {
        InitializeComponent();
        ConfigureGridColumns();
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

}
