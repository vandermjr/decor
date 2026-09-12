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

public sealed class StockReservationRepositoryIntegrationTests(MariaDbFixture fixture) : IClassFixture<MariaDbFixture>
{
    [Fact]
    public async Task StockReservationRepository_Save_Get_And_CalculateActiveQuantities()
    {
        await using var connection = new MySqlConnection(fixture.ConnectionString);
        await EnsureTablesCreatedAsync(connection);

        // Seed dependencies
        var customerId = await connection.QuerySingleAsync<int>(
            "INSERT INTO customers (Name, IsActive) VALUES ('Cliente Reserva Teste', 1); SELECT LAST_INSERT_ID();");
        var employeeId = await connection.QuerySingleAsync<int>(
            "INSERT INTO employees (Name, IsActive) VALUES ('Funcionario Reserva Teste', 1); SELECT LAST_INSERT_ID();");
        var quoteId = await connection.QuerySingleAsync<int>(
            $"INSERT INTO quotes (CustomerID, CreatedByEmployeeID, SourceType, CreatedAt) VALUES ({customerId}, {employeeId}, 1, NOW()); SELECT LAST_INSERT_ID();");
        var sectionId = await connection.QuerySingleAsync<int>(
            $"INSERT INTO quote_sections (QuoteID, SectionType, Status, CreatedAt) VALUES ({quoteId}, 1, 4, NOW()); SELECT LAST_INSERT_ID();");
        var productId = await connection.QuerySingleAsync<int>(
            "INSERT INTO products (Description, IsActive, ProductType) VALUES ('Tecido Linho Bege', 1, 1); SELECT LAST_INSERT_ID();");
        var quoteItemId = await connection.QuerySingleAsync<int>(
            $"INSERT INTO quote_items (QuoteSectionID, ProductID, Quantity, UnitPrice, HasInstallationService) VALUES ({sectionId}, {productId}, 10, 80.00, 0); SELECT LAST_INSERT_ID();");
        var orderId = await connection.QuerySingleAsync<int>(
            $"INSERT INTO orders (QuoteSectionID, CustomerID, OrderType, Status, CreatedAt) VALUES ({sectionId}, {customerId}, 1, 2, NOW()); SELECT LAST_INSERT_ID();");
        var orderItemId = await connection.QuerySingleAsync<int>(
            $"INSERT INTO order_items (OrderID, QuoteItemID, ProductID, Quantity, UnitPrice) VALUES ({orderId}, {quoteItemId}, {productId}, 10, 80.00); SELECT LAST_INSERT_ID();");
        var stockLocationId = await connection.QuerySingleAsync<int>(
            "INSERT INTO stock_locations (Name, LocationType, IsActive) VALUES ('Depósito Principal Teste', 1, 1); SELECT LAST_INSERT_ID();");

        var repo = CreateReservationRepository();

        // 1. Create active reservation
        var reservation = new StockReservation
        {
            OrderItemID = orderItemId,
            ProductID = productId,
            StockLocationID = stockLocationId,
            Quantity = 4.5m,
            Status = StockReservationStatus.Active,
            CreatedByEmployeeID = employeeId,
            CreatedAt = DateTime.UtcNow
        };

        var saveResult = await repo.SaveAsync(reservation);
        saveResult.Should().Be(1);
        reservation.ReservationID.Should().BeGreaterThan(0);

        // 2. Query by ID
        var fetched = await repo.GetByIdAsync(reservation.ReservationID);
        fetched.Should().NotBeNull();
        fetched!.Quantity.Should().Be(4.5m);
        fetched.Status.Should().Be(StockReservationStatus.Active);
        fetched.OrderItemID.Should().Be(orderItemId);

        // 3. Query Active By OrderItemID
        var activeByItem = await repo.GetActiveByOrderItemIdAsync(orderItemId);
        activeByItem.Should().NotBeNull();
        activeByItem!.ReservationID.Should().Be(reservation.ReservationID);

        // 4. Query Total Active Reserved Quantity
        var totalReserved = await repo.GetTotalActiveReservedQuantityAsync(productId, stockLocationId);
        totalReserved.Should().Be(4.5m);

        // 5. Query By OrderId
        var forOrder = await repo.GetByOrderIdAsync(orderId);
        forOrder.Should().HaveCount(1);
        forOrder[0].ReservationID.Should().Be(reservation.ReservationID);

        // 6. Update to Released
        reservation.Status = StockReservationStatus.Released;
        reservation.ReleasedAt = DateTime.UtcNow;
        var updateResult = await repo.SaveAsync(reservation);
        updateResult.Should().Be(1);

        var activeAfterRelease = await repo.GetActiveByOrderItemIdAsync(orderItemId);
        activeAfterRelease.Should().BeNull();

        var totalReservedAfterRelease = await repo.GetTotalActiveReservedQuantityAsync(productId, stockLocationId);
        totalReservedAfterRelease.Should().Be(0m);
    }

