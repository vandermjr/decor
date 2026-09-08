using Avalonia.Controls;
using System.ComponentModel;
using Decor.AvaloniaUI.ViewModels;

namespace Decor.AvaloniaUI.Views;

public partial class ProductEditWindow : Window
{
    public ProductEditWindow()
    {
        InitializeComponent();
    }

    public ProductEditWindow(ProductsViewModel viewModel)
        : this()
    {
        DataContext = viewModel;
        viewModel.PropertyChanged += ViewModel_PropertyChanged;
    }

    protected override void OnClosed(EventArgs e)
    {
        if (DataContext is ProductsViewModel viewModel)
            viewModel.PropertyChanged -= ViewModel_PropertyChanged;

        base.OnClosed(e);
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ProductsViewModel.IsEditing) &&
            DataContext is ProductsViewModel viewModel &&
            !viewModel.IsEditing)
        {
            Close();
        }
    }
}
