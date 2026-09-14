using System.ComponentModel.DataAnnotations;
using Decor.Application.Services;
using Decor.Core.Common;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Repositories;
using Decor.Core.Interfaces.Services;
using FluentAssertions;
using Xunit;

namespace Decor.Application.Tests;

public sealed class CashServicesTests
{
    [Fact]
    public async Task CashAccount_RequiresNameAndUniqueName()
    {
        var repository = new FakeCashAccountRepository();
        var service = new CashAccountService(repository, new AllowAllAuthorizationService());

        await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(" ", CashAccountType.Cash));
        await service.CreateAsync("Caixa", CashAccountType.Cash);

        await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync("caixa", CashAccountType.Bank));
    }

    [Fact]
    public async Task CashTransaction_IncomeAndExpenseRequirePositiveAmountAndNoTransferId()
    {
        var accounts = new FakeCashAccountRepository();
        await accounts.SaveAsync(new CashAccount { CashAccountID = 1, Name = "Caixa", AccountType = CashAccountType.Cash });
        var transactions = new FakeCashTransactionRepository();
        var service = new CashTransactionService(transactions, accounts, new AllowAllAuthorizationService());

        await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(1, 0, CashTransactionType.Income, null, null, 7));
        await service.CreateAsync(1, 100, CashTransactionType.Income, "Manual", null, 7);
        transactions.Transactions.Single().TransferID.Should().BeNull();
        await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(1, -1, CashTransactionType.Expense, null, null, 7));
    }

    [Fact]
    public async Task Transfer_CreatesAtomicPairWithSameIdAndAmount()
    {
        var accounts = new FakeCashAccountRepository();
        await accounts.SaveAsync(new CashAccount { CashAccountID = 1, Name = "Origem", AccountType = CashAccountType.Cash });
        await accounts.SaveAsync(new CashAccount { CashAccountID = 2, Name = "Destino", AccountType = CashAccountType.Bank });
        var transactions = new FakeCashTransactionRepository();
        var service = new CashTransactionService(transactions, accounts, new AllowAllAuthorizationService());

        var transferId = await service.CreateTransferAsync(1, 2, 250, 7);

        transactions.Transactions.Should().HaveCount(2);
        transactions.Transactions.Should().OnlyContain(t => t.TransferID == transferId && t.Amount == 250);
        transactions.Transactions.Should().ContainSingle(t => t.CashAccountID == 1 && t.TransactionType == CashTransactionType.Expense);
        transactions.Transactions.Should().ContainSingle(t => t.CashAccountID == 2 && t.TransactionType == CashTransactionType.Income);
    }

    [Fact]
    public async Task Balance_CalculatesMixedIncomeExpenseAndTransfer()
    {
        var accounts = new FakeCashAccountRepository();
        await accounts.SaveAsync(new CashAccount { CashAccountID = 1, Name = "Caixa", AccountType = CashAccountType.Cash });
        await accounts.SaveAsync(new CashAccount { CashAccountID = 2, Name = "Banco", AccountType = CashAccountType.Bank });
        var transactions = new FakeCashTransactionRepository();
        var service = new CashTransactionService(transactions, accounts, new AllowAllAuthorizationService());

        await service.CreateAsync(1, 1000, CashTransactionType.Income, null, null, 7);
        await service.CreateAsync(1, 100, CashTransactionType.Expense, null, null, 7);
        await service.CreateTransferAsync(1, 2, 300, 7);

        (await service.GetBalanceAsync(1)).Should().Be(600);
        (await service.GetBalanceAsync(2)).Should().Be(300);
    }

    private sealed class FakeCashAccountRepository : ICashAccountRepository
    {
        private int _nextId = 1;
        public List<CashAccount> Accounts { get; } = [];

        public Task<int> SaveAsync(CashAccount account, CancellationToken cancellationToken = default)
        {
            if (account.CashAccountID == 0) account.CashAccountID = _nextId++;
            Accounts.RemoveAll(a => a.CashAccountID == account.CashAccountID);
            Accounts.Add(account);
            return Task.FromResult(1);
        }

        public Task<CashAccount?> GetByIdAsync(int cashAccountId, CancellationToken cancellationToken = default) => Task.FromResult(Accounts.SingleOrDefault(a => a.CashAccountID == cashAccountId));
        public Task<IReadOnlyList<CashAccount>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<CashAccount>>(Accounts);
        public Task<bool> NameExistsAsync(string name, int currentCashAccountId, CancellationToken cancellationToken = default) => Task.FromResult(Accounts.Any(a => a.CashAccountID != currentCashAccountId && string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase)));
        public Task<int> DeactivateAsync(int cashAccountId, CancellationToken cancellationToken = default)
        {
            var account = Accounts.SingleOrDefault(a => a.CashAccountID == cashAccountId);
            if (account is null) return Task.FromResult(0);
            account.IsActive = false;
            return Task.FromResult(1);
        }
    }

    private sealed class FakeCashTransactionRepository : ICashTransactionRepository
    {
        private int _nextId = 1;
        public List<CashTransaction> Transactions { get; } = [];

        public Task<int> RegisterAsync(CashTransaction transaction, CancellationToken cancellationToken = default)
        {
            transaction.CashTransactionID = _nextId++;
            Transactions.Add(transaction);
            return Task.FromResult(transaction.CashTransactionID);
        }

        public Task<Guid> RegisterTransferAsync(CashTransaction expense, CashTransaction income, CancellationToken cancellationToken = default)
        {
            var id = Guid.NewGuid();
            expense.TransferID = income.TransferID = id;
            RegisterAsync(expense, cancellationToken);
            RegisterAsync(income, cancellationToken);
            return Task.FromResult(id);
        }

        public Task<CashTransaction?> GetByIdAsync(int cashTransactionId, CancellationToken cancellationToken = default) => Task.FromResult(Transactions.SingleOrDefault(t => t.CashTransactionID == cashTransactionId));
        public Task<IReadOnlyList<CashTransaction>> GetByCashAccountAsync(int cashAccountId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<CashTransaction>>(Transactions.Where(t => t.CashAccountID == cashAccountId).ToList());
        public async Task<decimal> GetBalanceAsync(int cashAccountId, CancellationToken cancellationToken = default)
        {
            var transactions = await GetByCashAccountAsync(cashAccountId, cancellationToken);
            return transactions.Sum(t => t.TransactionType == CashTransactionType.Income ? t.Amount : -t.Amount);
        }
    }

    private sealed class AllowAllAuthorizationService : IAuthorizationService
    {
        public bool HasPermission(string permission) => permission is DecorPermissions.CashAccountsCreate or DecorPermissions.CashAccountsUpdate or DecorPermissions.CashAccountsView or DecorPermissions.CashAccountsDeactivate or DecorPermissions.CashTransactionsCreate or DecorPermissions.CashTransactionsView;
        public bool CanCreate(string resource) => true;
        public bool CanEdit(string resource) => true;
        public bool CanDelete(string resource) => true;
        public bool CanView(string resource) => true;
    }
}