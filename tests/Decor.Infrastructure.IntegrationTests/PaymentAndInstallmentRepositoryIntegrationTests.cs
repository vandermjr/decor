using System.Data;
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

public sealed class PaymentAndInstallmentRepositoryIntegrationTests(MariaDbFixture fixture) : IClassFixture<MariaDbFixture>
{
    [Fact]
    public async Task PaymentMethodsAndInstallments_IntegrationFlow()
    {
        await using var connection = new MySqlConnection(fixture.ConnectionString);
        await EnsureTablesAndMigrationAppliedAsync(connection);

        var dbConn = new DatabaseConnection(fixture.ConnectionString);
        var dialect = new MariaDBDialect();

        var paymentRepo = new PaymentMethodsRepository(dbConn, () => FluentCommandBuilder.Create(dialect));
        var installmentRepo = new OrderInstallmentRepository(dbConn, () => FluentCommandBuilder.Create(dialect));

        // 1. Verify seeded PaymentMethods from migration
        var allMethods = await paymentRepo.SearchGetByAsync();
        allMethods.Should().Contain(p => p.Name == "PIX");
        allMethods.Should().Contain(p => p.Name == "Boleto");

        var pix = allMethods.First(p => p.Name == "PIX");

        // 2. Create customer, quote, section, order
        var customerId = await connection.QuerySingleAsync<int>(
            "INSERT INTO customers (Name, IsActive) VALUES ('Cliente Integracao Parcela', 1); SELECT LAST_INSERT_ID();");

        var employeeId = await connection.QuerySingleAsync<int>(
            "INSERT INTO employees (Name, IsActive) VALUES ('Funcionario Integracao Parcela', 1); SELECT LAST_INSERT_ID();");

        var quoteId = await connection.QuerySingleAsync<int>(
            $"INSERT INTO quotes (CustomerID, CreatedByEmployeeID, SourceType, CreatedAt) VALUES ({customerId}, {employeeId}, 1, NOW()); SELECT LAST_INSERT_ID();");

        var sectionId = await connection.QuerySingleAsync<int>(
            $"INSERT INTO quote_sections (QuoteID, SectionType, Status, CreatedAt) VALUES ({quoteId}, 1, 4, NOW()); SELECT LAST_INSERT_ID();");

        var orderId = await connection.QuerySingleAsync<int>(
            $"INSERT INTO orders (QuoteSectionID, CustomerID, OrderType, Status, CreatedAt) VALUES ({sectionId}, {customerId}, 1, 2, NOW()); SELECT LAST_INSERT_ID();");

        // 3. Save Installment
        var installment = new OrderInstallment
        {
            OrderID = orderId,
            PaymentMethodID = pix.PaymentMethodID,
            InstallmentNumber = 1,
            Amount = 150.00m,
            DueDate = DateTime.UtcNow.AddDays(15).Date,
            Status = OrderInstallmentStatus.Pending
        };

        var saved = await installmentRepo.SaveAsync(installment);
        saved.Should().Be(1);
        installment.InstallmentID.Should().BeGreaterThan(0);

        // 4. Check IsInUse on PaymentMethod
        var inUse = await paymentRepo.IsInUseAsync(pix.PaymentMethodID);
        inUse.Should().BeTrue();

        // 5. GetByOrderIdAsync
        var orderInstallments = await installmentRepo.GetByOrderIdAsync(orderId);
        orderInstallments.Should().HaveCount(1);
        orderInstallments[0].InstallmentID.Should().Be(installment.InstallmentID);
        orderInstallments[0].Amount.Should().Be(150.00m);

        // 6. Update Status & PaidAt
        installment.Status = OrderInstallmentStatus.Paid;
        installment.PaidAt = DateTime.UtcNow;
        installment.ReceivedByEmployeeID = employeeId;
        await installmentRepo.SaveAsync(installment);

        var fetched = await installmentRepo.GetByIdAsync(installment.InstallmentID);
        fetched.Should().NotBeNull();
        fetched!.Status.Should().Be(OrderInstallmentStatus.Paid);
        fetched.ReceivedByEmployeeID.Should().Be(employeeId);
    }

    private async Task EnsureTablesAndMigrationAppliedAsync(MySqlConnection connection)
    {
        var migrationsDirectory = Path.Combine(AppContext.BaseDirectory, "Migrations");
        var migrationFile = Path.Combine(migrationsDirectory, "20260911_add_payment_installments.sql");

        await connection.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS customers (
                CustomerID INT NOT NULL AUTO_INCREMENT,
                Name VARCHAR(150) NOT NULL,
                IsActive TINYINT(1) NOT NULL DEFAULT 1,
                PRIMARY KEY (CustomerID)
            ) ENGINE=InnoDB;

            CREATE TABLE IF NOT EXISTS employees (
                EmployeeID INT NOT NULL AUTO_INCREMENT,
                Name VARCHAR(150) NOT NULL,
                IsActive TINYINT(1) NOT NULL DEFAULT 1,
                PRIMARY KEY (EmployeeID)
            ) ENGINE=InnoDB;

            CREATE TABLE IF NOT EXISTS quotes (
                QuoteID INT NOT NULL AUTO_INCREMENT,
                CustomerID INT NOT NULL,
                CreatedByEmployeeID INT NOT NULL,
                SourceType TINYINT UNSIGNED NOT NULL,
                CreatedAt DATETIME NOT NULL,
                PRIMARY KEY (QuoteID)
            ) ENGINE=InnoDB;

            CREATE TABLE IF NOT EXISTS quote_sections (
                QuoteSectionID INT NOT NULL AUTO_INCREMENT,
                QuoteID INT NOT NULL,
                SectionType TINYINT UNSIGNED NOT NULL,
                Status TINYINT UNSIGNED NOT NULL,
                CreatedAt DATETIME NOT NULL,
                PRIMARY KEY (QuoteSectionID)
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
        ");

        if (File.Exists(migrationFile))
        {
            var sql = await File.ReadAllTextAsync(migrationFile);
            await connection.ExecuteAsync(sql);
        }
    }
}
