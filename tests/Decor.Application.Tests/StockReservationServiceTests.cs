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

public sealed class StockReservationServiceTests
{
    [Fact]
    public async Task CreateReservation_ValidDataAndApprovedOrder_CreatesActiveReservation()
    {
        var testContext = new TestContext();
        testContext.SeedApprovedOrderWithItem(orderId: 1, orderItemId: 10, productId: 100);
        testContext.SeedProduct(productId: 100, productType: ProductType.Good);
        testContext.SeedStockLocation(locationId: 5, locationType: StockLocationType.Empresa, partnerId: null);
        testContext.SeedStockBalance(productId: 100, locationId: 5, quantity: 50m);

        var service = testContext.CreateService(DecorPermissions.StockReservationsCreate);

        var result = await service.CreateReservationAsync(
            orderItemId: 10,
            stockLocationId: 5,
            createdByEmployeeId: 1,
            quantity: 15m
        );

        result.Should().NotBeNull();
        result.OrderItemID.Should().Be(10);
        result.ProductID.Should().Be(100);
        result.StockLocationID.Should().Be(5);
        result.Quantity.Should().Be(15m);
        result.Status.Should().Be(StockReservationStatus.Active);
        result.CreatedByEmployeeID.Should().Be(1);
        result.ReleasedAt.Should().BeNull();
    }

    [Fact]
    public async Task CreateReservation_OrderNotApproved_ThrowsValidationException()
    {
        var testContext = new TestContext();
        testContext.SeedOrderWithItem(orderId: 1, orderItemId: 10, productId: 100, status: OrderStatus.PendingApproval);
        testContext.SeedProduct(productId: 100, productType: ProductType.Good);
        testContext.SeedStockLocation(locationId: 5, locationType: StockLocationType.Empresa, partnerId: null);
        testContext.SeedStockBalance(productId: 100, locationId: 5, quantity: 50m);

        var service = testContext.CreateService(DecorPermissions.StockReservationsCreate);

        var act = () => service.CreateReservationAsync(10, 5, 1, 15m);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*Aprovado*");
    }

    [Fact]
    public async Task CreateReservation_ProductTypeService_ThrowsValidationException()
    {
        var testContext = new TestContext();
        testContext.SeedApprovedOrderWithItem(orderId: 1, orderItemId: 10, productId: 100);
        testContext.SeedProduct(productId: 100, productType: ProductType.Service);
        testContext.SeedStockLocation(locationId: 5, locationType: StockLocationType.Empresa, partnerId: null);
        testContext.SeedStockBalance(productId: 100, locationId: 5, quantity: 50m);

        var service = testContext.CreateService(DecorPermissions.StockReservationsCreate);

        var act = () => service.CreateReservationAsync(10, 5, 1, 15m);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*Serviço*");
    }

    [Fact]
    public async Task CreateReservation_DuplicateActiveReservationForOrderItem_ThrowsValidationException()
    {
        var testContext = new TestContext();
        testContext.SeedApprovedOrderWithItem(orderId: 1, orderItemId: 10, productId: 100);
        testContext.SeedProduct(productId: 100, productType: ProductType.Good);
        testContext.SeedStockLocation(locationId: 5, locationType: StockLocationType.Empresa, partnerId: null);
        testContext.SeedStockBalance(productId: 100, locationId: 5, quantity: 50m);
        testContext.SeedReservation(new StockReservation
        {
            ReservationID = 1,
            OrderItemID = 10,
            ProductID = 100,
            StockLocationID = 5,
            Quantity = 10m,
            Status = StockReservationStatus.Active
        });

        var service = testContext.CreateService(DecorPermissions.StockReservationsCreate);

        var act = () => service.CreateReservationAsync(10, 5, 1, 5m);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*Já existe uma reserva ativa*");
    }

    [Fact]
    public async Task CreateReservation_PartnerStockLocation_ThrowsValidationException()
    {
        var testContext = new TestContext();
        testContext.SeedApprovedOrderWithItem(orderId: 1, orderItemId: 10, productId: 100);
        testContext.SeedProduct(productId: 100, productType: ProductType.Good);
        testContext.SeedStockLocation(locationId: 8, locationType: StockLocationType.Parceiro, partnerId: 3);
        testContext.SeedStockBalance(productId: 100, locationId: 8, quantity: 50m);

        var service = testContext.CreateService(DecorPermissions.StockReservationsCreate);

        var act = () => service.CreateReservationAsync(10, 8, 1, 10m);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*depósito próprio da empresa*");
    }

