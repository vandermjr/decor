using Decor.AvaloniaUI.ViewModels;
using Decor.Core.Common;
using Decor.Core.DTOs;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Services;
using Moq;

namespace Decor.Application.Tests;

public sealed class RemainingActionPermissionTests
{
    [Fact]
    public void DatabaseMaintenance_DeniedViewBlocksScheduleAndBackupCommands()
    {
        var backup = new Mock<IDatabaseBackupService>(MockBehavior.Strict);
        var authorization = new Mock<IAuthorizationService>();
        var viewModel = new DatabaseMaintenanceViewModel(backup.Object, authorization: authorization.Object);
        var originalStatus = viewModel.StatusMessage;

        Assert.False(viewModel.NewScheduleCommand.CanExecute(null));
        Assert.False(viewModel.SaveScheduleCommand.CanExecute(null));
        Assert.False(viewModel.DeleteSelectedScheduleCommand.CanExecute(null));
        Assert.False(viewModel.StartImmediateBackupCommand.CanExecute(null));
        viewModel.NewScheduleCommand.Execute(null);
        viewModel.SaveScheduleCommand.Execute(null);
        viewModel.StartImmediateBackupCommand.Execute(null);

        Assert.Empty(viewModel.Schedules);
        Assert.Equal(originalStatus, viewModel.StatusMessage);
        Assert.Empty(backup.Invocations);
        var compatible = new DatabaseMaintenanceViewModel(backup.Object);
        Assert.True(compatible.NewScheduleCommand.CanExecute(null));
        Assert.True(compatible.SaveScheduleCommand.CanExecute(null));
        Assert.True(compatible.StartImmediateBackupCommand.CanExecute(null));
    }

    [Fact]
    public async Task TermDelivery_RevokedViewBlocksRemovalAndReport()
    {
        var products = new Mock<IProductService>(MockBehavior.Strict);
        var authorization = new Mock<IAuthorizationService>();
        authorization.Setup(value => value.HasPermission(It.IsAny<string>())).Returns(true);
        var viewModel = new TermDeliveryViewModel(products.Object, authorization.Object);
        var term = new Term { ProductID = 1, Description = "Product", Qtde = 1 };
        viewModel.Terms.Add(term);
        viewModel.SelectedTerm = term;
        viewModel.BeginRemove();
        Assert.True(viewModel.ConfirmDeleteCommand.CanExecute(null));
        authorization.Setup(value => value.HasPermission(DecorPermissions.TermDeliveryView)).Returns(false);

        Assert.False(viewModel.AddProductCommand.CanExecute(null));
        Assert.False(viewModel.RemoveTermCommand.CanExecute(null));
        Assert.False(viewModel.GenerateReportCommand.CanExecute(null));
        Assert.False(viewModel.ConfirmDeleteCommand.CanExecute(null));
        await viewModel.ConfirmDeleteAsync();
        viewModel.GenerateReportCommand.Execute(null);

        Assert.Single(viewModel.Terms);
        Assert.Empty(products.Invocations);
        Assert.True(new TermDeliveryViewModel(products.Object).AddProductCommand.CanExecute(null));
    }

    [Fact]
    public void TermDelivery_DeniedProductsViewBlocksLookup()
    {
        var products = new Mock<IProductService>(MockBehavior.Strict);
        var authorization = new Mock<IAuthorizationService>();
        authorization.Setup(value => value.HasPermission(DecorPermissions.TermDeliveryView)).Returns(true);
        var viewModel = new TermDeliveryViewModel(products.Object, authorization.Object) { ProductIdText = "1", Quantity = 1 };

        Assert.False(viewModel.AddProductCommand.CanExecute(null));
        viewModel.AddProductCommand.Execute(null);

        Assert.Empty(products.Invocations);
    }

