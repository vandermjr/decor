using Dapper;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Data;
using Decor.FluentSqlBuilder;
using Decor.Infrastructure.Data.Repositories;
using MySqlConnector;

namespace Decor.Infrastructure.IntegrationTests;

public sealed class ServiceSeparationMigrationIntegrationTests(MariaDbFixture fixture) : IClassFixture<MariaDbFixture>
{
    [Fact]
    public async Task Migration_PreservesCatalogAndCommercialHistory_AndIsIdempotent()
    {
        await using var connection = await CreateLegacySchemaAsync();
        await ApplyAsync(connection);
        var migrated = await connection.QuerySingleAsync<ServiceSnapshot>("SELECT * FROM services WHERE ServiceID = 20");
        migrated.Description.Should().Be("Instalacao");
        migrated.IsActive.Should().BeFalse();
        migrated.CostPrice.Should().Be(12.34m);
        migrated.SalePrice.Should().Be(56.78m);
        migrated.EmployeeCommissionValue.Should().Be(9.87m);
        migrated.Observations.Should().Be("historico preservado");
        (await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM products WHERE ProductType <> 1")).Should().Be(0);
        (await connection.ExecuteScalarAsync<int>("SELECT DefaultInstallationServiceID FROM products WHERE ProductID = 10")).Should().Be(20);
        foreach (var table in new[] { "quote_items", "order_items" })
        {
            var item = await connection.QuerySingleAsync<CatalogReference>($"SELECT ProductID, ServiceID FROM {table} WHERE ServiceID = 20");
            item.ProductID.Should().BeNull();
            item.ServiceID.Should().Be(20);
            (await connection.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM {table} WHERE ProductID = 10 AND ServiceID IS NULL")).Should().Be(1);
        }
        (await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM installation_appointments WHERE OrderItemID = 2")).Should().Be(1);
        (await connection.ExecuteScalarAsync<decimal>("SELECT Quantity * UnitPrice FROM order_items WHERE OrderItemID = 2")).Should().Be(113.56m);
        await connection.ExecuteAsync("UPDATE services SET SalePrice = 99 WHERE ServiceID = 20");
        await ApplyAsync(connection);
        (await connection.ExecuteScalarAsync<decimal>("SELECT SalePrice FROM services WHERE ServiceID = 20")).Should().Be(99m);
        (await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM services")).Should().Be(1);
        (await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM information_schema.KEY_COLUMN_USAGE WHERE TABLE_SCHEMA = DATABASE() AND REFERENCED_TABLE_NAME = 'services'")).Should().Be(3);
        (await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM role_permissions rp JOIN permissions p USING (PermissionID) JOIN roles r USING (RoleID) WHERE r.RoleName = 'Administradores' AND p.PermissionCode LIKE 'Services.%'")).Should().Be(4);
        await connection.ExecuteAsync("DELETE rp FROM role_permissions rp JOIN permissions p USING (PermissionID) WHERE p.PermissionCode = 'Services.Edit'");
        await ApplyAsync(connection);
        (await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM role_permissions rp JOIN permissions p USING (PermissionID) WHERE p.PermissionCode = 'Services.Edit'")).Should().Be(0);
    }

    [Theory]
    [InlineData("quote_items")]
    [InlineData("order_items")]
    public async Task Migration_EnforcesExactlyOneCatalogAndForeignKeys(string table)
    {
        await using var connection = await CreateLegacySchemaAsync();
        await ApplyAsync(connection);
        foreach (var invalid in new[] { "ProductID = NULL, ServiceID = NULL", "ProductID = 10, ServiceID = 20", "ProductID = NULL, ServiceID = 999", "ProductID = 999, ServiceID = NULL" })
        {
            var action = () => connection.ExecuteAsync($"UPDATE {table} SET {invalid}");
            await action.Should().ThrowAsync<MySqlException>();
        }
        var serviceProduct = () => connection.ExecuteAsync("UPDATE products SET ProductType = 2 WHERE ProductID = 10");
        await serviceProduct.Should().ThrowAsync<MySqlException>();
        var invalidDefault = () => connection.ExecuteAsync("UPDATE products SET DefaultInstallationServiceID = 10 WHERE ProductID = 10");
        await invalidDefault.Should().ThrowAsync<MySqlException>();
    }

    [Theory]
    [InlineData("stock_movements", "ProductID")]
    [InlineData("stock_balances", "ProductID")]
    [InlineData("stock_reservations", "ProductID")]
    [InlineData("purchase_order_items", "ProductID")]
    [InlineData("product_kit_components", "KitProductID")]
    [InlineData("product_kit_components", "ComponentProductID")]
    [InlineData("custom_history", "CatalogID")]
    public async Task Migration_RejectsInvalidHistoryWithoutDeletingOrMigratingIt(string table, string column)
    {
        await using var connection = await CreateLegacySchemaAsync();
        await connection.ExecuteAsync($"CREATE TABLE {table} (HistoryID INT PRIMARY KEY, {column} INT NOT NULL, FOREIGN KEY ({column}) REFERENCES products (ProductID)) ENGINE=InnoDB; INSERT INTO {table} VALUES (1, 20)");
        var action = () => ApplyAsync(connection);
        await action.Should().ThrowAsync<MySqlException>().WithMessage("*reconcile first*");
        (await connection.ExecuteScalarAsync<int>($"SELECT {column} FROM {table}")).Should().Be(20);
        (await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM products WHERE ProductID = 20")).Should().Be(1);
        (await connection.ExecuteScalarAsync<int>("SELECT ProductID FROM order_items WHERE OrderItemID = 2")).Should().Be(20);
        (await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM services")).Should().Be(0);
    }

    [Theory]
    [InlineData("Different service")]
    [InlineData("instalacao")]
    public async Task Migration_RejectsServiceIdCollisionWithoutOverwrite(string description)
    {
        await using var connection = await CreateLegacySchemaAsync();
        await ApplyAsync(connection);
        await connection.ExecuteAsync("ALTER TABLE products DROP CONSTRAINT CK_products_goods_only; INSERT INTO products (ProductID, ProductType, Description) VALUES (20, 2, @description)", new { description });
        var action = () => ApplyAsync(connection);
        await action.Should().ThrowAsync<MySqlException>().WithMessage("*collision*");
        (await connection.ExecuteScalarAsync<string>("SELECT Description FROM services WHERE ServiceID = 20")).Should().Be("Instalacao");
        (await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM products WHERE ProductID = 20")).Should().Be(1);
    }

    [Theory]
    [InlineData("quote_items", "both")]
    [InlineData("quote_items", "neither")]
    [InlineData("quote_items", "missing")]
    [InlineData("order_items", "both")]
    [InlineData("order_items", "neither")]
    [InlineData("order_items", "missing")]
    public async Task Migration_RejectsInvalidCommercialReferencesWithoutMovingLegacyData(string table, string invalidState)
    {
        await using var connection = await CreateLegacySchemaAsync();
        await connection.ExecuteAsync($"ALTER TABLE {table} DROP FOREIGN KEY FK_{table}_products; ALTER TABLE {table} ADD ServiceID INT NULL; ALTER TABLE {table} MODIFY ProductID INT NULL");
        var assignment = invalidState switch
        {
            "both" => "ServiceID = 999",
            "neither" => "ProductID = NULL, ServiceID = NULL",
            _ => "ProductID = 999, ServiceID = NULL"
        };
        await connection.ExecuteAsync($"UPDATE {table} SET {assignment}");
        var action = () => ApplyAsync(connection);
        await action.Should().ThrowAsync<MySqlException>().WithMessage("*Commercial item*");
        (await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM products WHERE ProductID = 20")).Should().Be(1);
        (await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM services")).Should().Be(0);
    }

    [Theory]
    [InlineData("StockQuantity = 1", "ProductID = 20")]
    [InlineData("MinimumStock = 1", "ProductID = 20")]
    [InlineData("DefaultInstallationServiceID = 10", "ProductID = 10")]
    [InlineData("DefaultInstallationServiceID = 10", "ProductID = 20")]
    [InlineData("ProductType = 3", "ProductID = 20")]
    public async Task Migration_RejectsInvalidLegacyFieldsBeforeMovingData(string update, string predicate)
    {
        await using var connection = await CreateLegacySchemaAsync();
        await connection.ExecuteAsync($"UPDATE products SET {update} WHERE {predicate}");
        var action = () => ApplyAsync(connection);
        await action.Should().ThrowAsync<MySqlException>();
        (await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM products WHERE ProductID = 20")).Should().Be(1);
        (await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM services")).Should().Be(0);
        (await connection.ExecuteScalarAsync<int>("SELECT ProductID FROM quote_items WHERE QuoteItemID = 2")).Should().Be(20);
    }

    [Fact]
    public async Task Migration_PreservesValidGoodsKitAndExistingPermissionRevocation()
    {
        await using var connection = await CreateLegacySchemaAsync();
        await connection.ExecuteAsync("""
            INSERT INTO products (ProductID, Description) VALUES (30, 'Componente');
            CREATE TABLE product_kit_components (
                ComponentID INT PRIMARY KEY AUTO_INCREMENT, KitProductID INT NOT NULL, ComponentProductID INT NOT NULL,
                Quantity DECIMAL(10,3) NOT NULL, IsRequired TINYINT(1) NOT NULL DEFAULT 1, DisplayOrder INT NOT NULL DEFAULT 0,
                FOREIGN KEY (KitProductID) REFERENCES products (ProductID),
                FOREIGN KEY (ComponentProductID) REFERENCES products (ProductID)
            ) ENGINE=InnoDB;
            INSERT INTO product_kit_components (KitProductID, ComponentProductID, Quantity) VALUES (10, 30, 2.5);
            INSERT INTO permissions (PermissionCode, Description) VALUES ('Services.View', 'Previously revoked');
            """);
        await ApplyAsync(connection);
        (await connection.ExecuteScalarAsync<decimal>("SELECT Quantity FROM product_kit_components WHERE KitProductID = 10 AND ComponentProductID = 30")).Should().Be(2.5m);
        (await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM role_permissions rp JOIN permissions p USING (PermissionID) WHERE p.PermissionCode = 'Services.View'")).Should().Be(0);
    }

    [Fact]
    public async Task Migration_WithoutLegacyServicesStillSeparatesSchema()
    {
        await using var connection = await CreateLegacySchemaAsync();
        await connection.ExecuteAsync("DELETE FROM installation_appointments; DELETE FROM order_items WHERE ProductID = 20; DELETE FROM quote_items WHERE ProductID = 20; UPDATE products SET DefaultInstallationServiceID = NULL; DELETE FROM products WHERE ProductType = 2");
        await ApplyAsync(connection);
        await ApplyAsync(connection);
        (await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM services")).Should().Be(0);
        await connection.ExecuteAsync("INSERT INTO services (ServiceID, Description) VALUES (10, 'Same ID different catalog'); UPDATE products SET DefaultInstallationServiceID = 10 WHERE ProductID = 10");
        (await connection.ExecuteScalarAsync<int>("SELECT DefaultInstallationServiceID FROM products WHERE ProductID = 10")).Should().Be(10);
    }

    [Fact]
    public async Task ProductRepository_LegacyServiceLookupUsesSeparateCatalog_AndRejectsServiceWrites()
    {
        await using var connection = await CreateLegacySchemaAsync();
        await ApplyAsync(connection);
        var repository = new ProductRepository(new TestDatabase(fixture.ConnectionString), () => null!, FluentCommandBuilder.Create);
        repository.ServiceProductExists(20).Should().BeTrue();
        repository.ServiceProductExists(10).Should().BeFalse();
        repository.GoodProductExists(10).Should().BeTrue();
        repository.GoodProductExists(20).Should().BeFalse();
        var save = () => repository.SaveAsync(new Product { ProductType = ProductType.Service });
        await save.Should().ThrowAsync<System.ComponentModel.DataAnnotations.ValidationException>();
        var syncSave = () => repository.Save(new Product { ProductType = ProductType.Service });
        syncSave.Should().Throw<System.ComponentModel.DataAnnotations.ValidationException>();
    }

    private async Task<MySqlConnection> CreateLegacySchemaAsync()
    {
        var connection = new MySqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        await connection.ExecuteAsync("""
            SET FOREIGN_KEY_CHECKS = 0;
            DROP TABLE IF EXISTS installation_appointments, quote_items, order_items, stock_movements, stock_balances,
                stock_reservations, purchase_order_items, product_kit_components, custom_history, products, services;
            SET FOREIGN_KEY_CHECKS = 1;
            DELETE FROM permissions WHERE PermissionCode LIKE 'Services.%';
            INSERT INTO roles (RoleName, Description, HierarchyLevel, IsSystemProtected)
                VALUES ('Administradores', 'System administrators', 200, 1)
                ON DUPLICATE KEY UPDATE IsSystemProtected = 1;
            CREATE TABLE products (
                ProductID INT PRIMARY KEY AUTO_INCREMENT, Description VARCHAR(50) NOT NULL,
                IsActive TINYINT(1) NOT NULL DEFAULT 1, ProductType TINYINT UNSIGNED NOT NULL DEFAULT 1,
                BrandID INT NOT NULL DEFAULT 1, SubgroupID INT NULL, StockUnitID INT NULL,
                StockQuantity DECIMAL(10,3) NOT NULL DEFAULT 0, MinimumStock DECIMAL(10,3) NOT NULL DEFAULT 0,
                CostPrice DECIMAL(10,2) NULL, SalePrice DECIMAL(10,2) NULL, EmployeeCommissionValue DECIMAL(10,2) NULL,
                Observations TEXT NULL, DefaultInstallationServiceID INT NULL,
                CONSTRAINT FK_products_default_installation_service FOREIGN KEY (DefaultInstallationServiceID) REFERENCES products (ProductID)
            ) ENGINE=InnoDB;
            CREATE TABLE quote_items (
                QuoteItemID INT PRIMARY KEY AUTO_INCREMENT, QuoteSectionID INT NOT NULL,
                ProductID INT NOT NULL, Quantity DECIMAL(10,3) NOT NULL, UnitPrice DECIMAL(10,2) NOT NULL,
                HasInstallationService TINYINT(1) NOT NULL DEFAULT 0,
                CONSTRAINT FK_quote_items_products FOREIGN KEY (ProductID) REFERENCES products (ProductID)
            ) ENGINE=InnoDB;
            CREATE TABLE order_items (
                OrderItemID INT PRIMARY KEY AUTO_INCREMENT, OrderID INT NOT NULL, QuoteItemID INT NULL,
                ProductID INT NOT NULL, Quantity DECIMAL(10,3) NOT NULL, UnitPrice DECIMAL(10,2) NOT NULL,
                HasInstallationService TINYINT(1) NOT NULL DEFAULT 0,
                CONSTRAINT FK_order_items_products FOREIGN KEY (ProductID) REFERENCES products (ProductID),
                CONSTRAINT FK_order_items_quote_items FOREIGN KEY (QuoteItemID) REFERENCES quote_items (QuoteItemID)
            ) ENGINE=InnoDB;
            CREATE TABLE installation_appointments (
                AppointmentID INT PRIMARY KEY, OrderItemID INT NOT NULL,
                FOREIGN KEY (OrderItemID) REFERENCES order_items (OrderItemID)
            ) ENGINE=InnoDB;
            INSERT INTO products (ProductID, ProductType, Description, IsActive, CostPrice, SalePrice, EmployeeCommissionValue, Observations)
                VALUES (10, 1, 'Cortina', 1, 10, 20, NULL, NULL), (20, 2, 'Instalacao', 0, 12.34, 56.78, 9.87, 'historico preservado');
            UPDATE products SET DefaultInstallationServiceID = 20 WHERE ProductID = 10;
            INSERT INTO quote_items (QuoteItemID, QuoteSectionID, ProductID, Quantity, UnitPrice) VALUES (1, 1, 10, 1, 20), (2, 1, 20, 2, 56.78);
            INSERT INTO order_items (OrderItemID, OrderID, QuoteItemID, ProductID, Quantity, UnitPrice) VALUES (1, 1, 1, 10, 1, 20), (2, 1, 2, 20, 2, 56.78);
            INSERT INTO installation_appointments VALUES (1, 2);
            """);
        return connection;
    }

    private static async Task ApplyAsync(MySqlConnection connection)
    {
        var assembly = typeof(ProductRepository).Assembly;
        var resource = assembly.GetManifestResourceNames().Single(name => name.EndsWith("20261008_separate_services_from_products.sql", StringComparison.Ordinal));
        using var stream = assembly.GetManifestResourceStream(resource)!;
        using var reader = new StreamReader(stream);
        await connection.ExecuteAsync(await reader.ReadToEndAsync());
    }

    private sealed class CatalogReference
    {
        public int? ProductID { get; set; }
        public int? ServiceID { get; set; }
    }

    private sealed class ServiceSnapshot
    {
        public string Description { get; set; } = "";
        public bool IsActive { get; set; }
        public decimal? CostPrice { get; set; }
        public decimal? SalePrice { get; set; }
        public decimal? EmployeeCommissionValue { get; set; }
        public string? Observations { get; set; }
    }

    private sealed class TestDatabase(string connectionString) : IDatabaseConnection
    {
        public System.Data.IDbConnection CreateConnection() => new MySqlConnection(connectionString);
    }
}