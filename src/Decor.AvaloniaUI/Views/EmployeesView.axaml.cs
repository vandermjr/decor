using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Decor.AvaloniaUI.ViewModels;
using Decor.Core.DTOs;

namespace Decor.AvaloniaUI.Views;

public partial class EmployeesView : UserControl
{
    public EmployeesView()
    {
        InitializeComponent();
        EmployeesGrid.InitializeColumns(
            dtoType: typeof(EmployeeDTO),
            getDisplayName: null,
            getColumnWidth: propertyName => propertyName == nameof(EmployeeDTO.EmployeeID)
                ? new DataGridLength(80)
                : new DataGridLength(1, DataGridLengthUnitType.Star),
            propertyNames: [nameof(EmployeeDTO.EmployeeID), nameof(EmployeeDTO.Name), nameof(EmployeeDTO.JobTitle),
                nameof(EmployeeDTO.BaseSalary), nameof(EmployeeDTO.WorkScheduleNote), nameof(EmployeeDTO.Document),
                nameof(EmployeeDTO.Phone), nameof(EmployeeDTO.IsActive)]);
        EmployeesGrid.ValueMatchChanged += (_, eventArgs) =>
        {
            if (DataContext is EmployeesViewModel viewModel)
                viewModel.SetValueMatch(eventArgs.ColumnName, eventArgs.MatchCount);
        };
        AttachedToVisualTree += (_, _) => Dispatcher.UIThread.Post(() => SearchTextBox.FocusTextInput());
    }

    public EmployeesView(EmployeesViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }

}