    [Fact]
    public async Task StockMovementRepository_RegisterTransferAsync_WithOrderItemID_SavesOrderItemID()
    {
        await using var connection = new MySqlConnection(fixture.ConnectionString);
        await EnsureTablesCreatedAsync(connection);

        // Seed dependencies
        var customerId = await connection.QuerySingleAsync<int>(
            "INSERT INTO customers (Name, IsActive) VALUES ('Cliente Mov Teste', 1); SELECT LAST_INSERT_ID();");
        var employeeId = await connection.QuerySingleAsync<int>(
            "INSERT INTO employees (Name, IsActive) VALUES ('Funcionario Mov Teste', 1); SELECT LAST_INSERT_ID();");
        var quoteId = await connection.QuerySingleAsync<int>(
            $"INSERT INTO quotes (CustomerID, CreatedByEmployeeID, SourceType, CreatedAt) VALUES ({customerId}, {employeeId}, 1, NOW()); SELECT LAST_INSERT_ID();");
        var sectionId = await connection.QuerySingleAsync<int>(
            $"INSERT INTO quote_sections (QuoteID, SectionType, Status, CreatedAt) VALUES ({quoteId}, 1, 4, NOW()); SELECT LAST_INSERT_ID();");
        var productId = await connection.QuerySingleAsync<int>(
            "INSERT INTO products (Description, IsActive, ProductType) VALUES ('Tecido Seda', 1, 1); SELECT LAST_INSERT_ID();");
        var quoteItemId = await connection.QuerySingleAsync<int>(
            $"INSERT INTO quote_items (QuoteSectionID, ProductID, Quantity, UnitPrice, HasInstallationService) VALUES ({sectionId}, {productId}, 5, 120.00, 0); SELECT LAST_INSERT_ID();");
        var orderId = await connection.QuerySingleAsync<int>(
            $"INSERT INTO orders (QuoteSectionID, CustomerID, OrderType, Status, CreatedAt) VALUES ({sectionId}, {customerId}, 1, 2, NOW()); SELECT LAST_INSERT_ID();");
        var orderItemId = await connection.QuerySingleAsync<int>(
            $"INSERT INTO order_items (OrderID, QuoteItemID, ProductID, Quantity, UnitPrice) VALUES ({orderId}, {quoteItemId}, {productId}, 5, 120.00); SELECT LAST_INSERT_ID();");
        var sourceLocId = await connection.QuerySingleAsync<int>(
            "INSERT INTO stock_locations (Name, LocationType, IsActive) VALUES ('Depósito Central', 1, 1); SELECT LAST_INSERT_ID();");
        var destLocId = await connection.QuerySingleAsync<int>(
            "INSERT INTO stock_locations (Name, LocationType, IsActive) VALUES ('Depósito Costura', 1, 1); SELECT LAST_INSERT_ID();");

        var movementRepo = CreateMovementRepository();

        var outbound = new StockMovement
        {
            ProductID = productId,
            StockLocationID = sourceLocId,
            Quantity = -3m,
            MovementType = StockMovementType.Transferencia,
            PerformedByEmployeeID = employeeId,
            MovementDate = DateTime.UtcNow,
            OrderItemID = orderItemId
        };
        var inbound = new StockMovement
        {
            ProductID = productId,
            StockLocationID = destLocId,
            Quantity = 3m,
            MovementType = StockMovementType.Transferencia,
            PerformedByEmployeeID = employeeId,
            MovementDate = DateTime.UtcNow,
            OrderItemID = orderItemId
        };

        var transferId = await movementRepo.RegisterTransferAsync(outbound, inbound);
        transferId.Should().NotBeEmpty();

        var movements = (await movementRepo.GetByTransferIdAsync(transferId)).ToList();
        movements.Should().HaveCount(2);
        movements.All(m => m.OrderItemID == orderItemId).Should().BeTrue();
    }

