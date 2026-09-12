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

public sealed class OrderInstallmentServiceTests
{
    [Fact]
    public async Task CreateInstallmentPlan_OrderNotApproved_ThrowsValidationException()
    {
        var testContext = new TestContext();
        testContext.SeedOrder(orderId: 10, status: OrderStatus.PendingApproval, itemAmount: 100m);
        testContext.SeedPaymentMethod(pmId: 1, name: "PIX", isActive: true);

        var service = testContext.CreateService(DecorPermissions.OrderInstallmentsCreatePlan);

        var items = new[] { new InstallmentItemInputDTO(1, 100m, DateTime.UtcNow.AddDays(10)) };
        var act = () => service.CreateInstallmentPlanAsync(10, items);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*Aprovado*");
    }

    [Fact]
    public async Task CreateInstallmentPlan_AlreadyExists_ThrowsValidationException()
    {
        var testContext = new TestContext();
        testContext.SeedOrder(orderId: 10, status: OrderStatus.Approved, itemAmount: 100m);
        testContext.SeedPaymentMethod(pmId: 1, name: "PIX", isActive: true);
        testContext.SeedInstallment(installmentId: 1, orderId: 10, amount: 100m, status: OrderInstallmentStatus.Pending);

        var service = testContext.CreateService(DecorPermissions.OrderInstallmentsCreatePlan);

        var items = new[] { new InstallmentItemInputDTO(1, 100m, DateTime.UtcNow.AddDays(10)) };
        var act = () => service.CreateInstallmentPlanAsync(10, items);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*Já existe um plano*");
    }

    [Fact]
    public async Task CreateInstallmentPlan_TotalMismatches_ThrowsValidationException()
    {
        var testContext = new TestContext();
        testContext.SeedOrder(orderId: 10, status: OrderStatus.Approved, itemAmount: 200m);
        testContext.SeedPaymentMethod(pmId: 1, name: "PIX", isActive: true);

        var service = testContext.CreateService(DecorPermissions.OrderInstallmentsCreatePlan);

        var items = new[] { new InstallmentItemInputDTO(1, 100m, DateTime.UtcNow.AddDays(10)) };
        var act = () => service.CreateInstallmentPlanAsync(10, items);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*diverge*");
    }

    [Fact]
    public async Task CreateInstallmentPlan_ValidApprovedOrder_CreatesPendingInstallmentsSequentially()
    {
        var testContext = new TestContext();
        testContext.SeedOrder(orderId: 10, status: OrderStatus.Approved, itemAmount: 300m);
        testContext.SeedPaymentMethod(pmId: 1, name: "PIX", isActive: true);
        testContext.SeedPaymentMethod(pmId: 2, name: "Boleto", isActive: true);

        var service = testContext.CreateService(DecorPermissions.OrderInstallmentsCreatePlan);

        var dueDate1 = DateTime.UtcNow.AddDays(10);
        var dueDate2 = DateTime.UtcNow.AddDays(40);

        var items = new[]
        {
            new InstallmentItemInputDTO(1, 150m, dueDate1),
            new InstallmentItemInputDTO(2, 150m, dueDate2)
        };

        var result = (await service.CreateInstallmentPlanAsync(10, items)).ToList();

        result.Should().HaveCount(2);
        result[0].InstallmentNumber.Should().Be(1);
        result[0].Amount.Should().Be(150m);
        result[0].Status.Should().Be(OrderInstallmentStatus.Pending);
        result[0].PaymentMethodID.Should().Be(1);

        result[1].InstallmentNumber.Should().Be(2);
        result[1].Amount.Should().Be(150m);
        result[1].Status.Should().Be(OrderInstallmentStatus.Pending);
        result[1].PaymentMethodID.Should().Be(2);
    }

    [Fact]
    public async Task RegisterPayment_PendingStatus_ChangesToPaid()
    {
        var testContext = new TestContext();
        testContext.SeedInstallment(installmentId: 5, orderId: 10, amount: 100m, status: OrderInstallmentStatus.Pending);

        var service = testContext.CreateService(DecorPermissions.OrderInstallmentsRegisterPayment, DecorPermissions.OrderInstallmentsView);

        await service.RegisterPaymentAsync(5, receivedByEmployeeId: 2);

        var updated = await service.GetInstallmentByIdAsync(5);
        updated.Status.Should().Be(OrderInstallmentStatus.Paid);
        updated.ReceivedByEmployeeID.Should().Be(2);
        updated.PaidAt.Should().NotBeNull();
    }

