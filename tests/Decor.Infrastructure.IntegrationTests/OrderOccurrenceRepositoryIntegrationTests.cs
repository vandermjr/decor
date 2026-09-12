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

public sealed class OrderOccurrenceRepositoryIntegrationTests(MariaDbFixture fixture) : IClassFixture<MariaDbFixture>
{
    [Fact]
    public async Task Occurrences_MigrationRepositoriesAndTransaction_WorkAgainstMariaDb()
    {
        await using var connection = new MySqlConnection(fixture.ConnectionString);
        await PrepareSchemaAsync(connection);

        var seededReasons = (await connection.QueryAsync<string>("SELECT Description FROM occurrence_reasons ORDER BY ReasonID;")).ToList();
        seededReasons.Should().Contain(["Atraso de pagamento", "Atraso de entrega", "Troca", "Renegociação"]);

        var administratorPermissions = await connection.QuerySingleAsync<int>(@"
            SELECT COUNT(*)
            FROM role_permissions rp
            INNER JOIN roles r ON r.RoleID = rp.RoleID
            INNER JOIN permissions p ON p.PermissionID = rp.PermissionID
            WHERE r.RoleName = 'Administrador'
              AND p.PermissionCode IN (
                'OccurrenceReasons.View', 'OccurrenceReasons.Create', 'OccurrenceReasons.Edit',
                'OccurrenceReasons.Delete', 'OrderOccurrences.View', 'OrderOccurrences.Register');");
        administratorPermissions.Should().Be(6);

        var dialect = new MariaDBDialect();
        Func<FluentCommandBuilder> createBuilder = () => FluentCommandBuilder.Create(dialect);
        var databaseConnection = new DatabaseConnection(fixture.ConnectionString);
        var reasonRepository = new OccurrenceReasonRepository(databaseConnection, createBuilder);
        var orderRepository = new OrderRepository(databaseConnection, createBuilder);
        var occurrenceRepository = new OrderOccurrenceRepository(databaseConnection, createBuilder, orderRepository);

        var reason = new OccurrenceReason { Description = "Avaria no transporte", IsActive = true };
        await reasonRepository.SaveAsync(reason);
        (await reasonRepository.SearchGetByAsync("Avaria", 1, 10)).Should().ContainSingle(r => r.ReasonID == reason.ReasonID);

        var customerId = await connection.QuerySingleAsync<int>("INSERT INTO customers (Name) VALUES ('Cliente Ocorrência'); SELECT LAST_INSERT_ID();");
        var employeeId = await connection.QuerySingleAsync<int>("INSERT INTO employees (Name) VALUES ('Funcionário Ocorrência'); SELECT LAST_INSERT_ID();");
        var sectionId = await connection.QuerySingleAsync<int>("INSERT INTO quote_sections (Description) VALUES ('Seção Ocorrência'); SELECT LAST_INSERT_ID();");
        var orderId = await connection.QuerySingleAsync<int>($@"
            INSERT INTO orders (QuoteSectionID, CustomerID, OrderType, Status, CreatedAt)
            VALUES ({sectionId}, {customerId}, 2, 2, UTC_TIMESTAMP());
            SELECT LAST_INSERT_ID();");

        var order = (await orderRepository.GetByIdAsync(orderId))!;
        order.ManufacturingDeadline = new DateTime(2026, 10, 5);
        order.InstallationDeadline = new DateTime(2026, 10, 12);
        var olderOccurrence = new OrderOccurrence
        {
            OrderID = orderId,
            ReasonID = reason.ReasonID,
            RegisteredByEmployeeID = employeeId,
            RegisteredAt = new DateTime(2026, 9, 12, 10, 0, 0, DateTimeKind.Utc),
            Observation = "Primeiro registro",
            NewManufacturingDeadline = order.ManufacturingDeadline,
            NewInstallationDeadline = order.InstallationDeadline
        };
        await occurrenceRepository.RegisterAsync(olderOccurrence, order);

        var newerOccurrence = new OrderOccurrence
        {
            OrderID = orderId,
            ReasonID = reason.ReasonID,
            RegisteredByEmployeeID = employeeId,
            RegisteredAt = olderOccurrence.RegisteredAt.AddHours(1),
            Observation = "Segundo registro"
        };
        await occurrenceRepository.RegisterAsync(newerOccurrence, order);

        var history = await occurrenceRepository.GetHistoryForOrderAsync(orderId);
        history.Select(o => o.OccurrenceID).Should().ContainInOrder(newerOccurrence.OccurrenceID, olderOccurrence.OccurrenceID);

        var persistedOrder = (await orderRepository.GetByIdAsync(orderId))!;
        persistedOrder.ManufacturingDeadline.Should().Be(new DateTime(2026, 10, 5));
        persistedOrder.InstallationDeadline.Should().Be(new DateTime(2026, 10, 12));
        (await reasonRepository.IsInUseAsync(reason.ReasonID)).Should().BeTrue();

        persistedOrder.ManufacturingDeadline = new DateTime(2027, 1, 1);
        var invalidOccurrence = new OrderOccurrence
        {
            OrderID = orderId,
            ReasonID = reason.ReasonID,
            RegisteredByEmployeeID = int.MaxValue,
            RegisteredAt = DateTime.UtcNow,
            Observation = "Deve causar rollback",
            NewManufacturingDeadline = persistedOrder.ManufacturingDeadline
        };

        var act = () => occurrenceRepository.RegisterAsync(invalidOccurrence, persistedOrder);
        await act.Should().ThrowAsync<MySqlException>();

        var afterRollback = (await orderRepository.GetByIdAsync(orderId))!;
        afterRollback.ManufacturingDeadline.Should().Be(new DateTime(2026, 10, 5));
    }

    private static async Task PrepareSchemaAsync(MySqlConnection connection)
    {
        await connection.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS customers (
                CustomerID INT NOT NULL AUTO_INCREMENT,
                Name VARCHAR(150) NOT NULL,
                PRIMARY KEY (CustomerID)
            ) ENGINE=InnoDB;

            CREATE TABLE IF NOT EXISTS employees (
                EmployeeID INT NOT NULL AUTO_INCREMENT,
                Name VARCHAR(150) NOT NULL,
                PRIMARY KEY (EmployeeID)
            ) ENGINE=InnoDB;

            CREATE TABLE IF NOT EXISTS quote_sections (
                QuoteSectionID INT NOT NULL AUTO_INCREMENT,
                Description VARCHAR(150) NULL,
                PRIMARY KEY (QuoteSectionID)
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
        ");

        var migrationPath = Path.Combine(AppContext.BaseDirectory, "Migrations", "20260911_add_order_occurrences.sql");
        var migrationSql = await File.ReadAllTextAsync(migrationPath);
        await connection.ExecuteAsync(migrationSql);
        await connection.ExecuteAsync(migrationSql);
    }
}