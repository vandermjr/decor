using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Decor.AvaloniaUI.ViewModels;

namespace Decor.AvaloniaUI.Views;

public partial class ContextualSearchWindow : Window
{
    public ContextualSearchWindow()
    {
        InitializeComponent();
        ResultsGrid.InitializeColumns(typeof(LookupSearchItem), propertyNames: [
            nameof(LookupSearchItem.Code), nameof(LookupSearchItem.Name),
            nameof(LookupSearchItem.Detail), nameof(LookupSearchItem.Category)]);
        ResultsGrid.DefaultSortMemberPath = nameof(LookupSearchItem.Name);
        AttachedToVisualTree += (_, _) => Dispatcher.UIThread.Post(() => SearchTextBox.Focus());
    }

    public ContextualSearchWindow(ContextualSearchViewModel viewModel) : this() => DataContext = viewModel;

    public LookupSearchItem? SelectedResult => (DataContext as ContextualSearchViewModel)?.SelectedItem;

    private async void SearchTextBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        if (DataContext is ContextualSearchViewModel viewModel)
            await viewModel.SearchAsync();
        e.Handled = true;
    }

    private void Accept_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (SelectedResult is not null) Close(true);
    }

    private void Cancel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close(false);
}