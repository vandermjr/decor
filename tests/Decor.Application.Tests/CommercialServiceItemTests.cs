using System.ComponentModel.DataAnnotations;
using Decor.Application.Mappers;
using Decor.Application.Services;
using Decor.Application.Validation;
using Decor.Core.Common;
using Decor.Core.DTOs;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Repositories;
using Decor.Core.Interfaces.Services;
using Decor.Core.Validation;
using Decor.FluentSqlBuilder;
using Moq;

namespace Decor.Application.Tests;

public sealed class CommercialServiceItemTests
{
    [Theory]
    [InlineData(null, null, false)]
    [InlineData(5, 5, false)]
    [InlineData(0, null, false)]
    [InlineData(null, 0, false)]
    [InlineData(-1, null, false)]
    [InlineData(null, -1, false)]
    [InlineData(5, null, true)]
    [InlineData(null, 5, true)]
    [InlineData(0, 5, false)]
    [InlineData(5, 0, false)]
    public void Reference_RequiresExactlyOnePositiveIdentity(int? productId, int? serviceId, bool valid)
        => Assert.Equal(valid, CommercialItemReference.IsValid(productId, serviceId));

    [Theory]
    [InlineData(null, null)]
    [InlineData(5, 5)]
    [InlineData(0, null)]
    [InlineData(null, 0)]
    public void DtoValidators_RejectInvalidCommercialReferences(int? productId, int? serviceId)
    {
        var quote = new Quote { CustomerID = 1, CreatedByEmployeeID = 1, SourceType = QuoteSourceType.DirectCapture,
            Sections = [new QuoteSection { Items = [new QuoteItem { ProductID = productId, ServiceID = serviceId, Quantity = 1m, UnitPrice = 10m }] }] };
        var order = new Order { CustomerID = 1, QuoteSectionID = 1, OrderType = OrderType.Catalog, Status = OrderStatus.PendingApproval,
            Items = [new OrderItem { ProductID = productId, ServiceID = serviceId, Quantity = 1m, UnitPrice = 10m }] };
        Assert.Contains(CommercialItemReference.ValidationMessage, new QuoteDTOValidator().Validate(quote.ToDTO()));
        Assert.Contains(CommercialItemReference.ValidationMessage, new OrderDTOValidator().Validate(order.ToDTO()));
    }

    [Fact]
    public void Mappers_PreserveIndependentIdentityAndExistingConstructors()
    {
        var quoteProduct = new QuoteItemDTO(1, 2, 5, 3m, 10m, false);
        Assert.Null(quoteProduct.ServiceID);
        Assert.Equal(quoteProduct, quoteProduct.FromDTO().ToDTO());
        var quoteService = quoteProduct with { ProductID = null, ServiceID = 5 };
        Assert.Equal(quoteService, quoteService.FromDTO().ToDTO());
        var orderProduct = new OrderItemDTO(1, 2, 3, 5, 3m, 10m, true, null, null);
        Assert.Null(orderProduct.ServiceID);
        var orderService = orderProduct with { ProductID = null, ServiceID = 5 };
        var roundTrip = orderService.FromDTO().ToDTO();
        Assert.Null(roundTrip.ProductID);
        Assert.Equal(5, roundTrip.ServiceID);
        Assert.Equal(10m, roundTrip.UnitPrice);
        Assert.True(roundTrip.HasInstallationService);

        var cancelledOrder = new Order
        {
            CustomerID = 1,
            QuoteSectionID = 2,
            Status = OrderStatus.Cancelled,
            CancellationReason = "Solicitado pelo cliente"
        };
        cancelledOrder.ToDTO().FromDTO().CancellationReason.Should().Be("Solicitado pelo cliente");
    }

