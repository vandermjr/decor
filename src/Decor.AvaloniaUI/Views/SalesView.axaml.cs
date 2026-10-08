using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Decor.AvaloniaUI.ViewModels;

namespace Decor.AvaloniaUI.Views;

public partial class SalesView : UserControl
{
    public SalesView()
    {
        InitializeComponent();
        SalesGrid.InitializeColumns(typeof(SaleListItem), propertyName => propertyName switch
        {
            nameof(SaleListItem.OrderID) => "Código da venda",
            nameof(SaleListItem.QuoteSectionID) => "Seção do orçamento",
            nameof(SaleListItem.CustomerID) => "Cliente",
            nameof(SaleListItem.OrderType) => "Tipo",
            nameof(SaleListItem.Status) => "Estado",
            nameof(SaleListItem.DownPayment) => "Entrada",
            _ => propertyName
        }, propertyName => propertyName == nameof(SaleListItem.OrderID)
            ? new DataGridLength(120)
            : new DataGridLength(1, DataGridLengthUnitType.Star));
        SalesGrid.ValueMatchChanged += (_, args) =>
        {
            if (DataContext is SalesViewModel viewModel) viewModel.SetValueMatch(args.ColumnName, args.MatchCount);
        };
        AttachedToVisualTree += (_, _) => Dispatcher.UIThread.Post(() => SearchTextBox.Focus());
    }

    public SalesView(SalesViewModel viewModel) : this()
    {
        DataContext = viewModel;
        _ = viewModel.InitializeAsync();
    }

    private void SearchTextBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        if (DataContext is SalesViewModel viewModel && viewModel.SearchCommand.CanExecute(null))
            viewModel.SearchCommand.Execute(null);
        e.Handled = true;
    }
}