    [Fact]
    public async Task CreateReservation_InsufficientAvailableBalanceConsideringOtherActiveReservations_ThrowsValidationException()
    {
        var testContext = new TestContext();
        testContext.SeedApprovedOrderWithItem(orderId: 1, orderItemId: 10, productId: 100);
        testContext.SeedProduct(productId: 100, productType: ProductType.Good);
        testContext.SeedStockLocation(locationId: 5, locationType: StockLocationType.Empresa, partnerId: null);
        testContext.SeedStockBalance(productId: 100, locationId: 5, quantity: 20m);
        // Outro item já reservou 15m
        testContext.SeedReservation(new StockReservation
        {
            ReservationID = 1,
            OrderItemID = 99,
            ProductID = 100,
            StockLocationID = 5,
            Quantity = 15m,
            Status = StockReservationStatus.Active
        });

        var service = testContext.CreateService(DecorPermissions.StockReservationsCreate);

        // Saldo total = 20, reservado = 15, disponível = 5. Tentando reservar 10.
        var act = () => service.CreateReservationAsync(10, 5, 1, 10m);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*Saldo insuficiente*");
    }

    [Fact]
    public async Task CreateReservation_WithoutPermission_ThrowsUnauthorizedAccessException()
    {
        var testContext = new TestContext();
        var service = testContext.CreateService();

        var act = () => service.CreateReservationAsync(1, 1, 1, 10m);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task ReleaseReservation_ActiveReservation_SetsStatusReleasedAndTimestamp()
    {
        var testContext = new TestContext();
        var reservation = new StockReservation
        {
            ReservationID = 10,
            OrderItemID = 1,
            ProductID = 100,
            StockLocationID = 5,
            Quantity = 10m,
            Status = StockReservationStatus.Active,
            CreatedAt = DateTime.UtcNow.AddHours(-1)
        };
        testContext.SeedReservation(reservation);

        var service = testContext.CreateService(DecorPermissions.StockReservationsRelease);

        await service.ReleaseReservationAsync(10, releasedByEmployeeId: 2);

        reservation.Status.Should().Be(StockReservationStatus.Released);
        reservation.ReleasedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task ReleaseReservation_AlreadyReleased_ThrowsValidationException()
    {
        var testContext = new TestContext();
        var reservation = new StockReservation
        {
            ReservationID = 10,
            OrderItemID = 1,
            ProductID = 100,
            StockLocationID = 5,
            Quantity = 10m,
            Status = StockReservationStatus.Released,
            CreatedAt = DateTime.UtcNow.AddHours(-2),
            ReleasedAt = DateTime.UtcNow.AddHours(-1)
        };
        testContext.SeedReservation(reservation);

        var service = testContext.CreateService(DecorPermissions.StockReservationsRelease);

        var act = () => service.ReleaseReservationAsync(10);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*Apenas reservas ativas podem ser liberadas*");
    }

    [Fact]
    public async Task ReleaseActiveReservationsForOrder_ReleasesAllActiveReservations()
    {
        var testContext = new TestContext();
        testContext.SeedApprovedOrderWithItem(orderId: 1, orderItemId: 10, productId: 100);
        testContext.SeedApprovedOrderWithItem(orderId: 1, orderItemId: 20, productId: 200);

        var r1 = new StockReservation { ReservationID = 1, OrderItemID = 10, ProductID = 100, StockLocationID = 5, Quantity = 5, Status = StockReservationStatus.Active };
        var r2 = new StockReservation { ReservationID = 2, OrderItemID = 20, ProductID = 200, StockLocationID = 5, Quantity = 8, Status = StockReservationStatus.Active };
        testContext.SeedReservation(r1);
        testContext.SeedReservation(r2);

        var service = testContext.CreateService();

        await service.ReleaseActiveReservationsForOrderAsync(1);

        r1.Status.Should().Be(StockReservationStatus.Released);
        r1.ReleasedAt.Should().NotBeNull();
        r2.Status.Should().Be(StockReservationStatus.Released);
        r2.ReleasedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task RegisterTransfer_WithOrderItemId_StoresOrderItemIdOnMovements()
    {
        var repo = new InMemoryStockMovementRepository();
        var auth = new FixedAuth(DecorPermissions.StockMovementsTransfer);
        var service = new StockMovementService(repo, auth);

        var transferId = await service.RegisterTransferAsync(
            productId: 100,
            sourceStockLocationId: 1,
            destinationStockLocationId: 2,
            quantity: 10m,
            performedByEmployeeId: 3,
            notes: "Transferência para pedido",
            orderItemId: 55
        );

        transferId.Should().NotBeEmpty();
        repo.Movements.Should().HaveCount(2);
        repo.Movements.All(m => m.OrderItemID == 55).Should().BeTrue();
    }

    private sealed class TestContext
    {
        public InMemoryStockReservationRepository ReservationRepo { get; } = new();
        public InMemoryOrderRepository OrderRepo { get; } = new();
        public InMemoryProductRepository ProductRepo { get; } = new();
        public InMemoryStockLocationRepository StockLocationRepo { get; } = new();
        public InMemoryStockBalanceRepository StockBalanceRepo { get; } = new();

        public StockReservationService CreateService(params string[] permissions)
        {
            return new StockReservationService(
                ReservationRepo,
                OrderRepo,
                ProductRepo,
                StockLocationRepo,
                StockBalanceRepo,
                new StockReservationDTOValidator(),
                new StockReservationRepositoryValidator(ReservationRepo),
                new FixedAuth(permissions)
            );
        }

        public void SeedApprovedOrderWithItem(int orderId, int orderItemId, int productId)
        {
            SeedOrderWithItem(orderId, orderItemId, productId, OrderStatus.Approved);
        }

        public void SeedOrderWithItem(int orderId, int orderItemId, int productId, OrderStatus status)
        {
            var order = new Order { OrderID = orderId, Status = status, CustomerID = 1, QuoteSectionID = 1 };
            var item = new OrderItem { OrderItemID = orderItemId, OrderID = orderId, ProductID = productId, Quantity = 10m, UnitPrice = 50m };
            order.Items.Add(item);
            OrderRepo.Orders[orderId] = order;
            OrderRepo.OrderItems[orderItemId] = item;
        }

        public void SeedProduct(int productId, ProductType productType)
        {
            ProductRepo.Products[productId] = new Product
            {
                ProductID = productId,
                Description = "Produto Teste",
                ProductType = productType,
                IsActive = true
            };
        }

        public void SeedStockLocation(int locationId, StockLocationType locationType, int? partnerId)
        {
            StockLocationRepo.Locations[locationId] = new StockLocation
            {
                StockLocationID = locationId,
                Name = "Depósito Teste",
                LocationType = locationType,
                PartnerID = partnerId,
                IsActive = true
            };
        }

        public void SeedStockBalance(int productId, int locationId, decimal quantity)
        {
            StockBalanceRepo.Balances[(productId, locationId)] = new StockBalance
            {
                ProductID = productId,
                StockLocationID = locationId,
                Quantity = quantity,
                UpdatedAt = DateTime.UtcNow
            };
        }

        public void SeedReservation(StockReservation reservation)
        {
            ReservationRepo.Reservations[reservation.ReservationID] = reservation;
        }
    }

    private sealed class FixedAuth(params string[] permissions) : IAuthorizationService
    {
        private readonly HashSet<string> _permissions = permissions.ToHashSet(StringComparer.OrdinalIgnoreCase);
        public bool HasPermission(string permissionCode) => _permissions.Contains(permissionCode);
        public bool CanView(string resource) => HasPermission($"{resource}.View");
        public bool CanCreate(string resource) => HasPermission($"{resource}.Create");
        public bool CanEdit(string resource) => HasPermission($"{resource}.Edit");
        public bool CanDelete(string resource) => HasPermission($"{resource}.Delete");
    }

    private sealed class InMemoryStockReservationRepository : IStockReservationRepository
    {
        public Dictionary<int, StockReservation> Reservations { get; } = [];
        private int _nextId = 1;

        public int Save(StockReservation entity) => 1;
        public int Delete(int id) { Reservations.Remove(id); return 1; }
        public IEnumerable<StockReservation> SearchGetBy(string? arg = null) => Reservations.Values;
        public Task<int> SaveAsync(StockReservation entity, CancellationToken cancellationToken = default)
        {
            if (entity.ReservationID == 0) entity.ReservationID = _nextId++;
            Reservations[entity.ReservationID] = entity;
            return Task.FromResult(1);
        }
        public Task<int> DeleteAsync(int id, CancellationToken cancellationToken = default) { Reservations.Remove(id); return Task.FromResult(1); }
        public Task<IReadOnlyList<StockReservation>> SearchGetByAsync(string? arg = null, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<StockReservation>>(Reservations.Values.ToList());
        public Task<StockReservation?> GetByIdAsync(int reservationId, CancellationToken cancellationToken = default)
            => Task.FromResult(Reservations.TryGetValue(reservationId, out var r) ? r : null);
        public Task<IEnumerable<StockReservation>> GetByOrderItemIdAsync(int orderItemId, CancellationToken cancellationToken = default)
            => Task.FromResult<IEnumerable<StockReservation>>(Reservations.Values.Where(r => r.OrderItemID == orderItemId).ToList());
        public Task<IReadOnlyList<StockReservation>> GetByOrderIdAsync(int orderId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<StockReservation>>(Reservations.Values.ToList());
        public Task<IReadOnlyList<StockReservation>> GetActiveByOrderIdAsync(int orderId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<StockReservation>>(Reservations.Values.Where(r => r.Status == StockReservationStatus.Active).ToList());
        public Task<StockReservation?> GetActiveByOrderItemIdAsync(int orderItemId, CancellationToken cancellationToken = default)
            => Task.FromResult(Reservations.Values.FirstOrDefault(r => r.OrderItemID == orderItemId && r.Status == StockReservationStatus.Active));
        public Task<IEnumerable<StockReservation>> GetActiveByProductAndLocationAsync(int productId, int stockLocationId, CancellationToken cancellationToken = default)
            => Task.FromResult<IEnumerable<StockReservation>>(Reservations.Values.Where(r => r.ProductID == productId && r.StockLocationID == stockLocationId && r.Status == StockReservationStatus.Active).ToList());
        public Task<decimal> GetTotalActiveReservedQuantityAsync(int productId, int stockLocationId, CancellationToken cancellationToken = default)
            => Task.FromResult(Reservations.Values.Where(r => r.ProductID == productId && r.StockLocationID == stockLocationId && r.Status == StockReservationStatus.Active).Sum(r => r.Quantity));
    }

    private sealed class InMemoryOrderRepository : IOrderRepository
    {
        public Dictionary<int, Order> Orders { get; } = [];
        public Dictionary<int, OrderItem> OrderItems { get; } = [];

        public int Save(Order entity) => 1;
        public int Delete(int id) => 1;
        public IEnumerable<Order> SearchGetBy(string? arg = null) => Orders.Values;
        public Task<int> SaveAsync(Order entity, CancellationToken cancellationToken = default) { Orders[entity.OrderID] = entity; return Task.FromResult(1); }
        public Task<int> DeleteAsync(int id, CancellationToken cancellationToken = default) => Task.FromResult(1);
        public Task<IReadOnlyList<Order>> SearchGetByAsync(string? arg = null, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Order>>(Orders.Values.ToList());
        public Task<Order?> GetByIdAsync(int orderId, CancellationToken cancellationToken = default)
            => Task.FromResult(Orders.TryGetValue(orderId, out var o) ? o : null);
        public Task<Order?> GetCompleteOrderAsync(int orderId, CancellationToken cancellationToken = default)
            => Task.FromResult(Orders.TryGetValue(orderId, out var o) ? o : null);
        public Task<Order?> GetByQuoteSectionIdAsync(int quoteSectionId, CancellationToken cancellationToken = default)
            => Task.FromResult(Orders.Values.FirstOrDefault(o => o.QuoteSectionID == quoteSectionId));
        public Task<OrderItem?> GetOrderItemByIdAsync(int orderItemId, CancellationToken cancellationToken = default)
            => Task.FromResult(OrderItems.TryGetValue(orderItemId, out var oi) ? oi : null);
        public Task<int> SaveOrderItemAsync(OrderItem item, CancellationToken cancellationToken = default) { OrderItems[item.OrderItemID] = item; return Task.FromResult(1); }
        public Task<int> SaveSpecificationValueAsync(OrderItemSpecificationValue value, CancellationToken cancellationToken = default) => Task.FromResult(1);
    }

    private sealed class InMemoryProductRepository : IProductRepository
    {
        public Dictionary<int, Product> Products { get; } = [];

        public int Save(Product entity) => 1;
        public int Delete(int id) => 1;
        public IEnumerable<Product> SearchGetBy(string? arg = null) => Products.Values;
        public Task<int> SaveAsync(Product entity, CancellationToken cancellationToken = default) => Task.FromResult(1);
        public Task<int> DeleteAsync(int id, CancellationToken cancellationToken = default) => Task.FromResult(1);
        public Task<IReadOnlyList<Product>> SearchGetByAsync(string? arg = null, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Product>>(Products.Values.ToList());
        public bool BrandExists(int marcaId) => true;
        public bool SubgroupExists(int subgroupId) => true;
    }

    private sealed class InMemoryStockLocationRepository : IStockLocationRepository
    {
        public Dictionary<int, StockLocation> Locations { get; } = [];

        public int Save(StockLocation entity) => 1;
        public int Delete(int id) => 1;
        public IEnumerable<StockLocation> SearchGetBy(string? arg = null) => Locations.Values;
        public Task<int> SaveAsync(StockLocation entity, CancellationToken cancellationToken = default) => Task.FromResult(1);
        public Task<int> DeleteAsync(int id, CancellationToken cancellationToken = default) => Task.FromResult(1);
        public Task<IReadOnlyList<StockLocation>> SearchGetByAsync(string? arg = null, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<StockLocation>>(Locations.Values.ToList());
    }

    private sealed class InMemoryStockBalanceRepository : IStockBalanceRepository
    {
        public Dictionary<(int ProductId, int LocationId), StockBalance> Balances { get; } = [];

        public Task<StockBalance?> GetByProductAndLocationAsync(int productId, int stockLocationId, CancellationToken cancellationToken = default)
            => Task.FromResult(Balances.TryGetValue((productId, stockLocationId), out var b) ? b : null);
        public Task<IEnumerable<StockBalance>> GetByProductAsync(int productId, CancellationToken cancellationToken = default)
            => Task.FromResult<IEnumerable<StockBalance>>(Balances.Values.Where(b => b.ProductID == productId).ToList());
        public Task<IEnumerable<StockBalance>> GetByLocationAsync(int stockLocationId, CancellationToken cancellationToken = default)
            => Task.FromResult<IEnumerable<StockBalance>>(Balances.Values.Where(b => b.StockLocationID == stockLocationId).ToList());
    }

    private sealed class InMemoryStockMovementRepository : IStockMovementRepository
    {
        public List<StockMovement> Movements { get; } = [];

        public Task<int> RegisterMovementAsync(StockMovement movement, CancellationToken cancellationToken = default)
        {
            Movements.Add(movement);
            return Task.FromResult(Movements.Count);
        }

        public Task<Guid> RegisterTransferAsync(StockMovement outboundMovement, StockMovement inboundMovement, CancellationToken cancellationToken = default)
        {
            var id = Guid.NewGuid();
            outboundMovement.TransferID = id;
            inboundMovement.TransferID = id;
            Movements.Add(outboundMovement);
            Movements.Add(inboundMovement);
            return Task.FromResult(id);
        }

        public Task<StockMovement?> GetByIdAsync(int stockMovementId, CancellationToken cancellationToken = default)
            => Task.FromResult(Movements.ElementAtOrDefault(stockMovementId - 1));

        public Task<IEnumerable<StockMovement>> GetByProductAsync(int productId, CancellationToken cancellationToken = default)
            => Task.FromResult<IEnumerable<StockMovement>>(Movements.Where(m => m.ProductID == productId).ToList());

        public Task<IEnumerable<StockMovement>> GetByTransferIdAsync(Guid transferId, CancellationToken cancellationToken = default)
            => Task.FromResult<IEnumerable<StockMovement>>(Movements.Where(m => m.TransferID == transferId).ToList());

        public Task<int> UpdateReviewAsync(Guid transferId, StockMovementReviewStatus reviewStatus, int reviewedByEmployeeId, DateTime reviewedAt, CancellationToken cancellationToken = default)
            => Task.FromResult(2);
    }
}
