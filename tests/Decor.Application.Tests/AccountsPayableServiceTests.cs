using System.ComponentModel.DataAnnotations;
using Decor.Application.Mappers;
using Decor.Application.Services;
using Decor.Core.Common;
using Decor.Core.DTOs;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Repositories;
using Decor.Core.Interfaces.Services;
using Decor.Core.Validation;
using FluentAssertions;

namespace Decor.Application.Tests;

public sealed class AccountsPayableServiceTests
{
    [Fact]
    public async Task Create_AllowsStandalonePayableWithoutSource()
    {
        var context = new TestContext();
        var result = await context.Service(DecorPermissions.AccountsPayableCreate).CreateAsync(context.Dto());

        result.Status.Should().Be(AccountsPayableStatus.Pending);
        result.SourceType.Should().BeNull();
        result.SourceID.Should().BeNull();
    }

    [Fact]
    public async Task Create_ValidatesPayeeTypeAndSelectedPayee()
    {
        var context = new TestContext();
        var dto = context.Dto() with { PayeeType = AccountsPayablePayeeType.Employee, PayeeID = 99 };

        var act = () => context.Service(DecorPermissions.AccountsPayableCreate).CreateAsync(dto);

        await act.Should().ThrowAsync<ValidationException>().WithMessage("*favorecido*");
    }

    [Fact]
    public async Task Create_ValidatesOptionalSourceWhenProvided()
    {
        var context = new TestContext();
        var valid = context.Dto() with { SourceType = AccountsPayableSourceType.TailorQuotationRevision, SourceID = 7 };
        var invalid = context.Dto() with { SourceType = AccountsPayableSourceType.ServiceExecutionRecord, SourceID = 999 };

        (await context.Service(DecorPermissions.AccountsPayableCreate).CreateAsync(valid)).SourceID.Should().Be(7);
        var act = () => context.Service(DecorPermissions.AccountsPayableCreate).CreateAsync(invalid);

        await act.Should().ThrowAsync<ValidationException>().WithMessage("*origem*");
    }

    [Fact]
    public async Task Create_RejectsNonPositiveAmount()
    {
        var context = new TestContext();
        var act = () => context.Service(DecorPermissions.AccountsPayableCreate).CreateAsync(context.Dto() with { Amount = 0m });

        await act.Should().ThrowAsync<ValidationException>().WithMessage("*maior que zero*");
    }

    [Fact]
    public async Task RegisterPayment_ChangesPendingToPaidAndRecordsEffectiveDate()
    {
        var context = new TestContext();
        context.Payables.Add(context.Entity(AccountsPayableStatus.Pending));

        await context.Service(DecorPermissions.AccountsPayableRegisterPayment).RegisterPaymentAsync(1, 42, 3);

        context.Payables.Single().Status.Should().Be(AccountsPayableStatus.Paid);
        context.Payables.Single().PaidByEmployeeID.Should().Be(42);
        context.Payables.Single().PaidAt.Should().NotBeNull();
        context.CashTransactions.Should().ContainSingle(t =>
            t.CashAccountID == 3 && t.Amount == 100m && t.TransactionType == CashTransactionType.Expense &&
            t.SourceType == "AccountsPayable" && t.SourceID == 1 && t.CreatedByEmployeeID == 42);
    }

    [Fact]
    public async Task Cancel_OnlyAllowsPending()
    {
        var context = new TestContext();
        context.Payables.Add(context.Entity(AccountsPayableStatus.Paid));

        var act = () => context.Service(DecorPermissions.AccountsPayableCancel).CancelAsync(1);

        await act.Should().ThrowAsync<ValidationException>().WithMessage("*pendentes*");
    }

    [Fact]
    public async Task Cancel_ChangesPendingToCancelled()
    {
        var context = new TestContext();
        context.Payables.Add(context.Entity(AccountsPayableStatus.Pending));

        await context.Service(DecorPermissions.AccountsPayableCancel).CancelAsync(1);

        context.Payables.Single().Status.Should().Be(AccountsPayableStatus.Cancelled);
    }

