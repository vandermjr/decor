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

public sealed class OrderOccurrenceServiceTests
{
    [Fact]
    public async Task RegisterOccurrence_WithoutPermission_Throws()
    {
        var service = CreateOccurrenceService();

        var act = () => service.RegisterOccurrenceAsync(1, 1, 1, "Contato com cliente");

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task RegisterOccurrence_ForCancelledOrder_Throws()
    {
        var orderRepository = new OrderRepositoryFake(new Order { OrderID = 1, Status = OrderStatus.Cancelled });
        var service = CreateOccurrenceService(orderRepository: orderRepository, permissions: DecorPermissions.OrderOccurrencesRegister);

        var act = () => service.RegisterOccurrenceAsync(1, 1, 1, "Contato com cliente");

        await act.Should().ThrowAsync<ValidationException>().WithMessage("*cancelado*");
    }

    [Fact]
    public async Task RegisterOccurrence_WithInactiveReason_Throws()
    {
        var reasonRepository = new OccurrenceReasonRepositoryFake(new OccurrenceReason { ReasonID = 1, Description = "Troca", IsActive = false });
        var service = CreateOccurrenceService(reasonRepository: reasonRepository, permissions: DecorPermissions.OrderOccurrencesRegister);

        var act = () => service.RegisterOccurrenceAsync(1, 1, 1, "Contato com cliente");

        await act.Should().ThrowAsync<ValidationException>().WithMessage("*inativo*");
    }

    [Fact]
    public async Task RegisterOccurrence_WithBlankObservation_Throws()
    {
        var service = CreateOccurrenceService(permissions: DecorPermissions.OrderOccurrencesRegister);

        var act = () => service.RegisterOccurrenceAsync(1, 1, 1, "   ");

        await act.Should().ThrowAsync<ValidationException>().WithMessage("*observação*");
    }

    [Fact]
    public async Task RegisterOccurrence_WithUnknownEmployee_ThrowsClearError()
    {
        var service = CreateOccurrenceService(
            employeeRepository: new EmployeeRepositoryFake(),
            permissions: DecorPermissions.OrderOccurrencesRegister);

        var act = () => service.RegisterOccurrenceAsync(1, 1, 999, "Contato com cliente");

        await act.Should().ThrowAsync<KeyNotFoundException>().WithMessage("*Funcionário*999*");
    }

    [Fact]
    public async Task RegisterOccurrence_WithDeadlines_RegistersAndUpdatesOrder()
    {
        var occurrenceRepository = new OrderOccurrenceRepositoryFake();
        var manufacturing = new DateTime(2026, 10, 5);
        var installation = new DateTime(2026, 10, 12);
        var service = CreateOccurrenceService(occurrenceRepository, permissions: DecorPermissions.OrderOccurrencesRegister);

        var result = await service.RegisterOccurrenceAsync(1, 1, 1, "Prazo renegociado", manufacturing, installation);

        result.OccurrenceID.Should().Be(1);
        result.RegisteredAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        occurrenceRepository.RegisteredOrder!.ManufacturingDeadline.Should().Be(manufacturing);
        occurrenceRepository.RegisteredOrder.InstallationDeadline.Should().Be(installation);
    }

    [Fact]
    public async Task GetHistoryForOrder_ReturnsRepositoryOrder()
    {
        var repository = new OrderOccurrenceRepositoryFake();
        repository.History.Add(new OrderOccurrence { OccurrenceID = 2, OrderID = 1, ReasonID = 1, RegisteredByEmployeeID = 1, RegisteredAt = DateTime.UtcNow, Observation = "Mais recente" });
        repository.History.Add(new OrderOccurrence { OccurrenceID = 1, OrderID = 1, ReasonID = 1, RegisteredByEmployeeID = 1, RegisteredAt = DateTime.UtcNow.AddHours(-1), Observation = "Anterior" });
        var service = CreateOccurrenceService(repository, permissions: DecorPermissions.OrderOccurrencesView);

        var history = await service.GetHistoryForOrderAsync(1);

        history.Select(o => o.OccurrenceID).Should().ContainInOrder(2, 1);
    }

    [Fact]
    public async Task SaveOccurrenceReason_WithDuplicateDescription_Throws()
    {
        var repository = new OccurrenceReasonRepositoryFake(new OccurrenceReason { ReasonID = 1, Description = "Troca", IsActive = true });
        var service = CreateReasonService(repository, DecorPermissions.OccurrenceReasonsCreate);

        var act = () => service.SaveOccurrenceReasonAsync(new OccurrenceReasonDTO(0, "Troca", true));

        await act.Should().ThrowAsync<ValidationException>().WithMessage("*Troca*");
    }

    [Fact]
    public async Task DeleteOccurrenceReason_WhenReferenced_ThrowsClearError()
    {
        var repository = new OccurrenceReasonRepositoryFake(new OccurrenceReason { ReasonID = 1, Description = "Troca", IsActive = true });
        repository.InUseIds.Add(1);
        var service = CreateReasonService(repository, DecorPermissions.OccurrenceReasonsDelete);

        var act = () => service.DeleteOccurrenceReasonAsync(1);

        await act.Should().ThrowAsync<ValidationException>().WithMessage("*referenciado*");
    }

    [Fact]
    public async Task SaveOccurrenceReason_WithValidData_Saves()
    {
        var repository = new OccurrenceReasonRepositoryFake();
        var service = CreateReasonService(repository, DecorPermissions.OccurrenceReasonsCreate);

        await service.SaveOccurrenceReasonAsync(new OccurrenceReasonDTO(0, "Avaria", true));

        repository.Reasons.Should().ContainSingle(r => r.Description == "Avaria" && r.IsActive);
    }

    private static OrderOccurrenceService CreateOccurrenceService(
        OrderOccurrenceRepositoryFake? occurrenceRepository = null,
        OccurrenceReasonRepositoryFake? reasonRepository = null,
        OrderRepositoryFake? orderRepository = null,
        EmployeeRepositoryFake? employeeRepository = null,
        params string[] permissions)
        => new(
            occurrenceRepository ?? new OrderOccurrenceRepositoryFake(),
            reasonRepository ?? new OccurrenceReasonRepositoryFake(new OccurrenceReason { ReasonID = 1, Description = "Troca", IsActive = true }),
            orderRepository ?? new OrderRepositoryFake(new Order { OrderID = 1, Status = OrderStatus.Approved }),
            employeeRepository ?? new EmployeeRepositoryFake(new Employee { EmployeeID = 1, Name = "Funcionário", IsActive = true }),
            new OrderOccurrenceDTOValidator(),
            new OrderOccurrenceRepositoryValidator(),
            new AuthorizationServiceFake(permissions));

    private static OccurrenceReasonService CreateReasonService(OccurrenceReasonRepositoryFake repository, params string[] permissions)
        => new(repository, new OccurrenceReasonDTOValidator(), new OccurrenceReasonRepositoryValidator(repository), new AuthorizationServiceFake(permissions));

    private sealed class OccurrenceReasonRepositoryFake(params OccurrenceReason[] reasons) : IOccurrenceReasonRepository
    {
        public List<OccurrenceReason> Reasons { get; } = [.. reasons];
        public HashSet<int> InUseIds { get; } = [];

        public int Save(OccurrenceReason entity)
        {
            if (entity.ReasonID == 0) entity.ReasonID = Reasons.Count + 1;
            Reasons.RemoveAll(r => r.ReasonID == entity.ReasonID);
            Reasons.Add(entity);
            return 1;
        }

        public Task<int> SaveAsync(OccurrenceReason entity, CancellationToken cancellationToken = default) => Task.FromResult(Save(entity));
        public int Delete(int id) => Reasons.RemoveAll(r => r.ReasonID == id) == 1 ? 1 : 0;
        public Task<int> DeleteAsync(int id, CancellationToken cancellationToken = default) => Task.FromResult(Delete(id));
        public IEnumerable<OccurrenceReason> SearchGetBy(string? arg = null) => Reasons;
        public Task<IReadOnlyList<OccurrenceReason>> SearchGetByAsync(string? arg = null, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<OccurrenceReason>>(Reasons);
        public Task<OccurrenceReason?> GetByIdAsync(int reasonId, CancellationToken cancellationToken = default) => Task.FromResult(Reasons.FirstOrDefault(r => r.ReasonID == reasonId));
        public bool DescriptionExists(string description, int currentReasonId) => Reasons.Any(r => r.ReasonID != currentReasonId && r.Description.Equals(description, StringComparison.OrdinalIgnoreCase));
        public Task<bool> IsInUseAsync(int reasonId, CancellationToken cancellationToken = default) => Task.FromResult(InUseIds.Contains(reasonId));
    }

    private sealed class OrderOccurrenceRepositoryFake : IOrderOccurrenceRepository
    {
        public List<OrderOccurrence> History { get; } = [];
        public Order? RegisteredOrder { get; private set; }

        public Task<int> RegisterAsync(OrderOccurrence occurrence, Order order, CancellationToken cancellationToken = default)
        {
            occurrence.OccurrenceID = History.Count + 1;
            History.Add(occurrence);
            RegisteredOrder = order;
            return Task.FromResult(1);
        }

        public Task<IReadOnlyList<OrderOccurrence>> GetHistoryForOrderAsync(int orderId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<OrderOccurrence>>(History.Where(o => o.OrderID == orderId).ToList());
        public int Save(OrderOccurrence entity) => 1;
        public Task<int> SaveAsync(OrderOccurrence entity, CancellationToken cancellationToken = default) => Task.FromResult(1);
        public int Delete(int id) => 1;
        public Task<int> DeleteAsync(int id, CancellationToken cancellationToken = default) => Task.FromResult(1);
        public IEnumerable<OrderOccurrence> SearchGetBy(string? arg = null) => History;
        public Task<IReadOnlyList<OrderOccurrence>> SearchGetByAsync(string? arg = null, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<OrderOccurrence>>(History);
    }

    private sealed class OrderRepositoryFake(params Order[] orders) : IOrderRepository
    {
        public List<Order> Orders { get; } = [.. orders];
        public int Save(Order entity) => 1;
        public Task<int> SaveAsync(Order entity, CancellationToken cancellationToken = default) => Task.FromResult(1);
        public int Delete(int id) => 1;
        public Task<int> DeleteAsync(int id, CancellationToken cancellationToken = default) => Task.FromResult(1);
        public IEnumerable<Order> SearchGetBy(string? arg = null) => Orders;
        public Task<IReadOnlyList<Order>> SearchGetByAsync(string? arg = null, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Order>>(Orders);
        public Task<Order?> GetByIdAsync(int orderId, CancellationToken cancellationToken = default) => Task.FromResult(Orders.FirstOrDefault(o => o.OrderID == orderId));
        public Task<Order?> GetCompleteOrderAsync(int orderId, CancellationToken cancellationToken = default) => GetByIdAsync(orderId, cancellationToken);
        public Task<Order?> GetByQuoteSectionIdAsync(int quoteSectionId, CancellationToken cancellationToken = default) => Task.FromResult<Order?>(null);
        public Task<OrderItem?> GetOrderItemByIdAsync(int orderItemId, CancellationToken cancellationToken = default) => Task.FromResult<OrderItem?>(null);
        public Task<int> SaveOrderItemAsync(OrderItem item, CancellationToken cancellationToken = default) => Task.FromResult(1);
        public Task<int> SaveSpecificationValueAsync(OrderItemSpecificationValue value, CancellationToken cancellationToken = default) => Task.FromResult(1);
    }

    private sealed class EmployeeRepositoryFake(params Employee[] employees) : IEmployeeRepository
    {
        public List<Employee> Employees { get; } = [.. employees];
        public int Save(Employee entity) => 1;
        public Task<int> SaveAsync(Employee entity, CancellationToken cancellationToken = default) => Task.FromResult(1);
        public int Delete(int id) => 1;
        public Task<int> DeleteAsync(int id, CancellationToken cancellationToken = default) => Task.FromResult(1);
        public IEnumerable<Employee> SearchGetBy(string? arg = null) => Employees;
        public Task<IReadOnlyList<Employee>> SearchGetByAsync(string? arg = null, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default)
        {
            var result = int.TryParse(arg, out var id) ? Employees.Where(e => e.EmployeeID == id).ToList() : Employees;
            return Task.FromResult<IReadOnlyList<Employee>>(result);
        }
    }

    private sealed class AuthorizationServiceFake(params string[] permissions) : IAuthorizationService
    {
        private readonly HashSet<string> _permissions = [.. permissions];
        public bool HasPermission(string permission) => _permissions.Contains(permission);
        public bool CanCreate(string resource) => HasPermission($"{resource}.Create");
        public bool CanEdit(string resource) => HasPermission($"{resource}.Edit");
        public bool CanDelete(string resource) => HasPermission($"{resource}.Delete");
        public bool CanView(string resource) => HasPermission($"{resource}.View");
    }
}