    private StockReservationRepository CreateReservationRepository()
    {
        var dbConnection = new DatabaseConnection(fixture.ConnectionString);
        return new StockReservationRepository(dbConnection, () => FluentCommandBuilder.Create(new MariaDBDialect()));
    }

    private StockMovementRepository CreateMovementRepository()
    {
        var dbConnection = new DatabaseConnection(fixture.ConnectionString);
        return new StockMovementRepository(dbConnection, () => FluentCommandBuilder.Create(new MariaDBDialect()));
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

            CREATE TABLE IF NOT EXISTS orders (
                OrderID INT NOT NULL AUTO_INCREMENT,
                QuoteSectionID INT NOT NULL,
                CustomerID INT NOT NULL,
                OrderType TINYINT UNSIGNED NOT NULL,
                Status TINYINT UNSIGNED NOT NULL,
                RequiresDownPayment TINYINT(1) NULL,
                ManufacturingDeadline DATE NULL,
                InstallationDeadline DATE NULL,
                CreatedAt DATETIME NOT NULL,
                PRIMARY KEY (OrderID)
            ) ENGINE=InnoDB;

            CREATE TABLE IF NOT EXISTS order_items (
                OrderItemID INT NOT NULL AUTO_INCREMENT,
                OrderID INT NOT NULL,
                QuoteItemID INT NOT NULL,
                ProductID INT NOT NULL,
                Quantity DECIMAL(12,3) NOT NULL DEFAULT 0,
                UnitPrice DECIMAL(12,2) NOT NULL DEFAULT 0,
                HasInstallationService TINYINT(1) NOT NULL DEFAULT 0,
                SentToProductionAt DATETIME NULL,
                SentToProductionByEmployeeID INT NULL,
                PRIMARY KEY (OrderItemID)
            ) ENGINE=InnoDB;

            CREATE TABLE IF NOT EXISTS stock_locations (
                StockLocationID INT NOT NULL AUTO_INCREMENT,
                Name VARCHAR(100) NOT NULL,
                LocationType TINYINT UNSIGNED NOT NULL,
                PartnerID INT NULL,
                IsActive TINYINT(1) NOT NULL DEFAULT 1,
                PRIMARY KEY (StockLocationID)
            ) ENGINE=InnoDB;

            CREATE TABLE IF NOT EXISTS stock_balances (
                StockBalanceID INT NOT NULL AUTO_INCREMENT,
                ProductID INT NOT NULL,
                StockLocationID INT NOT NULL,
                Quantity DECIMAL(7,3) NOT NULL DEFAULT 0,
                UpdatedAt DATETIME NOT NULL,
                PRIMARY KEY (StockBalanceID),
                UNIQUE KEY UX_stock_balances_product_location (ProductID, StockLocationID)
            ) ENGINE=InnoDB;

            CREATE TABLE IF NOT EXISTS stock_movements (
                StockMovementID INT NOT NULL AUTO_INCREMENT,
                ProductID INT NOT NULL,
                StockLocationID INT NOT NULL,
                Quantity DECIMAL(7,3) NOT NULL,
                MovementType TINYINT UNSIGNED NOT NULL,
                TransferID CHAR(36) NULL,
                Reason TINYINT UNSIGNED NULL,
                Justification VARCHAR(255) NULL,
                AuthorizedByEmployeeID INT NULL,
                PerformedByEmployeeID INT NOT NULL,
                ReviewStatus TINYINT UNSIGNED NULL,
                ReviewedByEmployeeID INT NULL,
                ReviewedAt DATETIME NULL,
                MovementDate DATETIME NOT NULL,
                Notes VARCHAR(255) NULL,
                OrderItemID INT NULL,
                PRIMARY KEY (StockMovementID)
            ) ENGINE=InnoDB;

            CREATE TABLE IF NOT EXISTS stock_reservations (
                ReservationID INT NOT NULL AUTO_INCREMENT,
                OrderItemID INT NOT NULL,
                ProductID INT NOT NULL,
                StockLocationID INT NOT NULL,
                Quantity DECIMAL(12,3) NOT NULL DEFAULT 0,
                Status TINYINT UNSIGNED NOT NULL,
                CreatedByEmployeeID INT NOT NULL,
                CreatedAt DATETIME NOT NULL,
                ReleasedAt DATETIME NULL,
                PRIMARY KEY (ReservationID)
            ) ENGINE=InnoDB;
        ");
    }
}
