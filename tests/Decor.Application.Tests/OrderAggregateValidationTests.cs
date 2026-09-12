using System.ComponentModel.DataAnnotations;
using Decor.Application.Services;
using Decor.Application.Validation;
using Decor.Core.Common;
using Decor.Core.DTOs;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Repositories;
using Decor.Core.Interfaces.Services;
using Decor.Core.Validation;
using FluentAssertions;
using Xunit;

namespace Decor.Application.Tests;

public sealed class OrderAggregateValidationTests
{
    [Fact]
    public async Task ConvertFromQuote_ApprovedSection_CreatesOrderAndFreezesPriceAndUpdatesSectionStatus()
    {
        var quoteRepo = new TrackingQuoteRepository();
        var orderRepo = new TrackingOrderRepository();
        var productRepo = new FixedProductRepository();
        var service = CreateService(quoteRepo, orderRepo, productRepo,
            DecorPermissions.OrdersView, DecorPermissions.OrdersConvertFromQuote);

        var quote = CreateQuote(quoteId: 1, customerId: 100);
        var section = CreateSection(sectionId: 10, quoteId: 1, status: QuoteSectionStatus.Approved, type: QuoteSectionType.Custom);
        var item = CreateQuoteItem(itemId: 50, sectionId: 10, productId: 5, unitPrice: 150.50m);
        item.SpecificationValues.Add(new QuoteItemSpecificationValue { ValueID = 1, QuoteItemID = 50, AttributeID = 10, Value = "Azul" });
        section.Items.Add(item);
        quote.Sections.Add(section);
        quoteRepo.SetQuoteForTest(quote);

        var result = await service.ConvertFromQuoteAsync(10);

        result.Should().NotBeNull();
        result.QuoteSectionID.Should().Be(10);
        result.CustomerID.Should().Be(100);
        result.OrderType.Should().Be((int)OrderType.Custom);
        result.Status.Should().Be((int)OrderStatus.PendingApproval);
        result.Items.Should().HaveCount(1);

        var resultItem = result.Items![0];
        resultItem.UnitPrice.Should().Be(150.50m);
        resultItem.SpecificationValues.Should().HaveCount(1);
        resultItem.SpecificationValues![0].Value.Should().Be("Azul");

        section.Status.Should().Be(QuoteSectionStatus.ConvertedToOrder);
    }

    [Fact]
    public async Task ConvertFromQuote_NonApprovedSection_ThrowsValidationException()
    {
        var quoteRepo = new TrackingQuoteRepository();
        var orderRepo = new TrackingOrderRepository();
        var service = CreateService(quoteRepo, orderRepo, new FixedProductRepository(), DecorPermissions.OrdersConvertFromQuote);

        var quote = CreateQuote(quoteId: 1, customerId: 100);
        var section = CreateSection(sectionId: 10, quoteId: 1, status: QuoteSectionStatus.Draft, type: QuoteSectionType.Catalog);
        quote.Sections.Add(section);
        quoteRepo.SetQuoteForTest(quote);

        var act = () => service.ConvertFromQuoteAsync(10);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*Aprovado*");
    }

    [Fact]
    public async Task ConvertFromQuote_DuplicateOrder_ThrowsValidationException()
    {
        var quoteRepo = new TrackingQuoteRepository();
        var orderRepo = new TrackingOrderRepository();
        var service = CreateService(quoteRepo, orderRepo, new FixedProductRepository(), DecorPermissions.OrdersConvertFromQuote);

        var quote = CreateQuote(quoteId: 1, customerId: 100);
        var section = CreateSection(sectionId: 10, quoteId: 1, status: QuoteSectionStatus.Approved, type: QuoteSectionType.Catalog);
        quote.Sections.Add(section);
        quoteRepo.SetQuoteForTest(quote);

        orderRepo.SetExistingOrderForSection(10, new Order { OrderID = 99, QuoteSectionID = 10 });

        var act = () => service.ConvertFromQuoteAsync(10);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*Já existe um pedido*");
    }

