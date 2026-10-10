using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Decor.AvaloniaUI.ViewModels;
using Decor.Core.DTOs;

namespace Decor.AvaloniaUI.Views;

public partial class UnitsOfMeasureView : UserControl
{
    private UnitsOfMeasureViewModel? _subscribedViewModel;

    public UnitsOfMeasureView()
    {
        InitializeComponent();
        UnitsGrid.InitializeColumns(typeof(UnitOfMeasureDTO),
            getDisplayName: propertyName => propertyName switch
            {
                nameof(UnitOfMeasureDTO.UnitOfMeasureID) => "Código",
                nameof(UnitOfMeasureDTO.Code) => "Unidade",
                nameof(UnitOfMeasureDTO.Description) => "Descrição",
                nameof(UnitOfMeasureDTO.AllowsFraction) => "Permite fração",
                nameof(UnitOfMeasureDTO.IsActive) => "Estado",
                _ => propertyName
            },
            getColumnWidth: propertyName => propertyName switch
            {
                nameof(UnitOfMeasureDTO.UnitOfMeasureID) => new DataGridLength(90),
                nameof(UnitOfMeasureDTO.Code) => new DataGridLength(120),
                nameof(UnitOfMeasureDTO.AllowsFraction) => new DataGridLength(140),
                nameof(UnitOfMeasureDTO.IsActive) => new DataGridLength(100),
                _ => new DataGridLength(1, DataGridLengthUnitType.Star)
            },
            propertyNames: [nameof(UnitOfMeasureDTO.UnitOfMeasureID), nameof(UnitOfMeasureDTO.Code),
                nameof(UnitOfMeasureDTO.Description), nameof(UnitOfMeasureDTO.AllowsFraction), nameof(UnitOfMeasureDTO.IsActive)]);
        UnitsGrid.ValueMatchChanged += (_, args) =>
        {
            if (DataContext is UnitsOfMeasureViewModel viewModel)
                viewModel.SetValueMatch(args.ColumnName, args.MatchCount);
        };
        DataContextChanged += (_, _) => UpdateSubscription();
        AttachedToVisualTree += (_, _) => { UpdateSubscription(); FocusActiveField(); };
        DetachedFromVisualTree += (_, _) => Unsubscribe();
    }

    public UnitsOfMeasureView(UnitsOfMeasureViewModel viewModel) : this()
    {
        DataContext = viewModel;
        _ = viewModel.InitializeAsync();
    }

    private void UpdateSubscription()
    {
        Unsubscribe();
        _subscribedViewModel = DataContext as UnitsOfMeasureViewModel;
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
        if (args.PropertyName is nameof(UnitsOfMeasureViewModel.IsEditing) or nameof(UnitsOfMeasureViewModel.IsBusy))
            FocusActiveField();
    }

    private void FocusActiveField() => Dispatcher.UIThread.Post(() =>
    {
        if (DataContext is not UnitsOfMeasureViewModel { IsBusy: false } viewModel) return;
        if (viewModel.IsEditing) CodeTextBox.Focus();
        else SearchTextBox.FocusTextInput();
    });
}