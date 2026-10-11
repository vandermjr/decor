using System.ComponentModel;
using Avalonia.Controls;
using Decor.AvaloniaUI.ViewModels;

namespace Decor.AvaloniaUI.Views;

public partial class LookupSelectionWindow : Window
{
    private INotifyPropertyChanged _viewModel = null!;
    private Func<object?> _getSelectedValue = null!;
    private string _selectionPropertyName = string.Empty;

    public LookupSelectionWindow()
    {
        InitializeComponent();
    }

    public LookupSelectionWindow(Control content, INotifyPropertyChanged viewModel,
        Func<object?> getSelectedValue, string selectionPropertyName, string title)
        : this()
    {
        Title = title;
        LookupContent.Content = content;
        _viewModel = viewModel;
        _getSelectedValue = getSelectedValue;
        _selectionPropertyName = selectionPropertyName;
        _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        Closed += (_, _) => _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        RefreshAcceptButton();
    }

    public object? SelectedValue { get; private set; }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName is null or "" || eventArgs.PropertyName == _selectionPropertyName)
            RefreshAcceptButton();
    }

    private void RefreshAcceptButton() => AcceptButton.IsEnabled = _getSelectedValue() is not null;

    private void Accept_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs eventArgs)
    {
        SelectedValue = _getSelectedValue();
        if (SelectedValue is not null) Close(true);
    }

    private void Cancel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs eventArgs) => Close(false);
}