    [Fact]
    public async Task Approve_PendingApprovalOrder_UpdatesStatusAndDeadlines()
    {
        var orderRepo = new TrackingOrderRepository();
        var service = CreateService(new TrackingQuoteRepository(), orderRepo, new FixedProductRepository(), DecorPermissions.OrdersApprove);

        var order = new Order { OrderID = 1, Status = OrderStatus.PendingApproval, CustomerID = 10, QuoteSectionID = 100 };
        orderRepo.SetOrderForTest(order);

        var mfgDeadline = DateTime.UtcNow.AddDays(10);
        var instDeadline = DateTime.UtcNow.AddDays(15);

        await service.ApproveOrderAsync(1, requiresDownPayment: true, manufacturingDeadline: mfgDeadline, installationDeadline: instDeadline);

        order.Status.Should().Be(OrderStatus.Approved);
        order.RequiresDownPayment.Should().BeTrue();
        order.ManufacturingDeadline.Should().Be(mfgDeadline);
        order.InstallationDeadline.Should().Be(instDeadline);
    }

    [Fact]
    public async Task Approve_AlreadyApprovedOrder_ThrowsValidationException()
    {
        var orderRepo = new TrackingOrderRepository();
        var service = CreateService(new TrackingQuoteRepository(), orderRepo, new FixedProductRepository(), DecorPermissions.OrdersApprove);

        var order = new Order { OrderID = 1, Status = OrderStatus.Approved, CustomerID = 10, QuoteSectionID = 100 };
        orderRepo.SetOrderForTest(order);

        var act = () => service.ApproveOrderAsync(1, requiresDownPayment: false);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*Pendente de Aprovação*");
    }

    [Fact]
    public async Task Cancel_PendingApprovalOrApprovedOrder_TransitionsToCancelledAndReleasesActiveReservations()
    {
        var orderRepo = new TrackingOrderRepository();
        var reservationService = new TrackingStockReservationService();
        var service = CreateService(new TrackingQuoteRepository(), orderRepo, new FixedProductRepository(), reservationService, DecorPermissions.OrdersCancel);

        var order = new Order { OrderID = 1, Status = OrderStatus.Approved, CustomerID = 10, QuoteSectionID = 100 };
        orderRepo.SetOrderForTest(order);

        await service.CancelOrderAsync(1);

        order.Status.Should().Be(OrderStatus.Cancelled);
        reservationService.ReleasedOrders.Should().Contain(1);
    }

    [Fact]
    public async Task Cancel_AlreadyCancelledOrder_ThrowsValidationException()
    {
        var orderRepo = new TrackingOrderRepository();
        var service = CreateService(new TrackingQuoteRepository(), orderRepo, new FixedProductRepository(), DecorPermissions.OrdersCancel);

        var order = new Order { OrderID = 1, Status = OrderStatus.Cancelled, CustomerID = 10, QuoteSectionID = 100 };
        orderRepo.SetOrderForTest(order);

        var act = () => service.CancelOrderAsync(1);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*cancelado*");
    }

    [Fact]
    public async Task SendItemToProduction_ValidCustomGoodItem_SetsProductionTimestampAndEmployee()
    {
        var orderRepo = new TrackingOrderRepository();
        var productRepo = new FixedProductRepository();
        var service = CreateService(new TrackingQuoteRepository(), orderRepo, productRepo, DecorPermissions.OrdersSendToProduction);

        var order = new Order { OrderID = 10, Status = OrderStatus.Approved, OrderType = OrderType.Custom, CustomerID = 1, QuoteSectionID = 2 };
        var orderItem = new OrderItem { OrderItemID = 50, OrderID = 10, ProductID = 5, Quantity = 1, UnitPrice = 100 };
        order.Items.Add(orderItem);

        orderRepo.SetOrderForTest(order);
        orderRepo.SetOrderItemForTest(orderItem);
        productRepo.SetProduct(new Product { ProductID = 5, ProductType = ProductType.Good });

        await service.SendItemToProductionAsync(orderItemId: 50, sentToProductionByEmployeeID: 7);

        orderItem.SentToProductionAt.Should().NotBeNull();
        orderItem.SentToProductionByEmployeeID.Should().Be(7);
    }

