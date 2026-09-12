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

public sealed class ProductKitComponentRepositoryIntegrationTests(MariaDbFixture fixture) : IClassFixture<MariaDbFixture>
{
    [Fact]
    public async Task ProductKitComponentRepository_Save_Get_And_CalculatePricing()
    {
        await using var connection = new MySqlConnection(fixture.ConnectionString);
        await EnsureTablesCreatedAsync(connection);

        var kitProductId = await connection.QuerySingleAsync<int>(
            "INSERT INTO products (Description, IsActive, ProductType, SalePrice) VALUES ('Kit Cortina Completo', 1, 1, NULL); SELECT LAST_INSERT_ID();");
        var componentAId = await connection.QuerySingleAsync<int>(
            "INSERT INTO products (Description, IsActive, ProductType, SalePrice) VALUES ('Tecido', 1, 1, 100.00); SELECT LAST_INSERT_ID();");
        var componentBId = await connection.QuerySingleAsync<int>(
            "INSERT INTO products (Description, IsActive, ProductType, SalePrice) VALUES ('Trilho', 1, 1, 50.00); SELECT LAST_INSERT_ID();");

        var repo = CreateRepository();

        var componentA = new ProductKitComponent
        {
            KitProductID = kitProductId,
            ComponentProductID = componentAId,
            Quantity = 2m,
            IsVisibleToCustomer = true,
            DisplayOrder = 1
        };
        var componentB = new ProductKitComponent
        {
            KitProductID = kitProductId,
            ComponentProductID = componentBId,
            Quantity = 1.5m,
            IsVisibleToCustomer = false,
            DisplayOrder = 2
        };

        (await repo.SaveAsync(componentA)).Should().Be(1);
        (await repo.SaveAsync(componentB)).Should().Be(1);

        var components = (await repo.GetByKitProductIdAsync(kitProductId)).ToList();
        components.Should().HaveCount(2);
        components[0].DisplayOrder.Should().Be(1);

        var pricing = (await repo.GetPricingByKitProductIdAsync(kitProductId)).ToList();
        pricing.Should().HaveCount(2);
        var suggestedPrice = pricing.Sum(p => (p.SalePrice ?? 0) * p.Quantity);
        suggestedPrice.Should().Be(2m * 100.00m + 1.5m * 50.00m);

        // Ciclo indireto: componentA (que agora seria kit) já tem kitProductId como seu componente?
        repo.RelationExists(kitProductId, componentAId).Should().BeTrue();
        repo.RelationExists(componentAId, kitProductId).Should().BeFalse();
    }

    private ProductKitComponentRepository CreateRepository()
    {
        var dbConnection = new DatabaseConnection(fixture.ConnectionString);
        return new ProductKitComponentRepository(dbConnection, () => FluentCommandBuilder.Create(new MariaDBDialect()));
    }

    private async Task EnsureTablesCreatedAsync(MySqlConnection connection)
    {
        await connection.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS products (
                ProductID INT NOT NULL AUTO_INCREMENT,
                Description VARCHAR(255) NULL,
                IsActive TINYINT(1) NOT NULL DEFAULT 1,
                ProductType TINYINT UNSIGNED NOT NULL DEFAULT 1,
                SalePrice DECIMAL(10,2) NULL,
                PRIMARY KEY (ProductID)
            ) ENGINE=InnoDB;

            CREATE TABLE IF NOT EXISTS product_kit_components (
                ComponentID INT NOT NULL AUTO_INCREMENT,
                KitProductID INT NOT NULL,
                ComponentProductID INT NOT NULL,
                Quantity DECIMAL(10,3) NOT NULL,
                IsVisibleToCustomer TINYINT(1) NOT NULL DEFAULT 1,
                DisplayOrder INT NOT NULL DEFAULT 0,
                PRIMARY KEY (ComponentID)
            ) ENGINE=InnoDB;");
    }
}