    [Fact]
    public void OrderDtoValidator_RequiresReasonForCancelledOrders()
    {
        var validator = new OrderDTOValidator();
        var order = new Order { CustomerID = 1, QuoteSectionID = 1, Status = OrderStatus.Cancelled };
        Assert.Contains(validator.Validate(order.ToDTO()), error => error.Contains("motivo do cancelamento", StringComparison.OrdinalIgnoreCase));

        order.CancellationReason = "Solicitado pelo cliente";
        Assert.DoesNotContain(validator.Validate(order.ToDTO()), error => error.Contains("motivo do cancelamento", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AggregateValidators_CheckServiceExistence(bool exists)
    {
        var services = new Mock<IServiceRepository>();
        services.Setup(repository => repository.ServiceExists(5)).Returns(exists);
        var quote = new Quote { CustomerID = 1, CreatedByEmployeeID = 1, SourceType = QuoteSourceType.DirectCapture,
            Sections = [new QuoteSection { Items = [new QuoteItem { ServiceID = 5 }] }] };
        var order = new Order { CustomerID = 1, QuoteSectionID = 1, Items = [new OrderItem { ServiceID = 5 }] };
        Assert.Equal(exists, !new QuoteRepositoryValidator(Mock.Of<IQuoteRepository>(), Mock.Of<IPartnerRepository>(), services.Object).Validate(quote).Any());
        Assert.Equal(exists, !new OrderRepositoryValidator(Mock.Of<IOrderRepository>(), services.Object).Validate(order).Any());
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task QuoteSave_UsesServiceCatalogWithoutProductLookup(bool exists, bool active)
    {
        var quotes = new Mock<IQuoteRepository>();
        var quote = new Quote { QuoteID = 1, Sections = [new QuoteSection { QuoteSectionID = 2, Status = QuoteSectionStatus.Draft }] };
        quotes.Setup(repository => repository.GetCompleteQuoteAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(quote);
        quotes.Setup(repository => repository.SaveItemAsync(It.IsAny<QuoteItem>(), It.IsAny<CancellationToken>())).ReturnsAsync(1);
        var products = new Mock<IProductRepository>(MockBehavior.Strict);
        var services = new Mock<IServiceRepository>();
        services.Setup(repository => repository.ServiceExists(5)).Returns(exists);
        var catalog = new Mock<IServiceCatalogService>();
        catalog.Setup(service => service.GetServiceByIdAsync(5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ServiceDTO(5, "Installation", active, 0m, 20m, 0m, null));
        var authorization = new Mock<IAuthorizationService>();
        authorization.Setup(service => service.HasPermission(It.IsAny<string>())).Returns(true);
        var service = new QuoteService(quotes.Object, new QuoteDTOValidator(), Mock.Of<IRepositoryValidator<Quote>>(),
            authorization.Object, Mock.Of<IProductSpecificationAttributeRepository>(), products.Object,
            serviceRepository: services.Object, serviceCatalogService: catalog.Object);
        var dto = new QuoteItemDTO(0, 2, null, 2m, 20m, false, 5);

        if (exists && active)
        {
            var result = await service.SaveQuoteItemAsync(1, dto);
            Assert.Null(result.ProductID);
            Assert.Equal(5, result.ServiceID);
            quotes.Verify(repository => repository.SaveItemAsync(It.Is<QuoteItem>(item => item.ProductID == null && item.ServiceID == 5), It.IsAny<CancellationToken>()), Times.Once);
        }
        else
        {
            await Assert.ThrowsAsync<ValidationException>(() => service.SaveQuoteItemAsync(1, dto));
            quotes.Verify(repository => repository.SaveItemAsync(It.IsAny<QuoteItem>(), It.IsAny<CancellationToken>()), Times.Never);
        }
        products.VerifyNoOtherCalls();
        catalog.Verify(service => service.GetServiceByIdAsync(5, It.IsAny<CancellationToken>()), exists ? Times.Once() : Times.Never());
    }

    [Fact]
    public async Task Stock_ServiceItemNeverQueriesProductsOrStock()
    {
        var orders = new Mock<IOrderRepository>();
        orders.Setup(repository => repository.GetOrderItemByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrderItem { OrderItemID = 1, ServiceID = 5 });
        var products = new Mock<IProductRepository>(MockBehavior.Strict);
        var balances = new Mock<IStockBalanceRepository>(MockBehavior.Strict);
        var reservations = new Mock<IStockReservationRepository>(MockBehavior.Strict);
        var authorization = new Mock<IAuthorizationService>();
        authorization.Setup(service => service.HasPermission(DecorPermissions.StockReservationsCreate)).Returns(true);
        var service = new StockReservationService(reservations.Object, orders.Object, products.Object,
            Mock.Of<IStockLocationRepository>(), balances.Object, new StockReservationDTOValidator(),
            Mock.Of<IRepositoryValidator<StockReservation>>(), authorization.Object);

        await Assert.ThrowsAsync<ValidationException>(() => service.CreateReservationAsync(1, 1, 1, 1m));

        products.VerifyNoOtherCalls();
        balances.VerifyNoOtherCalls();
        reservations.VerifyNoOtherCalls();
    }

    [Fact]
    public void SqlBuilder_PersistsServiceIdAndNullableProductId()
    {
        var quoteSql = FluentCommandBuilder.Create().Insert(insert => insert.Entity(new QuoteItem { ServiceID = 5 })).Build().Sql;
        var orderSql = FluentCommandBuilder.Create().Insert(insert => insert.Entity(new OrderItem { ServiceID = 5 })).Build().Sql;
        Assert.Contains("ServiceID", quoteSql);
        Assert.Contains("ProductID", quoteSql);
        Assert.Contains("ServiceID", orderSql);
        Assert.Contains("ProductID", orderSql);
    }
}