    [Fact]
    public async Task SendItemToProduction_ServiceProduct_ThrowsValidationException()
    {
        var orderRepo = new TrackingOrderRepository();
        var productRepo = new FixedProductRepository();
        var service = CreateService(new TrackingQuoteRepository(), orderRepo, productRepo, DecorPermissions.OrdersSendToProduction);

        var order = new Order { OrderID = 10, Status = OrderStatus.Approved, OrderType = OrderType.Custom, CustomerID = 1, QuoteSectionID = 2 };
        var orderItem = new OrderItem { OrderItemID = 50, OrderID = 10, ProductID = 5, Quantity = 1, UnitPrice = 100 };
        order.Items.Add(orderItem);

        orderRepo.SetOrderForTest(order);
        orderRepo.SetOrderItemForTest(orderItem);
        productRepo.SetProduct(new Product { ProductID = 5, ProductType = ProductType.Service });

        var act = () => service.SendItemToProductionAsync(orderItemId: 50, sentToProductionByEmployeeID: 7);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*Serviço*");
    }

    [Fact]
    public async Task SendItemToProduction_CatalogOrder_ThrowsValidationException()
    {
        var orderRepo = new TrackingOrderRepository();
        var productRepo = new FixedProductRepository();
        var service = CreateService(new TrackingQuoteRepository(), orderRepo, productRepo, DecorPermissions.OrdersSendToProduction);

        var order = new Order { OrderID = 10, Status = OrderStatus.Approved, OrderType = OrderType.Catalog, CustomerID = 1, QuoteSectionID = 2 };
        var orderItem = new OrderItem { OrderItemID = 50, OrderID = 10, ProductID = 5, Quantity = 1, UnitPrice = 100 };
        order.Items.Add(orderItem);

        orderRepo.SetOrderForTest(order);
        orderRepo.SetOrderItemForTest(orderItem);
        productRepo.SetProduct(new Product { ProductID = 5, ProductType = ProductType.Good });

        var act = () => service.SendItemToProductionAsync(orderItemId: 50, sentToProductionByEmployeeID: 7);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*sob encomenda*");
    }

