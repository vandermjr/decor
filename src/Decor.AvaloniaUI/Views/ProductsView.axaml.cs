using System.ComponentModel;
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
        AttachedToVisualTree += (_, _) => Dispatcher.UIThread.Post(() => SearchTextBox.Focus());
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
        viewModel.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(ProductsViewModel.IsEditing) && viewModel.IsEditing)
            {
                // A visibilidade do formulário é atualizada após a propriedade do ViewModel.
                // Executar na próxima passagem da UI garante que o ScrollViewer já tenha sido
                // medido e evita reabrir o formulário na posição de rolagem anterior.
                Dispatcher.UIThread.Post(ProductFormScrollViewer.ScrollToHome);
            }
        };
        _ = viewModel.InitializeAsync();
    }

    private void ConfigureGridColumns()
    {
        ProductsGrid.InitializeColumns(
            dtoType: typeof(ProductDTO),
            getDisplayName: null, // Uses [DisplayAttribute] automatically
            getColumnWidth: GetColumnWidth
        );
    }

    private static DataGridLength GetColumnWidth(string propertyName) => propertyName switch
    {
        nameof(ProductDTO.ProductID) => new DataGridLength(70),
        nameof(ProductDTO.IsActive) => new DataGridLength(80),
        nameof(ProductDTO.StockQuantity) => new DataGridLength(90),
        nameof(ProductDTO.Barcode) => new DataGridLength(1, DataGridLengthUnitType.Star),
        nameof(ProductDTO.Description) => new DataGridLength(2, DataGridLengthUnitType.Star),
        nameof(ProductDTO.BrandName) => new DataGridLength(1.2, DataGridLengthUnitType.Star),
        nameof(ProductDTO.ManufacturerRef) => new DataGridLength(1.2, DataGridLengthUnitType.Star),
        nameof(ProductDTO.Dimensions) => new DataGridLength(1.2, DataGridLengthUnitType.Star),
        _ => new DataGridLength(1, DataGridLengthUnitType.Star)
    };

    private void SearchTextBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;

        if (DataContext is ProductsViewModel viewModel && viewModel.SearchCommand.CanExecute(null))
            viewModel.SearchCommand.Execute(null);

        e.Handled = true;
    }
}
