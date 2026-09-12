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

public sealed class OrderRepositoryIntegrationTests(MariaDbFixture fixture) : IClassFixture<MariaDbFixture>
{
    [Fact]
    public async Task OrderRepository_SaveAndGetCompleteOrder_PersistsAggregateCorrectly()
    {
        await using var connection = new MySqlConnection(fixture.ConnectionString);
        await EnsureTablesCreatedAsync(connection);

        // Seed customer
        var customerId = await connection.QuerySingleAsync<int>(
            "INSERT INTO customers (Name, IsActive) VALUES ('Cliente Teste Order', 1); SELECT LAST_INSERT_ID();");

        // Seed employee
        var employeeId = await connection.QuerySingleAsync<int>(
            "INSERT INTO employees (Name, IsActive) VALUES ('Funcionario Teste', 1); SELECT LAST_INSERT_ID();");

        // Seed quote & section & item
        var quoteId = await connection.QuerySingleAsync<int>(
            $"INSERT INTO quotes (CustomerID, CreatedByEmployeeID, SourceType, CreatedAt) VALUES ({customerId}, {employeeId}, 1, NOW()); SELECT LAST_INSERT_ID();");

        var sectionId = await connection.QuerySingleAsync<int>(
            $"INSERT INTO quote_sections (QuoteID, SectionType, Status, CreatedAt) VALUES ({quoteId}, 2, 4, NOW()); SELECT LAST_INSERT_ID();");

        // Seed product
        var productId = await connection.QuerySingleAsync<int>(
            "INSERT INTO products (Description, IsActive, ProductType) VALUES ('Cortina Sob Encomenda', 1, 1); SELECT LAST_INSERT_ID();");

        var attributeId = await connection.QuerySingleAsync<int>(
            "INSERT INTO product_specification_attributes (ProductCategoryID, Name, DataType, IsRequired, DisplayOrder) VALUES (1, 'Largura', 1, 0, 1); SELECT LAST_INSERT_ID();");

        var quoteItemId = await connection.QuerySingleAsync<int>(
            $"INSERT INTO quote_items (QuoteSectionID, ProductID, Quantity, UnitPrice, HasInstallationService) VALUES ({sectionId}, {productId}, 2, 250.00, 1); SELECT LAST_INSERT_ID();");

        var repo = CreateRepository();

        // 1. Save Order
        var order = new Order
        {
            QuoteSectionID = sectionId,
            CustomerID = customerId,
            OrderType = OrderType.Custom,
            Status = OrderStatus.PendingApproval,
            CreatedAt = DateTime.UtcNow
        };

        await repo.SaveAsync(order);
        order.OrderID.Should().BeGreaterThan(0);

        // 2. Save OrderItem
        var orderItem = new OrderItem
        {
            OrderID = order.OrderID,
            QuoteItemID = quoteItemId,
            ProductID = productId,
            Quantity = 2,
            UnitPrice = 250.00m,
            HasInstallationService = true
        };

        await repo.SaveOrderItemAsync(orderItem);
        orderItem.OrderItemID.Should().BeGreaterThan(0);

        // 3. Save Spec Value
        var specValue = new OrderItemSpecificationValue
        {
            OrderItemID = orderItem.OrderItemID,
            AttributeID = attributeId,
            Value = "3.5m"
        };
        await repo.SaveSpecificationValueAsync(specValue);
        specValue.ValueID.Should().BeGreaterThan(0);

        // 4. Retrieve complete order
        var fetchedOrder = await repo.GetCompleteOrderAsync(order.OrderID);
        fetchedOrder.Should().NotBeNull();
        fetchedOrder!.OrderID.Should().Be(order.OrderID);
        fetchedOrder.QuoteSectionID.Should().Be(sectionId);
        fetchedOrder.OrderType.Should().Be(OrderType.Custom);
        fetchedOrder.Status.Should().Be(OrderStatus.PendingApproval);
        fetchedOrder.Items.Should().HaveCount(1);

        var fetchedItem = fetchedOrder.Items.First();
        fetchedItem.OrderItemID.Should().Be(orderItem.OrderItemID);
        fetchedItem.UnitPrice.Should().Be(250.00m);
        fetchedItem.SpecificationValues.Should().HaveCount(1);
        fetchedItem.SpecificationValues.First().Value.Should().Be("3.5m");

        // 5. Test GetByQuoteSectionIdAsync
        var bySection = await repo.GetByQuoteSectionIdAsync(sectionId);
        bySection.Should().NotBeNull();
        bySection!.OrderID.Should().Be(order.OrderID);
    }

    private async Task EnsureTablesCreatedAsync(MySqlConnection connection)
    {
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

            CREATE TABLE IF NOT EXISTS products (
                ProductID INT NOT NULL AUTO_INCREMENT,
                Description VARCHAR(255) NULL,
                IsActive TINYINT(1) NOT NULL DEFAULT 1,
                ProductType TINYINT UNSIGNED NOT NULL DEFAULT 1,
                PRIMARY KEY (ProductID)
            ) ENGINE=InnoDB;

            CREATE TABLE IF NOT EXISTS product_specification_attributes (
                AttributeID INT NOT NULL AUTO_INCREMENT,
                ProductCategoryID INT NOT NULL,
                Name VARCHAR(150) NOT NULL,
                DataType TINYINT UNSIGNED NOT NULL,
                IsRequired TINYINT(1) NOT NULL DEFAULT 0,
                DisplayOrder INT NOT NULL DEFAULT 0,
                PRIMARY KEY (AttributeID)
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

            CREATE TABLE IF NOT EXISTS quote_items (
                QuoteItemID INT NOT NULL AUTO_INCREMENT,
                QuoteSectionID INT NOT NULL,
                ProductID INT NOT NULL,
                Quantity DECIMAL(12,3) NOT NULL DEFAULT 0,
                UnitPrice DECIMAL(12,2) NULL,
                HasInstallationService TINYINT(1) NOT NULL DEFAULT 0,
                PRIMARY KEY (QuoteItemID)
            ) ENGINE=InnoDB;
        ");

        var migrationsDir = Path.Combine(AppContext.BaseDirectory, "Migrations");
        var orderSql = await File.ReadAllTextAsync(Path.Combine(migrationsDir, "20260911_add_order_commercial.sql"));
        await connection.ExecuteAsync(orderSql);
    }

    private OrderRepository CreateRepository()
    {
        var dbConn = new DatabaseConnection(fixture.ConnectionString);
        var dialect = new MariaDBDialect();
        return new OrderRepository(dbConn, () => FluentCommandBuilder.Create(dialect));
    }
}
