using System.ComponentModel;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Decor.AvaloniaUI.Controls;
using Decor.AvaloniaUI.ViewModels;
using Decor.AvaloniaUI.Views;
using Decor.Core.Common;
using Decor.Core.DTOs;
using Decor.Core.Interfaces.Services;
using Moq;

namespace Decor.ActionSort.Tests;

public sealed class CrudActionAvailabilityTests
{
    [AvaloniaFact]
    public void Scoped_policy_preserves_visibility_bindings_and_does_not_hide_pagination_or_fields()
    {
        var source = new VisibilitySource();
        var action = new Button { Content = "Action", DataContext = source };
        action.Classes.Add("decor-action");
        action.Bind(Visual.IsVisibleProperty, new Binding(nameof(VisibilitySource.Shown)));
        var pagination = new Button { Content = "Page", IsEnabled = false };
        pagination.Classes.Add("grid-page-button");
        var field = new TextBox { IsEnabled = false };
        var panel = new StackPanel { Children = { action, pagination, field } };
        var window = new Window { Content = panel };
        window.Show();
        try
        {
            Settle(window);
            var binding = BindingOperations.GetBindingExpressionBase(action, Visual.IsVisibleProperty);
            Assert.NotNull(binding);
            Assert.True(action.IsVisible);
            action.IsEnabled = false;
            Settle(window);
            Assert.False(action.IsVisible);
            Assert.True(pagination.IsVisible);
            Assert.True(field.IsVisible);
            source.Shown = false;
            binding.UpdateTarget();
            action.IsEnabled = true;
            Settle(window);
            Assert.False(action.IsVisible);
            source.Shown = true;
            binding.UpdateTarget();
            Settle(window);
            Assert.True(action.IsVisible);
            panel.IsEnabled = false;
            Settle(window);
            Assert.False(action.IsVisible);
            panel.IsEnabled = true;
            Settle(window);
            Assert.True(action.IsVisible);
            Assert.Same(binding, BindingOperations.GetBindingExpressionBase(action, Visual.IsVisibleProperty));
            action.IsEnabled = false;
            action.Classes.Remove("decor-action");
            Settle(window);
            Assert.True(action.IsVisible);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Search_internal_commands_hide_when_busy_without_overwriting_clear_visibility()
    {
        var available = true;
        var command = new Mock<ICommand>();
        command.Setup(action => action.CanExecute(It.IsAny<object?>())).Returns(() => available);
        var search = new DecorSearchField { SearchCommand = command.Object, ClearCommand = command.Object };
        var window = new Window { Content = search };
        window.Show();
        try
        {
            Settle(window);
            var buttons = search.GetVisualDescendants().OfType<Button>().ToArray();
            var searchButton = Assert.Single(buttons, button => button.Name == "SearchButton");
            var clearButton = Assert.Single(buttons, button => button.Name == "ClearButton");
            Assert.True(searchButton.IsVisible);
            Assert.True(clearButton.IsVisible);
            available = false;
            command.Raise(action => action.CanExecuteChanged += null, EventArgs.Empty);
            Settle(window);
            Assert.False(searchButton.IsVisible);
            Assert.False(clearButton.IsVisible);
            search.ClearCommand = null;
            available = true;
            command.Raise(action => action.CanExecuteChanged += null, EventArgs.Empty);
            Settle(window);
            Assert.True(searchButton.IsVisible);
            Assert.False(clearButton.IsVisible);
            search.ClearCommand = command.Object;
            Settle(window);
            Assert.True(clearButton.IsVisible);
        }
        finally { window.Close(); }
    }

    [AvaloniaTheory]
    [InlineData("Brands", true)]
    [InlineData("Brands", false)]
    [InlineData("Customers", true)]
    [InlineData("Customers", false)]
    [InlineData("Suppliers", true)]
    [InlineData("Suppliers", false)]
    [InlineData("Employees", true)]
    [InlineData("Employees", false)]
    public async Task Save_and_delete_confirmation_use_owner_permissions(string owner, bool adding)
    {
        var granted = true;
        var authorization = new Mock<IAuthorizationService>();
        authorization.Setup(service => service.HasPermission(It.IsAny<string>())).Returns(() => granted);
        object viewModel;
        Control view;
        switch (owner)
        {
            case "Brands":
                var brands = new BrandsViewModel(Mock.Of<IBrandService>(), authorization.Object) { SelectedBrand = new BrandDTO(1, "Brand") };
                viewModel = brands;
                view = new BrandsView();
                break;
            case "Customers":
                var customers = new CustomersViewModel(Mock.Of<ICustomerService>(), authorization.Object) { SelectedItem = new CustomerDTO(1, "Customer", null, null, null, null, true) };
                viewModel = customers;
                view = new CustomersView();
                break;
            case "Suppliers":
                var suppliers = new SuppliersViewModel(Mock.Of<ISupplierService>(), authorization.Object) { SelectedItem = new SupplierDTO(1, "Supplier", null, null, null, true) };
                viewModel = suppliers;
                view = new SuppliersView();
                break;
            default:
                var employees = new EmployeesViewModel(Mock.Of<IEmployeeService>(), authorization.Object) { SelectedEmployee = new EmployeeDTO(1, "Employee", null, null, null, null, null, true, null) };
                viewModel = employees;
                view = new EmployeesView();
                break;
        }
        var type = viewModel.GetType();
        var confirm = (System.Windows.Input.ICommand)type.GetProperty("ConfirmDeleteCommand")!.GetValue(viewModel)!;
        Assert.False(confirm.CanExecute(null));
        type.GetMethod("BeginDelete")!.Invoke(viewModel, null);
        Assert.True(confirm.CanExecute(null));
        granted = false;
        Assert.False(confirm.CanExecute(null));
        granted = true;
        type.GetMethod("CancelDelete")!.Invoke(viewModel, null);
        if (adding) await (Task)type.GetMethod("BeginNewAsync")!.Invoke(viewModel, null)!;
        else type.GetMethod("BeginEdit")!.Invoke(viewModel, null);
        var save = (System.Windows.Input.ICommand)type.GetProperty("SaveCommand")!.GetValue(viewModel)!;
        Assert.True(save.CanExecute(null));
        granted = false;
        Assert.False(save.CanExecute(null));
        view.DataContext = viewModel;
        var window = new Window { Content = view, Width = 1000, Height = 700 };
        window.Show();
        try
        {
            Settle(window);
            var saveButton = Assert.Single(view.GetVisualDescendants().OfType<Button>(), button => ReferenceEquals(button.Command, save));
            Assert.False(saveButton.IsVisible);
            Assert.True(Assert.Single(view.GetVisualDescendants().OfType<Button>(), button =>
                ReferenceEquals(button.Command, type.GetProperty("CancelCommand")!.GetValue(viewModel))).IsVisible);
        }
        finally { window.Close(); }
    }

    private static void Settle(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    private sealed class VisibilitySource : INotifyPropertyChanged
    {
        private bool _shown = true;
        public bool Shown { get => _shown; set { _shown = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Shown))); } }
        public event PropertyChangedEventHandler? PropertyChanged;
    }
}