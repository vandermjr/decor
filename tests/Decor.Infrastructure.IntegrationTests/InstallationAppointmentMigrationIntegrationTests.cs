using Dapper;
using MySqlConnector;

namespace Decor.Infrastructure.IntegrationTests;

public sealed class InstallationAppointmentMigrationIntegrationTests(MariaDbFixture fixture) : IClassFixture<MariaDbFixture>
{
    [Fact]
    public async Task InstallationAppointmentMigration_CreatesSchemaAndAdministratorPermissions()
    {
        await using var connection = new MySqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        await connection.ExecuteAsync("CREATE TABLE IF NOT EXISTS employees (EmployeeID INT NOT NULL AUTO_INCREMENT, Name VARCHAR(100) NOT NULL, PRIMARY KEY (EmployeeID)) ENGINE=InnoDB");
        await connection.ExecuteAsync("CREATE TABLE IF NOT EXISTS partners (PartnerID INT NOT NULL AUTO_INCREMENT, Name VARCHAR(100) NOT NULL, PRIMARY KEY (PartnerID)) ENGINE=InnoDB");
        await connection.ExecuteAsync("CREATE TABLE IF NOT EXISTS products (ProductID INT NOT NULL AUTO_INCREMENT, ProductType TINYINT NOT NULL, PRIMARY KEY (ProductID)) ENGINE=InnoDB");
        await connection.ExecuteAsync("CREATE TABLE IF NOT EXISTS order_items (OrderItemID INT NOT NULL AUTO_INCREMENT, ProductID INT NOT NULL, PRIMARY KEY (OrderItemID), CONSTRAINT FK_test_order_items_products FOREIGN KEY (ProductID) REFERENCES products(ProductID)) ENGINE=InnoDB");
        await connection.ExecuteAsync("INSERT IGNORE INTO employees (EmployeeID, Name) VALUES (1, 'Teste')");
        await connection.ExecuteAsync("INSERT IGNORE INTO partners (PartnerID, Name) VALUES (1, 'Teste')");

        var migration = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Migrations", "20260912_add_installation_appointments.sql"));
        await connection.ExecuteAsync(migration);

        (await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name IN ('installation_appointments', 'appointment_reschedules')")).Should().Be(2);
        (await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM permissions WHERE PermissionCode LIKE 'InstallationAppointments.%'")).Should().Be(4);
        (await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM role_permissions rp INNER JOIN roles r ON r.RoleID = rp.RoleID INNER JOIN permissions p ON p.PermissionID = rp.PermissionID WHERE r.RoleName = 'Administrador' AND p.PermissionCode LIKE 'InstallationAppointments.%'")).Should().Be(4);
    }
}