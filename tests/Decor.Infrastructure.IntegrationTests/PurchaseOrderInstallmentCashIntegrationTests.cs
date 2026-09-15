using Dapper;
using Decor.Core.Entities;
using Decor.FluentSqlBuilder;
using Decor.FluentSqlBuilder.Dialects.MariaDB;
using Decor.Infrastructure.Data;
using Decor.Infrastructure.Data.Repositories;
using FluentAssertions;
using MySqlConnector;
using Xunit;

namespace Decor.Infrastructure.IntegrationTests;

public sealed class PurchaseOrderInstallmentCashIntegrationTests(MariaDbFixture fixture) : IClassFixture<MariaDbFixture>
{
    [Fact]
    public async Task PurchaseOrderInstallmentPayment_ChangesCashBalanceToExpense()
    {
        await using var connection = new MySqlConnection(fixture.ConnectionString);
        await EnsureSchemaAsync(connection);

        var dbConnection = new DatabaseConnection(fixture.ConnectionString);
        var dialect = new MariaDBDialect();
        var installmentRepository = new PurchaseOrderInstallmentRepository(dbConnection, () => FluentCommandBuilder.Create(dialect));
        var cashAccountRepository = new CashAccountRepository(dbConnection, () => FluentCommandBuilder.Create(dialect));
        var cashTransactionRepository = new CashTransactionRepository(dbConnection, () => FluentCommandBuilder.Create(dialect));

        var employeeId = await connection.QuerySingleAsync<int>(
            "INSERT INTO employees (Name, IsActive) VALUES ('Funcionario Integracao Compra', 1); SELECT LAST_INSERT_ID();");
        var supplierId = await connection.QuerySingleAsync<int>(
            "INSERT INTO suppliers (Name, IsActive) VALUES ('Fornecedor Integracao Compra', 1); SELECT LAST_INSERT_ID();");
        var purchaseOrderId = await connection.QuerySingleAsync<int>(
            $"INSERT INTO purchase_orders (SupplierID, OrderDate, Status) VALUES ({supplierId}, NOW(), 1); SELECT LAST_INSERT_ID();");
        var paymentMethodId = await connection.QuerySingleAsync<int>(
            "SELECT PaymentMethodID FROM payment_methods WHERE Name = 'PIX' LIMIT 1;");

        var cashAccount = new CashAccount { Name = "Caixa Integracao Pagamento Compra", AccountType = CashAccountType.Cash };
        await cashAccountRepository.SaveAsync(cashAccount);
        var balanceBefore = await cashTransactionRepository.GetBalanceAsync(cashAccount.CashAccountID);
        balanceBefore.Should().Be(0m);

        var installment = new PurchaseOrderInstallment
        {
            PurchaseOrderID = purchaseOrderId,
            PaymentMethodID = paymentMethodId,
            InstallmentNumber = 1,
            Amount = 150m,
            DueDate = DateTime.UtcNow.AddDays(15).Date,
            Status = PurchaseOrderInstallmentStatus.Pending
        };
        await installmentRepository.SaveAsync(installment);

        installment.Status = PurchaseOrderInstallmentStatus.Paid;
        installment.PaidAt = DateTime.UtcNow;
        installment.PaidByEmployeeID = employeeId;
        installment.PaidFromCashAccountID = cashAccount.CashAccountID;
        await installmentRepository.SaveAsync(installment);

        var transaction = new CashTransaction
        {
            CashAccountID = cashAccount.CashAccountID,
            Amount = installment.Amount,
            TransactionType = CashTransactionType.Expense,
            SourceType = "PurchaseOrderInstallment",
            SourceID = installment.InstallmentID,
            TransactionDate = DateTime.UtcNow,
            CreatedByEmployeeID = employeeId,
            CreatedAt = DateTime.UtcNow
        };
        await cashTransactionRepository.RegisterAsync(transaction);

        var fetched = await installmentRepository.GetByIdAsync(installment.InstallmentID);
        fetched.Should().NotBeNull();
        fetched!.PaidFromCashAccountID.Should().Be(cashAccount.CashAccountID);
        var balanceAfter = await cashTransactionRepository.GetBalanceAsync(cashAccount.CashAccountID);
        balanceAfter.Should().Be(-150m);
        transaction.SourceType.Should().Be("PurchaseOrderInstallment");
        transaction.SourceID.Should().Be(installment.InstallmentID);
    }

    private async Task EnsureSchemaAsync(MySqlConnection connection)
    {
        await connection.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS employees (
                EmployeeID INT NOT NULL AUTO_INCREMENT,
                Name VARCHAR(150) NOT NULL,
                IsActive TINYINT(1) NOT NULL DEFAULT 1,
                PRIMARY KEY (EmployeeID)
            ) ENGINE=InnoDB;
            CREATE TABLE IF NOT EXISTS suppliers (
                SupplierID INT NOT NULL AUTO_INCREMENT,
                Name VARCHAR(150) NOT NULL,
                IsActive TINYINT(1) NOT NULL DEFAULT 1,
                PRIMARY KEY (SupplierID)
            ) ENGINE=InnoDB;
            CREATE TABLE IF NOT EXISTS orders (
                OrderID INT NOT NULL AUTO_INCREMENT,
                QuoteSectionID INT NOT NULL,
                CustomerID INT NOT NULL,
                OrderType TINYINT UNSIGNED NOT NULL,
                Status TINYINT UNSIGNED NOT NULL,
                CreatedAt DATETIME NOT NULL,
                PRIMARY KEY (OrderID)
            ) ENGINE=InnoDB;
            CREATE TABLE IF NOT EXISTS purchase_orders (
                PurchaseOrderID INT NOT NULL AUTO_INCREMENT,
                SupplierID INT NOT NULL,
                OrderDate DATETIME NOT NULL,
                Status TINYINT UNSIGNED NOT NULL,
                Notes VARCHAR(255) NULL,
                PRIMARY KEY (PurchaseOrderID),
                CONSTRAINT FK_test_purchase_orders_suppliers FOREIGN KEY (SupplierID) REFERENCES suppliers (SupplierID)
            ) ENGINE=InnoDB;
        ");

        var migrationsDirectory = Path.Combine(AppContext.BaseDirectory, "Migrations");
        foreach (var migration in new[]
        {
            "20260911_add_payment_installments.sql",
            "20260914_add_cash_accounts_and_transactions.sql",
            "20260914_add_purchase_order_installments.sql",
            "20260914_add_purchase_order_installment_cash_account.sql"
        })
        {
            await connection.ExecuteAsync(await File.ReadAllTextAsync(Path.Combine(migrationsDirectory, migration)));
        }
    }
}
