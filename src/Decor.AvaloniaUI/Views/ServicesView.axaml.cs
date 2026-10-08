using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Decor.AvaloniaUI.ViewModels;
using Decor.Core.DTOs;

namespace Decor.AvaloniaUI.Views;

public partial class ServicesView : UserControl
{
    public ServicesView()
    {
        InitializeComponent();
        ServicesGrid.InitializeColumns(typeof(ServiceDTO),
            getDisplayName: propertyName => propertyName switch
            {
                nameof(ServiceDTO.ServiceID) => "C\u00f3digo",
                nameof(ServiceDTO.IsActive) => "Estado",
                nameof(ServiceDTO.Description) => "Descri\u00e7\u00e3o",
                nameof(ServiceDTO.CostPrice) => "Pre\u00e7o de custo",
                nameof(ServiceDTO.SalePrice) => "Pre\u00e7o de venda",
                nameof(ServiceDTO.EmployeeCommissionValue) => "Comiss\u00e3o",
                _ => propertyName
            },
            getColumnWidth: propertyName => propertyName == nameof(ServiceDTO.Description)
                ? new DataGridLength(1, DataGridLengthUnitType.Star) : new DataGridLength(140),
            propertyNames: [nameof(ServiceDTO.ServiceID), nameof(ServiceDTO.IsActive), nameof(ServiceDTO.Description),
                nameof(ServiceDTO.CostPrice), nameof(ServiceDTO.SalePrice), nameof(ServiceDTO.EmployeeCommissionValue)]);
        ServicesGrid.ValueMatchChanged += (_, args) =>
        {
            if (DataContext is ServicesViewModel viewModel)
                viewModel.SetValueMatch(args.ColumnName, args.MatchCount);
        };
        DataContextChanged += (_, _) => UpdateSubscription();
        AttachedToVisualTree += (_, _) => { UpdateSubscription(); FocusActiveField(); };
        DetachedFromVisualTree += (_, _) => Unsubscribe();
    }

    public ServicesView(ServicesViewModel viewModel) : this()
    {
        DataContext = viewModel;
        _ = viewModel.InitializeAsync();
    }

    private ServicesViewModel? _subscribedViewModel;

    private void UpdateSubscription()
    {
        Unsubscribe();
        _subscribedViewModel = DataContext as ServicesViewModel;
        if (_subscribedViewModel is not null)
            _subscribedViewModel.PropertyChanged += OnViewModelChanged;
    }

    private void Unsubscribe()
    {
        if (_subscribedViewModel is not null)
            _subscribedViewModel.PropertyChanged -= OnViewModelChanged;
        _subscribedViewModel = null;
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(ServicesViewModel.IsEditing) or nameof(ServicesViewModel.IsBusy))
            FocusActiveField();
    }

    private void FocusActiveField() => Dispatcher.UIThread.Post(() =>
    {
        if (DataContext is ServicesViewModel { IsBusy: false } viewModel)
        {
            if (viewModel.IsEditing) DescriptionTextBox.Focus();
            else SearchTextBox.Focus();
        }
    });

    private void SearchTextBox_KeyDown(object? sender, KeyEventArgs args)
    {
        if (args.Key != Key.Enter) return;
        if (DataContext is ServicesViewModel viewModel && viewModel.SearchCommand.CanExecute(null))
            viewModel.SearchCommand.Execute(null);
        args.Handled = true;
    }
}