    [Fact]
    public async Task RegisterPayment_AlreadyPaid_ThrowsValidationException()
    {
        var testContext = new TestContext();
        testContext.SeedInstallment(installmentId: 5, orderId: 10, amount: 100m, status: OrderInstallmentStatus.Paid);

        var service = testContext.CreateService(DecorPermissions.OrderInstallmentsRegisterPayment);

        var act = () => service.RegisterPaymentAsync(5, receivedByEmployeeId: 2);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*Pendente ou Vencida*");
    }

    [Fact]
    public async Task MarkOverdue_PendingStatus_ChangesToOverdue()
    {
        var testContext = new TestContext();
        testContext.SeedInstallment(installmentId: 5, orderId: 10, amount: 100m, status: OrderInstallmentStatus.Pending);

        var service = testContext.CreateService(DecorPermissions.OrderInstallmentsMarkOverdue, DecorPermissions.OrderInstallmentsView);

        await service.MarkOverdueAsync(5);

        var updated = await service.GetInstallmentByIdAsync(5);
        updated.Status.Should().Be(OrderInstallmentStatus.Overdue);
    }

    [Fact]
    public async Task CancelInstallment_PendingOrOverdueStatus_ChangesToCancelled()
    {
        var testContext = new TestContext();
        testContext.SeedInstallment(installmentId: 5, orderId: 10, amount: 100m, status: OrderInstallmentStatus.Pending);
        testContext.SeedInstallment(installmentId: 6, orderId: 10, amount: 100m, status: OrderInstallmentStatus.Overdue);

        var service = testContext.CreateService(DecorPermissions.OrderInstallmentsCancel, DecorPermissions.OrderInstallmentsView);

        await service.CancelInstallmentAsync(5);
        await service.CancelInstallmentAsync(6);

        (await service.GetInstallmentByIdAsync(5)).Status.Should().Be(OrderInstallmentStatus.Cancelled);
        (await service.GetInstallmentByIdAsync(6)).Status.Should().Be(OrderInstallmentStatus.Cancelled);
    }

    private sealed class TestContext
    {
        public TrackingOrderInstallmentRepository InstallmentRepo { get; } = new();
        public TrackingOrderRepository OrderRepo { get; } = new();
        public TrackingPaymentMethodRepository PaymentMethodRepo { get; } = new();

        public OrderInstallmentService CreateService(params string[] permissions)
        {
            return new OrderInstallmentService(
                InstallmentRepo,
                OrderRepo,
                PaymentMethodRepo,
                new OrderInstallmentDTOValidator(),
                new OrderInstallmentRepositoryValidator(InstallmentRepo),
                new FixedAuthorizationService(permissions)
            );
        }

        public void SeedOrder(int orderId, OrderStatus status, decimal itemAmount)
        {
            var order = new Order
            {
                OrderID = orderId,
                Status = status,
                Items = new List<OrderItem>
                {
                    new OrderItem
                    {
                        OrderItemID = 1,
                        OrderID = orderId,
                        Quantity = 1m,
                        UnitPrice = itemAmount
                    }
                }
            };
            OrderRepo.Orders.Add(order);
        }

        public void SeedPaymentMethod(int pmId, string name, bool isActive)
        {
            PaymentMethodRepo.PaymentMethods.Add(new PaymentMethod
            {
                PaymentMethodID = pmId,
                Name = name,
                Timing = PaymentTiming.Cash,
                IsActive = isActive
            });
        }

        public void SeedInstallment(int installmentId, int orderId, decimal amount, OrderInstallmentStatus status)
        {
            InstallmentRepo.Installments.Add(new OrderInstallment
            {
                InstallmentID = installmentId,
                OrderID = orderId,
                PaymentMethodID = 1,
                InstallmentNumber = 1,
                Amount = amount,
                DueDate = DateTime.UtcNow.AddDays(10),
                Status = status
            });
        }
    }

    private sealed class TrackingOrderInstallmentRepository : IOrderInstallmentRepository
    {
        public List<OrderInstallment> Installments { get; } = [];
        private int _nextId = 1;

        public int Save(OrderInstallment entity)
        {
            if (entity.InstallmentID == 0) entity.InstallmentID = _nextId++;
            Installments.RemoveAll(i => i.InstallmentID == entity.InstallmentID);
            Installments.Add(entity);
            return 1;
        }

        public Task<int> SaveAsync(OrderInstallment entity, CancellationToken cancellationToken = default)
        {
            Save(entity);
            return Task.FromResult(1);
        }

        public int Delete(int id)
        {
            Installments.RemoveAll(i => i.InstallmentID == id);
            return 1;
        }