    private sealed class TestContext
    {
        public List<AccountsPayable> Payables { get; } = [];
        public List<CashTransaction> CashTransactions { get; } = [];
        public List<Employee> Employees { get; } = [new() { EmployeeID = 42, Name = "Employee", IsActive = true }];
        public List<Partner> Partners { get; } = [new() { PartnerID = 10, Name = "Partner", IsActive = true }];
        public List<TailorQuotationRevision> Revisions { get; } = [new() { RevisionID = 7, RequestID = 1 }];

        public AccountsPayableDTO Dto() => new(0, AccountsPayablePayeeType.Partner, 10, "Costura avulsa", 100m, DateTime.UtcNow.AddDays(10), AccountsPayableStatus.Pending, null, null, 42, default, null, null, null);
        public AccountsPayable Entity(AccountsPayableStatus status) => new() { AccountsPayableID = 1, PayeeType = AccountsPayablePayeeType.Partner, PayeeID = 10, Description = "Costura", Amount = 100m, DueDate = DateTime.UtcNow.AddDays(10), Status = status, CreatedByEmployeeID = 42, CreatedAt = DateTime.UtcNow };
        public AccountsPayableService Service(params string[] permissions) => new(
            new PayableRepository(Payables), new EmployeeRepository(Employees), new PartnerRepository(Partners),
            new TailorRepository(Revisions), new ExecutionRepository(), new AccountsPayableDTOValidator(),
            new PayableValidator(), new TrackingCashTransactionService(CashTransactions), new Authorization(permissions));
    }

