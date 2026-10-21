using Dapper;
using Decor.Core.DTOs;
using Decor.FluentSqlBuilder;
using Decor.FluentSqlBuilder.Dialects.MariaDB;
using Decor.Infrastructure.Data;
using Decor.Infrastructure.Data.Repositories;
using Decor.Infrastructure.Data.Utils;
using MySqlConnector;

namespace Decor.Infrastructure.IntegrationTests;

public sealed class ProductNameMigrationIntegrationTests(MariaDbFixture fixture) : IClassFixture<MariaDbFixture>
{
    [Fact]
    public async Task ProductRepositoryCatalogSummary_MaterializesAndCountsProductStockStates()
    {
        await using var connection = new MySqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        await connection.ExecuteAsync("DROP TABLE IF EXISTS stock_reservations, stock_balances, products");

        try
        {
            await connection.ExecuteAsync("""
                CREATE TABLE products (
                    ProductID INT NOT NULL PRIMARY KEY,
                    IsActive TINYINT NOT NULL,
                    AcquisitionMode INT NOT NULL,
                    MinimumStock DECIMAL(10, 2) NOT NULL,
                    ProductType INT NOT NULL
                );
                CREATE TABLE stock_balances (
                    ProductID INT NOT NULL,
                    Quantity DECIMAL(10, 2) NOT NULL
                );
                CREATE TABLE stock_reservations (
                    ProductID INT NOT NULL,
                    Quantity DECIMAL(10, 2) NOT NULL,
                    Status INT NOT NULL
                );
                INSERT INTO products (ProductID, IsActive, AcquisitionMode, MinimumStock, ProductType)
                VALUES (1, 1, 1, 10, 1), (2, 0, 1, 0, 1), (3, 1, 2, 0, 1);
                INSERT INTO stock_balances (ProductID, Quantity) VALUES (1, 5);
                """);

            var repository = new ProductRepository(
                new DatabaseConnection(fixture.ConnectionString),
                () => new QueryContext(),
                () => FluentCommandBuilder.Create(new MariaDBDialect()));
            var summary = await repository.GetCatalogSummaryAsync();

            summary.Should().Be(new ProductCatalogSummaryDTO(3, 2, 1, 1));
        }
        finally
        {
            await connection.ExecuteAsync("DROP TABLE IF EXISTS stock_reservations, stock_balances, products");
        }
    }

    [Fact]
    public async Task Migration_RenamesProductColumnAndRebuildsFullTextIndexWithoutLosingData()
    {
        await using var connection = new MySqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        await connection.ExecuteAsync("DROP TABLE IF EXISTS products");

        try
        {
            await connection.ExecuteAsync("""
                CREATE TABLE products (
                    ProductID INT NOT NULL PRIMARY KEY,
                    Description VARCHAR(50) NOT NULL,
                    FULLTEXT INDEX IX_products_Description_FullText (Description)
                ) ENGINE=InnoDB;
                INSERT INTO products (ProductID, Description) VALUES (1, 'Produto preservado');
                """);

            var migrationPath = Path.Combine(AppContext.BaseDirectory, "Migrations", "20261019_rename_product_description_to_product.sql");
            await connection.ExecuteAsync(await File.ReadAllTextAsync(migrationPath));

            (await connection.ExecuteScalarAsync<string>("SELECT Product FROM products WHERE ProductID = 1"))
                .Should().Be("Produto preservado");
            (await connection.ExecuteScalarAsync<int>("""
                SELECT COUNT(*) FROM products
                WHERE MATCH(Product) AGAINST ('preservado' IN NATURAL LANGUAGE MODE)
                """)).Should().Be(1);
            (await connection.ExecuteScalarAsync<int>("""
                SELECT COUNT(*) FROM information_schema.COLUMNS
                WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'products' AND COLUMN_NAME = 'Description'
                """)).Should().Be(0);
            (await connection.ExecuteScalarAsync<int>("""
                SELECT COUNT(*) FROM information_schema.COLUMNS
                WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'products' AND COLUMN_NAME = 'Product'
                """)).Should().Be(1);
            (await connection.ExecuteScalarAsync<int>("""
                SELECT COUNT(*) FROM information_schema.STATISTICS
                WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'products'
                  AND INDEX_NAME = 'IX_products_Product_FullText' AND COLUMN_NAME = 'Product'
                """)).Should().Be(1);
        }
        finally
        {
            await connection.ExecuteAsync("DROP TABLE IF EXISTS products");
        }
    }
}
