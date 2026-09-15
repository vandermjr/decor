using Dapper;
using Decor.Application.Services;
using Decor.Application.Validation;
using Decor.Core.Common;
using Decor.Core.DTOs;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Services;
using Decor.Core.Validation;
using Decor.FluentSqlBuilder;
using Decor.FluentSqlBuilder.Dialects.MariaDB;
using Decor.Infrastructure.Data;
using Decor.Infrastructure.Data.Repositories;
using FluentAssertions;
using MySqlConnector;

namespace Decor.Infrastructure.IntegrationTests;

public sealed class AccountsPayableCashIntegrationTests(MariaDbFixture fixture) : IClassFixture<MariaDbFixture>
{
    [Fact]
    public async Task RegisterPayment_CreatesExpenseAndChangesMariaDbBalance()
    {
        await using var connection = new MySqlConnection(fixture.ConnectionString);
        await EnsureSchemaAsync(connection);

        var dbConnection = new DatabaseConnection(fixture.ConnectionString);
        var dialect = new MariaDBDialect();
        var createBuilder = () => FluentCommandBuilder.Create(dialect);
        var payableRepository = new AccountsPayableRepository(dbConnection, createBuilder);
        var cashAccountRepository = new CashAccountRepository(dbConnection, createBuilder);
        var cashTransactionRepository = new CashTransactionRepository(dbConnection, createBuilder);
        var cashTransactionService = new CashTransactionService(cashTransactionRepository, cashAccountRepository, new AllowAllAuthorizationService());
        var payableService = new AccountsPayableService(
            payableRepository,
            new EmployeeRepository(dbConnection, createBuilder),
            new PartnerRepository(dbConnection, createBuilder),
            new TailorQuotationRepository(dbConnection, createBuilder),
            new ServiceExecutionRecordRepository(dbConnection, createBuilder),
            new AccountsPayableDTOValidator(),
            new AccountsPayableRepositoryValidator(),
            cashTransactionService,
            new AllowAllAuthorizationService());

        var employeeId = await connection.QuerySingleAsync<int>(
            "INSERT INTO employees (Name, IsActive) VALUES ('Funcionario Integracao Pagar', 1); SELECT LAST_INSERT_ID();");
        var cashAccount = new CashAccount { Name = "Caixa Integracao Conta Pagar", AccountType = CashAccountType.Cash };
        await cashAccountRepository.SaveAsync(cashAccount);
        var payable = new AccountsPayable
        {
            PayeeType = AccountsPayablePayeeType.Employee,
            PayeeID = employeeId,
            Description = "Despesa de integracao",
            Amount = 150m,
            DueDate = DateTime.UtcNow.Date.AddDays(10),
            Status = AccountsPayableStatus.Pending,
            CreatedByEmployeeID = employeeId,
            CreatedAt = DateTime.UtcNow
        };
        await payableRepository.SaveAsync(payable);

        var balanceBefore = await cashTransactionRepository.GetBalanceAsync(cashAccount.CashAccountID);
        balanceBefore.Should().Be(0m);

        await payableService.RegisterPaymentAsync(payable.AccountsPayableID, employeeId, cashAccount.CashAccountID);

        var savedPayable = await payableRepository.GetByIdAsync(payable.AccountsPayableID);
        savedPayable!.Status.Should().Be(AccountsPayableStatus.Paid);
        savedPayable.PaidFromCashAccountID.Should().Be(cashAccount.CashAccountID);
        var transactions = await cashTransactionRepository.GetByCashAccountAsync(cashAccount.CashAccountID);
        transactions.Should().ContainSingle(t =>
            t.Amount == 150m && t.TransactionType == CashTransactionType.Expense &&
            t.SourceType == "AccountsPayable" && t.SourceID == payable.AccountsPayableID);
        var balanceAfter = await cashTransactionRepository.GetBalanceAsync(cashAccount.CashAccountID);
        balanceAfter.Should().Be(-150m);
    }

    private static async Task EnsureSchemaAsync(MySqlConnection connection)
    {
        await connection.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS employees (
                EmployeeID INT NOT NULL AUTO_INCREMENT,
                Name VARCHAR(150) NOT NULL,
                IsActive TINYINT(1) NOT NULL DEFAULT 1,
                PRIMARY KEY (EmployeeID)
            ) ENGINE=InnoDB;");

        var migrationsDirectory = Path.Combine(AppContext.BaseDirectory, "Migrations");
        foreach (var migration in new[]
        {
            "20260914_add_cash_accounts_and_transactions.sql",
            "20260914_add_accounts_payable.sql",
            "20260914_add_accounts_payable_cash_account.sql"
        })
        {
            await connection.ExecuteAsync(await File.ReadAllTextAsync(Path.Combine(migrationsDirectory, migration)));
        }
    }

    private sealed class AllowAllAuthorizationService : IAuthorizationService
    {
        public bool HasPermission(string permissionCode) => true;
        public bool CanView(string resource) => true;
        public bool CanCreate(string resource) => true;
        public bool CanEdit(string resource) => true;
        public bool CanDelete(string resource) => true;
    }
}