    [Fact]
    public void EditUserRoles_DeniedAssignmentBlocksSaveAfterLoading()
    {
        var users = new Mock<IUserAdministrationService>(MockBehavior.Strict);
        var roles = new Mock<IRoleAdministrationService>();
        roles.Setup(service => service.GetRolesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<AdministrativeRoleDTO>());
        var authorization = new Mock<IAuthorizationService>();
        var viewModel = new EditUserRolesViewModel(users.Object, roles.Object, authorization.Object);
        viewModel.Initialize(new AdministrativeUserDTO(2, "user", "User", true, [], []));

        Assert.True(viewModel.IsInitialized);
        Assert.False(viewModel.SaveCommand.CanExecute(null));
        viewModel.SaveCommand.Execute(null);

        Assert.Empty(users.Invocations);
        var compatible = new EditUserRolesViewModel(users.Object, roles.Object);
        compatible.Initialize(new AdministrativeUserDTO(2, "user", "User", true, [], []));
        Assert.True(compatible.SaveCommand.CanExecute(null));
    }

    [Fact]
    public async Task LegacyUserForms_DeniedCreateAndEditBlockSave()
    {
        var users = new Mock<IUserAdministrationService>(MockBehavior.Strict);
        var roles = new Mock<IRoleAdministrationService>();
        var authorization = new Mock<IAuthorizationService>();
        var create = new CreateUserViewModel(users.Object, roles.Object, authorization.Object);
        var edit = new EditUserViewModel(users.Object, authorization.Object);

        Assert.False(create.CreateCommand.CanExecute(null));
        Assert.False(edit.SaveCommand.CanExecute(null));
        await create.CreateAsync();
        await edit.SaveAsync();

        Assert.Empty(users.Invocations);
        Assert.True(new CreateUserViewModel(users.Object, roles.Object).CreateCommand.CanExecute(null));
        Assert.True(new EditUserViewModel(users.Object).SaveCommand.CanExecute(null));
    }

