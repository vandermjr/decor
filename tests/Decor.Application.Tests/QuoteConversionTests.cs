using System.ComponentModel.DataAnnotations;
using System.Data;
using Decor.Application.Services;
using Decor.Core.Common;
using Decor.Core.DTOs;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Data;
using Decor.Core.Interfaces.Repositories;
using Decor.Core.Interfaces.Services;
using Decor.Core.Validation;
using Moq;

namespace Decor.Application.Tests;

public sealed class QuoteConversionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Conversion_ProratesDiscountAcrossSectionsInBothPersistencePaths(bool transactional)
    {
        var quote = CreateQuote(30m);
        var first = quote.Sections.First();
        first.Items.Add(new QuoteItem { QuoteItemID = 11, Quantity = 2m, UnitPrice = 100m });
        var second = quote.Sections.Last();
        second.Items.Add(new QuoteItem { QuoteItemID = 12, Quantity = 1m, UnitPrice = 100m });
        var fixture = new ConversionFixture(quote, transactional);

        var firstOrder = await fixture.Service.ConvertFromQuoteAsync(first.QuoteSectionID);
        var secondOrder = await fixture.Service.ConvertFromQuoteAsync(second.QuoteSectionID);

        Assert.Equal(90m, Assert.Single(firstOrder.Items!).UnitPrice);
        Assert.Equal(90m, Assert.Single(secondOrder.Items!).UnitPrice);
        Assert.Equal(270m, firstOrder.Items!.Sum(item => item.Quantity * item.UnitPrice)
            + secondOrder.Items!.Sum(item => item.Quantity * item.UnitPrice));
        Assert.All(quote.Sections, section => Assert.Equal(QuoteSectionStatus.ConvertedToOrder, section.Status));
        Assert.All(quote.Sections.SelectMany(section => section.Items), item => Assert.Equal(100m, item.UnitPrice));
        if (transactional)
            fixture.Transaction.Verify(transaction => transaction.Commit(), Times.Exactly(2));
    }

    [Fact]
    public async Task Conversion_AssignsCentResidualToLastPricedItem()
    {
        var quote = CreateQuote(1m);
        var section = quote.Sections.First();
        foreach (var itemId in new[] { 11, 12, 13 })
            section.Items.Add(new QuoteItem { QuoteItemID = itemId, Quantity = 1m, UnitPrice = 10m });
        var fixture = new ConversionFixture(quote, false);

        var order = await fixture.Service.ConvertFromQuoteAsync(section.QuoteSectionID);

        Assert.Equal(new[] { 9.67m, 9.67m, 9.66m }, order.Items!.Select(item => item.UnitPrice));
        Assert.Equal(29m, order.Items!.Sum(item => item.Quantity * item.UnitPrice));
    }

    [Theory]
    [InlineData(null, 5)]
    [InlineData(10, null)]
    [InlineData(0, 5)]
    [InlineData(10, -1)]
    public async Task Conversion_RequiresValidCustomerAndEmployeeBeforePersistence(int? customerId, int? employeeId)
    {
        var quote = CreateQuote(0m);
        quote.CustomerID = customerId;
        quote.CreatedByEmployeeID = employeeId;
        var fixture = new ConversionFixture(quote, false);

        await Assert.ThrowsAsync<ValidationException>(() => fixture.Service.ConvertFromQuoteAsync(1));

        fixture.Orders.Verify(repository => repository.SaveAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public async Task Conversion_RejectsInvalidDiscountBeforePersistence(decimal discount)
    {
        var quote = CreateQuote(discount);
        quote.Sections.First().Items.Add(new QuoteItem { QuoteItemID = 11, Quantity = 1m, UnitPrice = 100m });
        var fixture = new ConversionFixture(quote, false);
        await Assert.ThrowsAsync<ValidationException>(() => fixture.Service.ConvertFromQuoteAsync(1));
        fixture.Orders.Verify(repository => repository.SaveAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Conversion_FullDiscountCreatesZeroPricedItems()
    {
        var quote = CreateQuote(100m);
        quote.Sections.First().Items.Add(new QuoteItem { QuoteItemID = 11, Quantity = 1m, UnitPrice = 100m });
        var order = await new ConversionFixture(quote, false).Service.ConvertFromQuoteAsync(1);
        Assert.Equal(0m, Assert.Single(order.Items!).UnitPrice);
    }

    private static Quote CreateQuote(decimal discount) => new()
    {
        QuoteID = 42, CustomerID = 10, CreatedByEmployeeID = 5, DiscountAmount = discount,
        Sections =
        [
            new QuoteSection { QuoteSectionID = 1, QuoteID = 42, SectionType = QuoteSectionType.Catalog, Status = QuoteSectionStatus.Approved },
            new QuoteSection { QuoteSectionID = 2, QuoteID = 42, SectionType = QuoteSectionType.Custom, Status = QuoteSectionStatus.Approved }
        ]
    };

    private sealed class ConversionFixture
    {
        public Mock<IOrderRepository> Orders { get; } = new();
        public Mock<IDbTransaction> Transaction { get; } = new();
        public OrderService Service { get; }

        public ConversionFixture(Quote quote, bool transactional)
        {
            var quotes = new Mock<IQuoteRepository>();
            quotes.Setup(repository => repository.GetSectionByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((int sectionId, CancellationToken token) => quote.Sections.Single(section => section.QuoteSectionID == sectionId));
            quotes.Setup(repository => repository.GetCompleteQuoteAsync(quote.QuoteID, It.IsAny<CancellationToken>())).ReturnsAsync(quote);
            var nextOrderId = 100;
            Orders.Setup(repository => repository.SaveAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
                .Callback((Order order, CancellationToken token) => order.OrderID = nextOrderId++).ReturnsAsync(1);
            var validator = new Mock<IRepositoryValidator<Order>>();
            validator.Setup(instance => instance.Validate(It.IsAny<Order>())).Returns(Array.Empty<string>());
            var authorization = new Mock<IAuthorizationService>();
            authorization.Setup(instance => instance.HasPermission(DecorPermissions.OrdersConvertFromQuote)).Returns(true);

            var connection = new Mock<IDbConnection>();
            connection.Setup(instance => instance.BeginTransaction()).Returns(Transaction.Object);
            var database = new Mock<IDatabaseConnection>();
            database.Setup(instance => instance.CreateConnection()).Returns(connection.Object);
            var transactionalOrders = new Mock<ITransactionalOrderRepository>();
            transactionalOrders.Setup(repository => repository.SaveAsync(It.IsAny<Order>(), connection.Object, Transaction.Object, It.IsAny<CancellationToken>()))
                .Callback((Order order, IDbConnection db, IDbTransaction tx, CancellationToken token) => order.OrderID = nextOrderId++).ReturnsAsync(1);
            var transactionalQuotes = new Mock<ITransactionalQuoteRepository>();

            Service = new OrderService(Orders.Object, quotes.Object, Mock.Of<IProductRepository>(),
                Mock.Of<IStockReservationService>(), Mock.Of<IOrderInstallmentService>(), Mock.Of<IDTOValidator<OrderDTO>>(),
                validator.Object, authorization.Object, transactional ? database.Object : null,
                transactional ? transactionalOrders.Object : null, transactional ? transactionalQuotes.Object : null);
        }
    }
}