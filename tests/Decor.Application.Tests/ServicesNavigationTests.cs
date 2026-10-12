using Decor.AvaloniaUI.Services;
using Decor.AvaloniaUI.ViewModels;
using Decor.Core.DTOs;
using Decor.Core.Interfaces.Services;
using Moq;

namespace Decor.Application.Tests;

public sealed class ServicesNavigationTests
{
    [Fact]
    public void DeniedView_DoesNotResolveCatalog()
    {
        var navigation = new Mock<INavigationService>(MockBehavior.Strict);
        var authorization = new Mock<IAuthorizationService>();
        var viewModel = new MainViewModel(navigation.Object, new Mock<IThemeService>().Object, authorization.Object, new Mock<IAuthenticatedUserContext>().Object);
        Assert.False(viewModel.CanViewServices);
        Assert.False(viewModel.ShowServicesCommand.CanExecute(null));
        viewModel.ShowServicesCommand.Execute(null);
        Assert.Empty(viewModel.OpenDocuments);
        Assert.Empty(navigation.Invocations);
    }

    [Fact]
    public void StockViews_ExposeNavigationCommands_WhenAuthorized()
    {
        var navigation = new Mock<INavigationService>();
        var authorization = new Mock<IAuthorizationService>();
        authorization.Setup(service => service.HasPermission(Decor.Core.Common.DecorPermissions.StockMovementsView)).Returns(true);
        authorization.Setup(service => service.HasPermission(Decor.Core.Common.DecorPermissions.StockMovementsTransfer)).Returns(true);
        authorization.Setup(service => service.HasPermission(Decor.Core.Common.DecorPermissions.StockMovementsAdjust)).Returns(true);

        var viewModel = new MainViewModel(navigation.Object, new Mock<IThemeService>().Object, authorization.Object, new Mock<IAuthenticatedUserContext>().Object);

        Assert.True(viewModel.CanViewStock);
        Assert.True(viewModel.ShowStockCommand.CanExecute(null));
        Assert.True(viewModel.ShowStockTransfersCommand.CanExecute(null));
        Assert.True(viewModel.ShowStockAdjustmentsCommand.CanExecute(null));
    }

    [Fact]
    public async Task StockOverview_UsesOnlyRegisteredProductCatalogIds()
    {
        var products = new Mock<IProductService>();
        products.Setup(service => service.GetAllProductsAsync(1, 250, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new ProductDTO(
                    ProductID: 101,
                    Barcode: "SKU-1001",
                    IsActive: true,
                    Description: "Argamassa ACME",
                    StockQuantity: 42m,
                    BrandID: null,
                    BrandName: null,
                    SubgroupID: null,
                    SubgroupName: null,
                    GroupID: null,
                    GroupName: null,
                    FamilyID: null,
                    FamilyName: null,
                    ClassID: null,
                    ClassName: null,
                    ManufacturerRef: null,
                    AuxiliarRef: null,
                    Dimensions: null,
                    Observations: null,
                    MinimumStock: 0m,
                    ProductType: 3,
                    CostPrice: null,
                    SalePrice: null,
                    EmployeeCommissionValue: null,
                    DefaultInstallationServiceID: null,
                    StockUnitID: null,
                    StockUnitCode: "sc",
                    CurrentStockQuantity: 42m,
                    ReservedStockQuantity: 0m,
                    AcquisitionMode: Decor.Core.Entities.ProductAcquisitionMode.Estocado),
                new ProductDTO(
                    ProductID: 205,
                    Barcode: "SKU-2005",
                    IsActive: true,
                    Description: "Piso Premium",
                    StockQuantity: 12m,
                    BrandID: null,
                    BrandName: null,
                    SubgroupID: null,
                    SubgroupName: null,
                    GroupID: null,
                    GroupName: null,
                    FamilyID: null,
                    FamilyName: null,
                    ClassID: null,
                    ClassName: null,
                    ManufacturerRef: null,
                    AuxiliarRef: null,
                    Dimensions: null,
                    Observations: null,
                    MinimumStock: 0m,
                    ProductType: 3,
                    CostPrice: null,
                    SalePrice: null,
                    EmployeeCommissionValue: null,
                    DefaultInstallationServiceID: null,
                    StockUnitID: null,
                    StockUnitCode: "m2",
                    CurrentStockQuantity: 12m,
                    ReservedStockQuantity: 0m,
                    AcquisitionMode: Decor.Core.Entities.ProductAcquisitionMode.Estocado)
            ]);

        var viewModel = new StockViewModel(products.Object);

        Assert.Contains(viewModel.Items, row => row.ProductId == 101 && row.Description == "Argamassa ACME" && row.UnitCode == "sc");
        Assert.Contains(viewModel.Items, row => row.ProductId == 205 && row.Description == "Piso Premium" && row.UnitCode == "m2");
        Assert.DoesNotContain(viewModel.Items, row => row.ProductId is 10 or 12 or 18 or 24 or 31);
    }
}