    [Theory]
    [InlineData(LookupSearchContext.Customer, DecorPermissions.CustomersView)]
    [InlineData(LookupSearchContext.Employee, DecorPermissions.EmployeesView)]
    [InlineData(LookupSearchContext.Partner, DecorPermissions.PartnersView)]
    [InlineData(LookupSearchContext.Product, DecorPermissions.ProductsView)]
    [InlineData(LookupSearchContext.Service, DecorPermissions.ServicesView)]
    public async Task ContextualSearch_DeniedViewBlocksSelectedContext(LookupSearchContext context, string permission)
    {
        var customers = new Mock<ICustomerService>(MockBehavior.Strict);
        var employees = new Mock<IEmployeeService>(MockBehavior.Strict);
        var partners = new Mock<IPartnerService>(MockBehavior.Strict);
        var products = new Mock<IProductService>(MockBehavior.Strict);
        var services = new Mock<IServiceCatalogService>(MockBehavior.Strict);
        var authorization = new Mock<IAuthorizationService>();
        authorization.Setup(value => value.HasPermission(It.IsAny<string>())).Returns(true);
        authorization.Setup(value => value.HasPermission(permission)).Returns(false);
        var viewModel = new ContextualSearchViewModel(customers.Object, employees.Object, partners.Object,
            products.Object, services.Object, authorization.Object);

        await viewModel.InitializeAsync(context);
        Assert.False(viewModel.SearchCommand.CanExecute(null));
        await viewModel.SearchAsync();

        Assert.Empty(customers.Invocations);
        Assert.Empty(employees.Invocations);
        Assert.Empty(partners.Invocations);
        Assert.Empty(products.Invocations);
        Assert.Empty(services.Invocations);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public async Task UserPermissions_DeniedAccessBlocksSearchClearAndLoading(bool authenticated, bool hasView)
    {
        var users = new Mock<IUserAdministrationService>(MockBehavior.Strict);
        var roles = new Mock<IRoleAdministrationService>(MockBehavior.Strict);
        var context = new Mock<IAuthenticatedUserContext>();
        context.SetupGet(value => value.IsAuthenticated).Returns(authenticated);
        var authorization = new Mock<IAuthorizationService>();
        authorization.Setup(value => value.HasPermission(It.IsAny<string>())).Returns(true);
        authorization.Setup(value => value.HasPermission(DecorPermissions.UsersView)).Returns(hasView);
        var viewModel = new UserPermissionsViewModel(users.Object, roles.Object, authorization.Object, context.Object)
            { SearchText = "unchanged" };

        Assert.False(viewModel.SearchCommand.CanExecute(null));
        Assert.False(viewModel.ClearSearchCommand.CanExecute(null));
        viewModel.SearchCommand.Execute(null);
        viewModel.ClearSearchCommand.Execute(null);
        await viewModel.InitializeAsync();
        viewModel.SelectedUser = new AdministrativeUserDTO(2, "user", "User", true, [], []);
        await viewModel.ReloadSelectedAsync();

        Assert.Equal("unchanged", viewModel.SearchText);
        Assert.Empty(viewModel.Users);
        Assert.Empty(viewModel.Modules);
        Assert.Empty(viewModel.Permissions);
        Assert.False(viewModel.IsBusy);
        Assert.False(viewModel.SaveCommand.CanExecute(null));
        Assert.False(viewModel.RestoreCommand.CanExecute(null));
        Assert.False(viewModel.AssignGroupsCommand.CanExecute(null));
        Assert.Empty(users.Invocations);
        Assert.Empty(roles.Invocations);
    }

    [Fact]
    public async Task Roles_DeniedViewBlocksLoading()
    {
        var service = new Mock<IRoleAdministrationService>(MockBehavior.Strict);
        var authorization = new Mock<IAuthorizationService>();
        var viewModel = new RolesViewModel(service.Object, authorization.Object);

        await viewModel.InitializeAsync();

        Assert.Empty(service.Invocations);
        Assert.False(viewModel.SaveCommand.CanExecute(null));
    }

    [Fact]
    public void Sales_RevokedCancelBlocksConfirmation()
    {
        var orders = new Mock<IOrderService>();
        var order = new OrderDTO(1, 2, 3, 0, (int)OrderStatus.Approved, false, null, null, DateTime.Today);
        orders.Setup(service => service.GetOrderByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        var authorization = new Mock<IAuthorizationService>();
        authorization.Setup(service => service.HasPermission(It.IsAny<string>())).Returns(true);
        var viewModel = new SalesViewModel(orders.Object, authorization.Object,
            new Mock<IOrderInstallmentService>().Object, new Mock<IPaymentMethodService>().Object);
        Assert.False(viewModel.ConfirmCancelCommand.CanExecute(null));
        viewModel.SelectedItem = new SaleListItem(1, 2, 3, "", "", "", DateTime.Today);
        viewModel.CancelCommand.Execute(null);
        Assert.True(viewModel.ConfirmCancelCommand.CanExecute(null));
        authorization.Setup(service => service.HasPermission(DecorPermissions.OrdersCancel)).Returns(false);

        Assert.False(viewModel.ConfirmCancelCommand.CanExecute(null));
        viewModel.ConfirmCancelCommand.Execute(null);

        Assert.True(viewModel.ShowCancelConfirmation);
        orders.Verify(service => service.CancelOrderAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(DecorPermissions.OrderInstallmentsCreatePlan)]
    [InlineData(DecorPermissions.OrderInstallmentsView)]
    [InlineData(DecorPermissions.PaymentMethodsView)]
    public void Sales_RevokedPlanPermissionBlocksRemoval(string permission)
    {
        var orders = new Mock<IOrderService>();
        orders.Setup(service => service.GetOrderByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrderDTO(1, 2, 3, 0, (int)OrderStatus.Approved, false, null, null, DateTime.Today));
        var authorization = new Mock<IAuthorizationService>();
        authorization.Setup(service => service.HasPermission(It.IsAny<string>())).Returns(true);
        var viewModel = new SalesViewModel(orders.Object, authorization.Object,
            new Mock<IOrderInstallmentService>().Object, new Mock<IPaymentMethodService>().Object);
        viewModel.SelectedItem = new SaleListItem(1, 2, 3, "", "", "", DateTime.Today);
        var entry = new PaymentPlanEntry(1, "Cash", 10m, DateTime.Today);
        viewModel.PlanEntries.Add(entry);
        viewModel.SelectedPlanEntry = entry;
        Assert.True(viewModel.RemoveInstallmentCommand.CanExecute(null));
        authorization.Setup(service => service.HasPermission(permission)).Returns(false);

        Assert.False(viewModel.RemoveInstallmentCommand.CanExecute(null));
        viewModel.RemoveInstallmentCommand.Execute(null);

        Assert.Single(viewModel.PlanEntries);
    }

    [Fact]
    public async Task Groups_DeniedViewBlocksLoading()
    {
        var service = new Mock<IRoleRegistrationService>(MockBehavior.Strict);
        var context = new Mock<IAuthenticatedUserContext>();
        context.SetupGet(value => value.IsAuthenticated).Returns(true);
        var authorization = new Mock<IAuthorizationService>();
        var viewModel = new GroupRegistrationViewModel(service.Object, context.Object, authorization.Object);

        await viewModel.InitializeAsync();

        Assert.Empty(service.Invocations);
        Assert.False(viewModel.NewCommand.CanExecute(null));
        Assert.False(viewModel.EditCommand.CanExecute(null));
        Assert.False(viewModel.SaveCommand.CanExecute(null));
    }

    [Fact]
    public async Task Groups_RevokedEditBlocksSavingOpenForm()
    {
        var service = new Mock<IRoleRegistrationService>(MockBehavior.Strict);
        var context = new Mock<IAuthenticatedUserContext>();
        context.SetupGet(value => value.IsAuthenticated).Returns(true);
        var authorization = new Mock<IAuthorizationService>();
        authorization.Setup(value => value.HasPermission(It.IsAny<string>())).Returns(true);
        var viewModel = new GroupRegistrationViewModel(service.Object, context.Object, authorization.Object);
        viewModel.NewCommand.Execute(null);
        Assert.True(viewModel.SaveCommand.CanExecute(null));
        authorization.Setup(value => value.HasPermission(DecorPermissions.RolesEdit)).Returns(false);

        Assert.False(viewModel.SaveCommand.CanExecute(null));
        await viewModel.SaveAsync();

        Assert.True(viewModel.IsEditing);
        Assert.Empty(service.Invocations);
    }

    [Fact]
    public async Task Users_DeniedViewBlocksSearchClearAndInitialization()
    {
        var users = new Mock<IUserAdministrationService>(MockBehavior.Strict);
        var authorization = new Mock<IAuthorizationService>();
        authorization.Setup(service => service.HasPermission(It.IsAny<string>())).Returns(true);
        authorization.Setup(service => service.HasPermission(DecorPermissions.UsersView)).Returns(false);
        var viewModel = new UsersViewModel(users.Object, authorization: authorization.Object) { SearchText = "unchanged" };

        Assert.False(viewModel.SearchCommand.CanExecute(null));
        Assert.False(viewModel.ClearSearchCommand.CanExecute(null));
        viewModel.SearchCommand.Execute(null);
        viewModel.ClearSearchCommand.Execute(null);
        await viewModel.InitializeAsync();

        Assert.Equal("unchanged", viewModel.SearchText);
        Assert.Empty(users.Invocations);
    }

    [Fact]
    public async Task Users_WithoutAuthorizationPreservesSearch()
    {
        var users = new Mock<IUserAdministrationService>();
        users.Setup(service => service.SearchAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<AdministrativeUserDTO>());
        var viewModel = new UsersViewModel(users.Object);

        Assert.True(viewModel.SearchCommand.CanExecute(null));
        Assert.True(viewModel.ClearSearchCommand.CanExecute(null));
        await viewModel.InitializeAsync();

        users.Verify(service => service.SearchAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}