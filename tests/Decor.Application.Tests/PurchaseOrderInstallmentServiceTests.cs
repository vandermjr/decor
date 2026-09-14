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

namespace Decor.Application.Tests;

public sealed class PurchaseOrderInstallmentServiceTests
{
    [Fact]
    public async Task CreatePlan_UsesPurchaseOrderItemsTotal()
    {
        var context = new TestContext();
        context.Orders.Add(new PurchaseOrder { PurchaseOrderID = 10, Status = PurchaseOrderStatus.Aberto });
        context.Items.Add(new PurchaseOrderItem { PurchaseOrderID = 10, QuantityOrdered = 2m, UnitPrice = 75m });
        context.PaymentMethods.Add(new PaymentMethod { PaymentMethodID = 1, Name = "PIX", Timing = PaymentTiming.Cash, IsActive = true });

        var result = await context.CreateService(DecorPermissions.PurchaseOrderInstallmentsCreatePlan)
            .CreateInstallmentPlanAsync(10, [new InstallmentItemInputDTO(1, 150m, DateTime.UtcNow.AddDays(10))]);

        result.Single().Amount.Should().Be(150m);
        context.Installments.Single().Status.Should().Be(PurchaseOrderInstallmentStatus.Pending);
    }

    [Fact]
    public async Task CreatePlan_WhenSumDiffersFromItemTotal_Throws()
    {
        var context = new TestContext();
        context.Orders.Add(new PurchaseOrder { PurchaseOrderID = 10, Status = PurchaseOrderStatus.Aberto });
        context.Items.Add(new PurchaseOrderItem { PurchaseOrderID = 10, QuantityOrdered = 2m, UnitPrice = 75m });
        context.PaymentMethods.Add(new PaymentMethod { PaymentMethodID = 1, Name = "PIX", Timing = PaymentTiming.Cash, IsActive = true });

        var act = () => context.CreateService(DecorPermissions.PurchaseOrderInstallmentsCreatePlan)
            .CreateInstallmentPlanAsync(10, [new InstallmentItemInputDTO(1, 100m, DateTime.UtcNow.AddDays(10))]);

        await act.Should().ThrowAsync<ValidationException>().WithMessage("*diverge*");
    }

    [Fact]
    public async Task RegisterPayment_ChangesPendingInstallmentToPaid()
    {
        var context = new TestContext();
        context.Installments.Add(new PurchaseOrderInstallment { InstallmentID = 5, PurchaseOrderID = 10, PaymentMethodID = 1, InstallmentNumber = 1, Amount = 100m, DueDate = DateTime.UtcNow.AddDays(10), Status = PurchaseOrderInstallmentStatus.Pending });

        await context.CreateService(DecorPermissions.PurchaseOrderInstallmentsRegisterPayment)
            .RegisterPaymentAsync(5, 42);

        var installment = context.Installments.Single();
        installment.Status.Should().Be(PurchaseOrderInstallmentStatus.Paid);
        installment.PaidByEmployeeID.Should().Be(42);
        installment.PaidAt.Should().NotBeNull();
    }

    private sealed class TestContext
    {
        public List<PurchaseOrder> Orders { get; } = [];
        public List<PurchaseOrderItem> Items { get; } = [];
        public List<PurchaseOrderInstallment> Installments { get; } = [];
        public List<PaymentMethod> PaymentMethods { get; } = [];

        public PurchaseOrderInstallmentService CreateService(params string[] permissions) => new(
            new InstallmentRepository(Installments), new PurchaseOrderRepository(Orders), new ItemRepository(Items), new PaymentMethodRepository(PaymentMethods),
            new PurchaseOrderInstallmentDTOValidator(), new PurchaseOrderInstallmentRepositoryValidator(), new AuthorizationService(permissions));
    }