    [Fact]
    public async Task OrderService_WithoutPermission_ThrowsUnauthorizedAccessException()
    {
        var service = CreateService(new TrackingQuoteRepository(), new TrackingOrderRepository(), new FixedProductRepository());

        var act = () => service.GetAllOrdersAsync();

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    private static OrderService CreateService(
        IQuoteRepository quoteRepo,
        IOrderRepository orderRepo,
        IProductRepository productRepo,
        params string[] permissions)
    {
        return CreateService(quoteRepo, orderRepo, productRepo, new TrackingStockReservationService(), permissions);
    }

    private static OrderService CreateService(
        IQuoteRepository quoteRepo,
        IOrderRepository orderRepo,
        IProductRepository productRepo,
        IStockReservationService stockReservationService,
        params string[] permissions)
    {
        return new OrderService(
            orderRepo,
            quoteRepo,
            productRepo,
            stockReservationService,
            new OrderDTOValidator(),
            new OrderRepositoryValidator(orderRepo),
            new FixedAuthorizationService(permissions));
    }

    private static Quote CreateQuote(int quoteId, int customerId) => new()
    {
        QuoteID = quoteId,
        CustomerID = customerId,
        CreatedByEmployeeID = 1,
        SourceType = QuoteSourceType.DirectCapture,
        CreatedAt = DateTime.UtcNow,
        Sections = []
    };

    private static QuoteSection CreateSection(int sectionId, int quoteId, QuoteSectionStatus status, QuoteSectionType type) => new()
    {
        QuoteSectionID = sectionId,
        QuoteID = quoteId,
        SectionType = type,
        Status = status,
        CreatedAt = DateTime.UtcNow,
        Items = []
    };

    private static QuoteItem CreateQuoteItem(int itemId, int sectionId, int productId, decimal unitPrice) => new()
    {
        QuoteItemID = itemId,
        QuoteSectionID = sectionId,
        ProductID = productId,
        Quantity = 1m,
        UnitPrice = unitPrice,
        HasInstallationService = false,
        SpecificationValues = []
    };

    private sealed class FixedAuthorizationService(params string[] permissions) : IAuthorizationService
    {
        private readonly HashSet<string> _permissions = permissions.ToHashSet(StringComparer.OrdinalIgnoreCase);
        public bool HasPermission(string permissionCode) => _permissions.Contains(permissionCode);
        public bool CanView(string resource) => HasPermission($"{resource}.View");
        public bool CanCreate(string resource) => HasPermission($"{resource}.Create");
        public bool CanEdit(string resource) => HasPermission($"{resource}.Edit");
        public bool CanDelete(string resource) => HasPermission($"{resource}.Delete");
    }

    private sealed class FixedProductRepository : IProductRepository
    {
        private Product? _product;
        public void SetProduct(Product product) => _product = product;

        public int Save(Product entity) => 1;
        public int Delete(int id) => 1;
        public IEnumerable<Product> SearchGetBy(string? arg = null) => _product != null ? [_product] : [];
        public Task<int> SaveAsync(Product entity, CancellationToken cancellationToken = default) => Task.FromResult(1);
        public Task<int> DeleteAsync(int id, CancellationToken cancellationToken = default) => Task.FromResult(1);
        public Task<IReadOnlyList<Product>> SearchGetByAsync(string? arg = null, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Product>>(_product != null ? [_product] : []);
        public bool BrandExists(int marcaID) => true;
        public bool SubgroupExists(int subgroupID) => true;
    }

    private sealed class TrackingQuoteRepository : IQuoteRepository
    {
        private Quote? _quote;
        public void SetQuoteForTest(Quote quote) => _quote = quote;

        public int Save(Quote entity) => 1;
        public int Delete(int id) => 1;
        public IEnumerable<Quote> SearchGetBy(string? arg = null) => [];
        public Task<int> SaveAsync(Quote entity, CancellationToken cancellationToken = default) => Task.FromResult(1);
        public Task<int> DeleteAsync(int id, CancellationToken cancellationToken = default) => Task.FromResult(1);
        public Task<IReadOnlyList<Quote>> SearchGetByAsync(string? arg = null, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Quote>>([]);
        public Task<Quote?> GetByIdAsync(int quoteId, CancellationToken cancellationToken = default) => Task.FromResult<Quote?>(_quote);
        public Task<Quote?> GetCompleteQuoteAsync(int quoteId, CancellationToken cancellationToken = default) => Task.FromResult<Quote?>(_quote);
        public Task<QuoteSection?> GetSectionByItemIdAsync(int quoteItemId, CancellationToken cancellationToken = default) => Task.FromResult(_quote?.Sections.FirstOrDefault(s => s.Items.Any(i => i.QuoteItemID == quoteItemId)));
        public Task<QuoteSection?> GetSectionByIdAsync(int quoteSectionId, CancellationToken cancellationToken = default) => Task.FromResult(_quote?.Sections.FirstOrDefault(s => s.QuoteSectionID == quoteSectionId));
        public Task<QuoteItem?> GetItemByIdAsync(int quoteItemId, CancellationToken cancellationToken = default) => Task.FromResult(_quote?.Sections.SelectMany(s => s.Items).FirstOrDefault(i => i.QuoteItemID == quoteItemId));
        public Task<int> SaveSectionAsync(QuoteSection section, CancellationToken cancellationToken = default) => Task.FromResult(1);
        public Task<int> SaveItemAsync(QuoteItem item, CancellationToken cancellationToken = default) => Task.FromResult(1);
        public Task<int> SaveSpecificationValueAsync(QuoteItemSpecificationValue value, CancellationToken cancellationToken = default) => Task.FromResult(1);
    }

    private sealed class TrackingOrderRepository : IOrderRepository
    {
        private Order? _order;
        private OrderItem? _orderItem;
        private readonly Dictionary<int, Order> _ordersBySection = [];
        private int _nextOrderId = 1;
        private int _nextItemId = 1;
        private int _nextSpecId = 1;

        public void SetOrderForTest(Order order) => _order = order;
        public void SetOrderItemForTest(OrderItem item) => _orderItem = item;
        public void SetExistingOrderForSection(int quoteSectionId, Order order) => _ordersBySection[quoteSectionId] = order;

        public int Save(Order entity) => 1;
        public int Delete(int id) => 1;
        public IEnumerable<Order> SearchGetBy(string? arg = null) => _order != null ? [_order] : [];
        public Task<int> SaveAsync(Order entity, CancellationToken cancellationToken = default)
        {
            if (entity.OrderID == 0) entity.OrderID = _nextOrderId++;
            _order = entity;
            _ordersBySection[entity.QuoteSectionID] = entity;
            return Task.FromResult(1);
        }
        public Task<int> DeleteAsync(int id, CancellationToken cancellationToken = default) => Task.FromResult(1);
        public Task<IReadOnlyList<Order>> SearchGetByAsync(string? arg = null, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Order>>(_order != null ? [_order] : []);

        public Task<Order?> GetByIdAsync(int orderId, CancellationToken cancellationToken = default) => Task.FromResult<Order?>(_order);
        public Task<Order?> GetCompleteOrderAsync(int orderId, CancellationToken cancellationToken = default) => Task.FromResult<Order?>(_order);
        public Task<Order?> GetByQuoteSectionIdAsync(int quoteSectionId, CancellationToken cancellationToken = default)
            => Task.FromResult(_ordersBySection.TryGetValue(quoteSectionId, out var o) ? o : null);
        public Task<OrderItem?> GetOrderItemByIdAsync(int orderItemId, CancellationToken cancellationToken = default) => Task.FromResult<OrderItem?>(_orderItem);

        public Task<int> SaveOrderItemAsync(OrderItem item, CancellationToken cancellationToken = default)
        {
            if (item.OrderItemID == 0) item.OrderItemID = _nextItemId++;
            _orderItem = item;
            return Task.FromResult(1);
        }

        public Task<int> SaveSpecificationValueAsync(OrderItemSpecificationValue value, CancellationToken cancellationToken = default)
        {
            if (value.ValueID == 0) value.ValueID = _nextSpecId++;
            return Task.FromResult(1);
        }
    }

    private sealed class TrackingStockReservationService : IStockReservationService
    {
        public List<int> ReleasedOrders { get; } = [];

        public Task<StockReservationDTO> CreateReservationAsync(int orderItemId, int stockLocationId, int createdByEmployeeId, decimal quantity, CancellationToken cancellationToken = default)
            => Task.FromResult(new StockReservationDTO(1, orderItemId, 1, stockLocationId, quantity, StockReservationStatus.Active, createdByEmployeeId, DateTime.UtcNow, null));

        public Task ReleaseReservationAsync(int reservationId, int? releasedByEmployeeId = null, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task ReleaseActiveReservationsForOrderAsync(int orderId, CancellationToken cancellationToken = default)
        {
            ReleasedOrders.Add(orderId);
            return Task.CompletedTask;
        }

        public Task<StockReservationDTO> GetByIdAsync(int reservationId, CancellationToken cancellationToken = default)
            => Task.FromResult(new StockReservationDTO(reservationId, 1, 1, 1, 1m, StockReservationStatus.Active, 1, DateTime.UtcNow, null));

        public Task<IEnumerable<StockReservationDTO>> GetByOrderItemIdAsync(int orderItemId, CancellationToken cancellationToken = default)
            => Task.FromResult<IEnumerable<StockReservationDTO>>([]);

        public Task<IEnumerable<StockReservationDTO>> GetByOrderIdAsync(int orderId, CancellationToken cancellationToken = default)
            => Task.FromResult<IEnumerable<StockReservationDTO>>([]);

        public Task<IEnumerable<StockReservationDTO>> GetAllAsync(int page = 1, int pageSize = 100, CancellationToken cancellationToken = default)
            => Task.FromResult<IEnumerable<StockReservationDTO>>([]);

        public Task<IEnumerable<StockReservationDTO>> SearchAsync(string? searchTerm, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default)
            => Task.FromResult<IEnumerable<StockReservationDTO>>([]);
    }
}