        public Task<int> DeleteAsync(int id, CancellationToken cancellationToken = default)
        {
            Delete(id);
            return Task.FromResult(1);
        }

        public IEnumerable<OrderInstallment> SearchGetBy(string? arg = null) => Installments;

        public Task<IReadOnlyList<OrderInstallment>> SearchGetByAsync(string? arg = null, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<OrderInstallment>>(Installments);

        public Task<IReadOnlyList<OrderInstallment>> GetByOrderIdAsync(int orderId, CancellationToken cancellationToken = default)
        {
            var list = Installments.Where(i => i.OrderID == orderId).OrderBy(i => i.InstallmentNumber).ToList();
            return Task.FromResult<IReadOnlyList<OrderInstallment>>(list);
        }

        public Task<OrderInstallment?> GetByIdAsync(int installmentId, CancellationToken cancellationToken = default)
        {
            var item = Installments.FirstOrDefault(i => i.InstallmentID == installmentId);
            return Task.FromResult(item);
        }
    }

    private sealed class TrackingOrderRepository : IOrderRepository
    {
        public List<Order> Orders { get; } = [];

        public int Save(Order entity) => 1;
        public int Delete(int id) => 1;
        public Task<int> DeleteAsync(int id, CancellationToken cancellationToken = default) => Task.FromResult(1);
        public IEnumerable<Order> SearchGetBy(string? arg = null) => Orders;

        public Task<Order?> GetByIdAsync(int orderId, CancellationToken cancellationToken = default)
            => Task.FromResult(Orders.FirstOrDefault(o => o.OrderID == orderId));

        public Task<Order?> GetCompleteOrderAsync(int orderId, CancellationToken cancellationToken = default)
            => Task.FromResult(Orders.FirstOrDefault(o => o.OrderID == orderId));

        public Task<Order?> GetByQuoteSectionIdAsync(int quoteSectionId, CancellationToken cancellationToken = default)
            => Task.FromResult<Order?>(null);

        public Task<IReadOnlyList<Order>> SearchGetByAsync(string? searchTerm, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Order>>(Orders);

        public Task<int> SaveAsync(Order order, CancellationToken cancellationToken = default) => Task.FromResult(1);
        public Task<int> SaveOrderItemAsync(OrderItem item, CancellationToken cancellationToken = default) => Task.FromResult(1);
        public Task<int> SaveSpecificationValueAsync(OrderItemSpecificationValue value, CancellationToken cancellationToken = default) => Task.FromResult(1);
        public Task<OrderItem?> GetOrderItemByIdAsync(int orderItemId, CancellationToken cancellationToken = default) => Task.FromResult<OrderItem?>(null);
    }

    private sealed class TrackingPaymentMethodRepository : IPaymentMethodRepository
    {
        public List<PaymentMethod> PaymentMethods { get; } = [];

        public int Save(PaymentMethod entity) => 1;
        public Task<int> SaveAsync(PaymentMethod entity, CancellationToken cancellationToken = default) => Task.FromResult(1);
        public int Delete(int id) => 1;
        public Task<int> DeleteAsync(int id, CancellationToken cancellationToken = default) => Task.FromResult(1);

        public IEnumerable<PaymentMethod> SearchGetBy(string? arg = null) => PaymentMethods;

        public Task<IReadOnlyList<PaymentMethod>> SearchGetByAsync(string? arg = null, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default)
        {
            if (!string.IsNullOrEmpty(arg) && int.TryParse(arg, out int id))
            {
                var filtered = PaymentMethods.Where(p => p.PaymentMethodID == id).ToList();
                return Task.FromResult<IReadOnlyList<PaymentMethod>>(filtered);
            }
            return Task.FromResult<IReadOnlyList<PaymentMethod>>(PaymentMethods);
        }

        public bool IsInUse(int paymentMethodId) => false;
        public Task<bool> IsInUseAsync(int paymentMethodId, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public bool NameExists(string name, int currentPaymentMethodId) => false;
        public Task<bool> NameExistsAsync(string name, int currentPaymentMethodId, CancellationToken cancellationToken = default) => Task.FromResult(false);
    }

    private sealed class FixedAuthorizationService(params string[] permissions) : IAuthorizationService
    {
        private readonly HashSet<string> _permissions = new(permissions);
        public bool HasPermission(string permission) => _permissions.Contains(permission);
        public bool CanCreate(string resource) => HasPermission($"{resource}.Create");
        public bool CanEdit(string resource) => HasPermission($"{resource}.Edit");
        public bool CanDelete(string resource) => HasPermission($"{resource}.Delete");
        public bool CanView(string resource) => HasPermission($"{resource}.View");
    }
}