    private sealed class InstallmentRepository(List<PurchaseOrderInstallment> items) : IPurchaseOrderInstallmentRepository
    {
        public int Save(PurchaseOrderInstallment entity) { if (entity.InstallmentID == 0) entity.InstallmentID = items.Count + 1; items.RemoveAll(x => x.InstallmentID == entity.InstallmentID); items.Add(entity); return 1; }
        public Task<int> SaveAsync(PurchaseOrderInstallment entity, CancellationToken cancellationToken = default) { Save(entity); return Task.FromResult(1); }
        public int Delete(int id) { items.RemoveAll(x => x.InstallmentID == id); return 1; }
        public Task<int> DeleteAsync(int id, CancellationToken cancellationToken = default) { Delete(id); return Task.FromResult(1); }
        public IEnumerable<PurchaseOrderInstallment> SearchGetBy(string? arg = null) => items;
        public Task<IReadOnlyList<PurchaseOrderInstallment>> SearchGetByAsync(string? arg = null, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<PurchaseOrderInstallment>>(items);
        public Task<IReadOnlyList<PurchaseOrderInstallment>> GetByPurchaseOrderIdAsync(int id, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<PurchaseOrderInstallment>>(items.Where(x => x.PurchaseOrderID == id).OrderBy(x => x.InstallmentNumber).ToList());
        public Task<PurchaseOrderInstallment?> GetByIdAsync(int id, CancellationToken cancellationToken = default) => Task.FromResult(items.FirstOrDefault(x => x.InstallmentID == id));
    }

    private sealed class PurchaseOrderRepository(List<PurchaseOrder> items) : IPurchaseOrderRepository
    {
        public int Save(PurchaseOrder entity) => 1;
        public Task<int> SaveAsync(PurchaseOrder entity, CancellationToken cancellationToken = default) => Task.FromResult(1);
        public int Delete(int id) => 1;
        public Task<int> DeleteAsync(int id, CancellationToken cancellationToken = default) => Task.FromResult(1);
        public IEnumerable<PurchaseOrder> SearchGetBy(string? arg = null) => items;
        public Task<IReadOnlyList<PurchaseOrder>> SearchGetByAsync(string? arg = null, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<PurchaseOrder>>(items.Where(x => arg == null || x.PurchaseOrderID.ToString() == arg).ToList());
    }

    private sealed class ItemRepository(List<PurchaseOrderItem> items) : IPurchaseOrderItemRepository
    {
        public int Save(PurchaseOrderItem entity) => 1;
        public Task<int> SaveAsync(PurchaseOrderItem entity, CancellationToken cancellationToken = default) => Task.FromResult(1);
        public int Delete(int id) => 1;
        public Task<int> DeleteAsync(int id, CancellationToken cancellationToken = default) => Task.FromResult(1);
        public IEnumerable<PurchaseOrderItem> SearchGetBy(string? arg = null) => items;
        public Task<IReadOnlyList<PurchaseOrderItem>> SearchGetByAsync(string? arg = null, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<PurchaseOrderItem>>(items);
        public Task<IEnumerable<PurchaseOrderItem>> GetByPurchaseOrderIdAsync(int id, CancellationToken cancellationToken = default) => Task.FromResult<IEnumerable<PurchaseOrderItem>>(items.Where(x => x.PurchaseOrderID == id).ToList());
    }

    private sealed class PaymentMethodRepository(List<PaymentMethod> items) : IPaymentMethodRepository
    {
        public int Save(PaymentMethod entity) => 1;
        public Task<int> SaveAsync(PaymentMethod entity, CancellationToken cancellationToken = default) => Task.FromResult(1);
        public int Delete(int id) => 1;
        public Task<int> DeleteAsync(int id, CancellationToken cancellationToken = default) => Task.FromResult(1);
        public IEnumerable<PaymentMethod> SearchGetBy(string? arg = null) => items;
        public Task<IReadOnlyList<PaymentMethod>> SearchGetByAsync(string? arg = null, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<PaymentMethod>>(items.Where(x => arg == null || x.PaymentMethodID.ToString() == arg).ToList());
        public bool IsInUse(int id) => false;
        public Task<bool> IsInUseAsync(int id, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public bool NameExists(string name, int currentPaymentMethodId) => false;
        public Task<bool> NameExistsAsync(string name, int currentPaymentMethodId, CancellationToken cancellationToken = default) => Task.FromResult(false);
    }

    private sealed class AuthorizationService(params string[] permissions) : IAuthorizationService
    {
        private readonly HashSet<string> _permissions = new(permissions);
        public bool HasPermission(string permission) => _permissions.Contains(permission);
        public bool CanCreate(string resource) => HasPermission($"{resource}.Create");
        public bool CanEdit(string resource) => HasPermission($"{resource}.Edit");
        public bool CanDelete(string resource) => HasPermission($"{resource}.Delete");
        public bool CanView(string resource) => HasPermission($"{resource}.View");
    }
}