    private sealed class PayableRepository(List<AccountsPayable> items) : IAccountsPayableRepository
    {
        public int Save(AccountsPayable entity) { if (entity.AccountsPayableID == 0) entity.AccountsPayableID = 1; if (!items.Contains(entity)) items.Add(entity); return 1; }
        public Task<int> SaveAsync(AccountsPayable entity, CancellationToken cancellationToken = default) { Save(entity); return Task.FromResult(1); }
        public int Delete(int id) => throw new NotSupportedException();
        public Task<int> DeleteAsync(int id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IEnumerable<AccountsPayable> SearchGetBy(string? arg = null) => items;
        public Task<IReadOnlyList<AccountsPayable>> SearchGetByAsync(string? arg = null, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AccountsPayable>>(items);
        public Task<AccountsPayable?> GetByIdAsync(int id, CancellationToken cancellationToken = default) => Task.FromResult(items.FirstOrDefault(x => x.AccountsPayableID == id));
    }

    private sealed class EmployeeRepository(List<Employee> items) : IEmployeeRepository
    {
        public int Save(Employee entity) => 1; public Task<int> SaveAsync(Employee entity, CancellationToken cancellationToken = default) => Task.FromResult(1);
        public int Delete(int id) => 1; public Task<int> DeleteAsync(int id, CancellationToken cancellationToken = default) => Task.FromResult(1);
        public IEnumerable<Employee> SearchGetBy(string? arg = null) => items;
        public Task<IReadOnlyList<Employee>> SearchGetByAsync(string? arg = null, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Employee>>(items.Where(x => arg == null || x.EmployeeID.ToString() == arg).ToList());
    }

    private sealed class PartnerRepository(List<Partner> items) : IPartnerRepository
    {
        public int Save(Partner entity) => 1; public Task<int> SaveAsync(Partner entity, CancellationToken cancellationToken = default) => Task.FromResult(1);
        public int Delete(int id) => 1; public Task<int> DeleteAsync(int id, CancellationToken cancellationToken = default) => Task.FromResult(1);
        public IEnumerable<Partner> SearchGetBy(string? arg = null) => items;
        public Task<IReadOnlyList<Partner>> SearchGetByAsync(string? arg = null, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Partner>>(items.Where(x => arg == null || x.PartnerID.ToString() == arg).ToList());
    }

    private sealed class TailorRepository(List<TailorQuotationRevision> revisions) : ITailorQuotationRepository
    {
        public Task<TailorQuotationRevision?> GetRevisionByIdAsync(int id, CancellationToken cancellationToken = default) => Task.FromResult(revisions.FirstOrDefault(x => x.RevisionID == id));
        public Task<int> SaveRevisionAsync(TailorQuotationRevision revision, CancellationToken cancellationToken = default) => Task.FromResult(1);
        public Task<IReadOnlyList<TailorQuotationRevision>> GetRevisionsByRequestIdAsync(int id, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<TailorQuotationRevision>>(revisions);
        public Task<TailorQuotationRequest?> GetByIdAsync(int id, CancellationToken cancellationToken = default) => Task.FromResult<TailorQuotationRequest?>(null);
        public Task<IReadOnlyList<TailorQuotationRequest>> GetByQuoteItemIdAsync(int id, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<TailorQuotationRequest>>([]);
        public Task<IReadOnlyList<TailorQuotationRequest>> GetByQuoteSectionIdAsync(int id, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<TailorQuotationRequest>>([]);
        public int Save(TailorQuotationRequest entity) => 1; public Task<int> SaveAsync(TailorQuotationRequest entity, CancellationToken cancellationToken = default) => Task.FromResult(1);
        public int Delete(int id) => 1; public Task<int> DeleteAsync(int id, CancellationToken cancellationToken = default) => Task.FromResult(1);
        public IEnumerable<TailorQuotationRequest> SearchGetBy(string? arg = null) => [];
        public Task<IReadOnlyList<TailorQuotationRequest>> SearchGetByAsync(string? arg = null, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<TailorQuotationRequest>>([]);
    }

    private sealed class ExecutionRepository : IServiceExecutionRecordRepository
    {
        public Task<ServiceExecutionRecord?> GetByIdAsync(int id, CancellationToken cancellationToken = default) => Task.FromResult<ServiceExecutionRecord?>(null);
        public Task<ServiceExecutionRecord?> GetByAppointmentIdAsync(int id, CancellationToken cancellationToken = default) => Task.FromResult<ServiceExecutionRecord?>(null);
        public int Save(ServiceExecutionRecord entity) => 1; public Task<int> SaveAsync(ServiceExecutionRecord entity, CancellationToken cancellationToken = default) => Task.FromResult(1);
        public int Delete(int id) => 1; public Task<int> DeleteAsync(int id, CancellationToken cancellationToken = default) => Task.FromResult(1);
        public IEnumerable<ServiceExecutionRecord> SearchGetBy(string? arg = null) => [];
        public Task<IReadOnlyList<ServiceExecutionRecord>> SearchGetByAsync(string? arg = null, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ServiceExecutionRecord>>([]);
    }

    private sealed class PayableValidator : IRepositoryValidator<AccountsPayable> { public IEnumerable<string> Validate(AccountsPayable entity) => []; }
    private sealed class TrackingCashTransactionService(List<CashTransaction> transactions) : ICashTransactionService
    {
        public Task<CashTransactionDTO> CreateAsync(int cashAccountId, decimal amount, CashTransactionType transactionType, string? sourceType, int? sourceId, int createdByEmployeeId, DateTime? transactionDate = null, CancellationToken cancellationToken = default)
        {
            var transaction = new CashTransaction
            {
                CashTransactionID = transactions.Count + 1,
                CashAccountID = cashAccountId,
                Amount = amount,
                TransactionType = transactionType,
                SourceType = sourceType,
                SourceID = sourceId,
                TransactionDate = transactionDate ?? DateTime.UtcNow,
                CreatedByEmployeeID = createdByEmployeeId,
                CreatedAt = DateTime.UtcNow
            };
            transactions.Add(transaction);
            return Task.FromResult(transaction.ToDTO());
        }

        public Task<Guid> CreateTransferAsync(int fromAccountId, int toAccountId, decimal amount, int createdByEmployeeId, string? sourceType = null, int? sourceId = null, DateTime? transactionDate = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<CashTransactionDTO> GetByIdAsync(int cashTransactionId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<CashTransactionDTO>> GetByCashAccountAsync(int cashAccountId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<decimal> GetBalanceAsync(int cashAccountId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
    private sealed class Authorization(params string[] permissions) : IAuthorizationService
    {
        private readonly HashSet<string> permissions = new(permissions);
        public bool HasPermission(string permissionCode) => permissions.Contains(permissionCode);
        public bool CanView(string resource) => true; public bool CanCreate(string resource) => true; public bool CanEdit(string resource) => false; public bool CanDelete(string resource) => false;
    }
}
