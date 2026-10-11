using System.Data;
using System.Security.Cryptography;
using Dapper;
using Decor.Application.Services;
using Decor.Core.Common;
using MySqlConnector;
using Testcontainers.MariaDb;
using Xunit;

namespace Decor.TestDataSeed.Tests;

public sealed class SeedTests
{
    [Fact]
    public Task Seed_is_transactional_idempotent_and_preserves_existing_security() => VerifySeed(true);

    [Fact]
    public Task Absent_optional_services_preserves_core_commercial_profile() => VerifySeed(false);

    private static async Task VerifySeed(bool servicesInstalled)
    {
        await using var container = new MariaDbBuilder("mariadb:10.11.6")
            .WithDatabase("decor_seed_test")
            .WithPassword(Convert.ToHexString(RandomNumberGenerator.GetBytes(24)))
            .Build();
        await container.StartAsync();
        await using var connection = new MySqlConnection(container.GetConnectionString());
        await connection.OpenAsync();
        await connection.ExecuteAsync("""
            CREATE TABLE users (UserID INT AUTO_INCREMENT PRIMARY KEY, Username VARCHAR(50) UNIQUE NOT NULL,
                DisplayName VARCHAR(100) NOT NULL, PasswordHash VARCHAR(255) NOT NULL,
                IsActive BOOL NOT NULL, MustChangePassword BOOL NOT NULL) ENGINE=InnoDB;
            CREATE TABLE roles (RoleID INT AUTO_INCREMENT PRIMARY KEY, RoleName VARCHAR(50) UNIQUE NOT NULL,
                Description VARCHAR(255), HierarchyLevel INT NOT NULL, IsSystemProtected BOOL NOT NULL) ENGINE=InnoDB;
            CREATE TABLE permissions (PermissionID INT AUTO_INCREMENT PRIMARY KEY, PermissionCode VARCHAR(100) UNIQUE NOT NULL) ENGINE=InnoDB;
            CREATE TABLE role_permissions (RoleID INT NOT NULL, PermissionID INT NOT NULL, PRIMARY KEY(RoleID, PermissionID),
                FOREIGN KEY(RoleID) REFERENCES roles(RoleID), FOREIGN KEY(PermissionID) REFERENCES permissions(PermissionID)) ENGINE=InnoDB;
            CREATE TABLE user_roles (UserID INT NOT NULL, RoleID INT NOT NULL, PRIMARY KEY(UserID, RoleID),
                FOREIGN KEY(UserID) REFERENCES users(UserID), FOREIGN KEY(RoleID) REFERENCES roles(RoleID)) ENGINE=InnoDB;
            CREATE TABLE employees (EmployeeID INT AUTO_INCREMENT PRIMARY KEY, Name VARCHAR(150) NOT NULL,
                Document VARCHAR(20), IsActive BOOL NOT NULL, UserID INT, FOREIGN KEY(UserID) REFERENCES users(UserID)) ENGINE=InnoDB;
            CREATE TABLE customers (CustomerID INT AUTO_INCREMENT PRIMARY KEY, Name VARCHAR(150) NOT NULL,
                Document VARCHAR(20), IsActive BOOL NOT NULL, Email VARCHAR(150)) ENGINE=InnoDB;
            CREATE TABLE suppliers (SupplierID INT AUTO_INCREMENT PRIMARY KEY, CorporateName VARCHAR(150) NOT NULL,
                Document VARCHAR(20), IsActive BOOL NOT NULL, Email VARCHAR(150)) ENGINE=InnoDB;
            """);
        var adminHash = new Pbkdf2PasswordHasher().Hash(Convert.ToHexString(RandomNumberGenerator.GetBytes(24)));
        await connection.ExecuteAsync("""
            INSERT INTO users VALUES (1, 'admin', 'Real administrator', @adminHash, 1, 0);
            INSERT INTO roles VALUES (1, 'Administradores', 'Existing protected group', 100, 1);
            INSERT INTO user_roles VALUES (1, 1);
            """, new { adminHash });
        foreach (var field in typeof(DecorPermissions).GetFields())
            await connection.ExecuteAsync("INSERT INTO permissions (PermissionCode) VALUES (@code)", new { code = (string)field.GetRawConstantValue()! });
        if (!servicesInstalled)
            await connection.ExecuteAsync("DELETE FROM permissions WHERE PermissionCode='Services.View'");
        await connection.ExecuteAsync("INSERT INTO role_permissions SELECT 1, PermissionID FROM permissions WHERE PermissionCode='Users.View'");
        var adminPermission = await connection.ExecuteScalarAsync<int>("SELECT PermissionID FROM role_permissions WHERE RoleID=1");

        await Apply(false);
        Assert.Equal(1, await Count("users"));
        Assert.Equal(0, await Count("employees"));
        Assert.Equal(1, await Count("roles"));

        await connection.ExecuteAsync("INSERT INTO suppliers (CorporateName, Document, IsActive) VALUES ('[FICTICIO DSEED1] Ferragens Estrela', 'REAL-COLLISION', 1)");
        await Assert.ThrowsAnyAsync<Exception>(() => Apply(true));
        Assert.Equal(1, await Count("users"));
        Assert.Equal(1, await Count("roles"));
        Assert.Equal(0, await Count("employees"));
        Assert.Equal(0, await Count("customers"));
        Assert.Equal(1, await Count("suppliers"));
        await connection.ExecuteAsync("DELETE FROM suppliers WHERE Document='REAL-COLLISION'");

        await connection.ExecuteAsync("DELETE FROM permissions WHERE PermissionCode='Products.View'");
        var blocked = await Assert.ThrowsAnyAsync<Exception>(() => Apply(true));
        Assert.Contains("Missing catalog permissions: Products.View", blocked.Message);
        Assert.Equal(1, await Count("users"));
        await connection.ExecuteAsync("INSERT INTO permissions (PermissionCode) VALUES ('Products.View')");

        await Apply(true);
        Assert.Equal(6, await Count("users"));
        Assert.Equal(6, await Count("roles"));
        Assert.Equal(5, await Count("employees"));
        Assert.Equal(5, await Count("customers"));
        Assert.Equal(5, await Count("suppliers"));
        Assert.Equal(6, await Count("user_roles"));
        Assert.Equal(servicesInstalled ? 58 : 57, await Count("role_permissions"));
        var commercialPermissions = (await connection.QueryAsync<string>("SELECT p.PermissionCode FROM permissions p JOIN role_permissions rp ON rp.PermissionID=p.PermissionID JOIN roles r ON r.RoleID=rp.RoleID WHERE r.RoleName='Teste DSEED1 Comercial' ORDER BY p.PermissionCode")).ToArray();
        var expectedCommercial = new[] { "Customers.View", "Products.View", "Employees.View", "Quotes.View", "Quotes.Create", "Quotes.Edit", "Quotes.Send", "Quotes.Approve", "Orders.View", "Orders.ConvertFromQuote", "PaymentMethods.View", "UnitsOfMeasure.View" };
        Assert.Equal(expectedCommercial.Concat(servicesInstalled ? new[] { "Services.View" } : Array.Empty<string>()).OrderBy(permission => permission), commercialPermissions);
        Assert.Equal(servicesInstalled ? 1 : 0, await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM permissions WHERE PermissionCode='Services.View'"));
        Assert.Equal(5, await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM employees e JOIN users u ON u.UserID=e.UserID WHERE u.Username LIKE 'devseed.%' AND u.MustChangePassword=1"));
        var hashes = (await connection.QueryAsync<string>("SELECT PasswordHash FROM users WHERE Username LIKE 'devseed.%' ORDER BY Username")).ToArray();
        Assert.Equal(5, hashes.Distinct().Count());
        Assert.All(hashes, hash => Assert.StartsWith("PBKDF2-SHA256$", hash));

        await connection.ExecuteAsync("DELETE rp FROM role_permissions rp JOIN roles r ON r.RoleID=rp.RoleID JOIN permissions p ON p.PermissionID=rp.PermissionID WHERE r.RoleName='Teste DSEED1 Cadastros' AND p.PermissionCode='Customers.Create'");
        await connection.ExecuteAsync("UPDATE users SET IsActive=0 WHERE Username='devseed.estoque'; UPDATE employees SET IsActive=0 WHERE Document='DSEED1-E04';");
        await Apply(true);
        await Apply(false);
        Assert.Equal(6, await Count("users"));
        Assert.Equal(6, await Count("roles"));
        Assert.Equal(5, await Count("employees"));
        Assert.Equal(5, await Count("customers"));
        Assert.Equal(5, await Count("suppliers"));
        Assert.Equal(servicesInstalled ? 57 : 56, await Count("role_permissions"));
        Assert.Equal(0, await connection.ExecuteScalarAsync<int>("SELECT IsActive FROM users WHERE Username='devseed.estoque'"));
        Assert.Equal(0, await connection.ExecuteScalarAsync<int>("SELECT IsActive FROM employees WHERE Document='DSEED1-E04'"));
        Assert.Equal(hashes, (await connection.QueryAsync<string>("SELECT PasswordHash FROM users WHERE Username LIKE 'devseed.%' ORDER BY Username")).ToArray());
        Assert.Equal(adminHash, await connection.ExecuteScalarAsync<string>("SELECT PasswordHash FROM users WHERE UserID=1"));
        Assert.Equal("Real administrator", await connection.ExecuteScalarAsync<string>("SELECT DisplayName FROM users WHERE UserID=1"));
        Assert.Equal(adminPermission, await connection.ExecuteScalarAsync<int>("SELECT PermissionID FROM role_permissions WHERE RoleID=1"));
        Assert.Equal(1, await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM user_roles WHERE UserID=1 AND RoleID=1"));
        Assert.Equal(100, await connection.ExecuteScalarAsync<int>("SELECT HierarchyLevel FROM roles WHERE RoleID=1"));

        async Task<int> Count(string table) => await connection.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM {table}");
        async Task Apply(bool apply)
        {
            await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable);
            await Program.Seed(connection, transaction, apply);
            if (apply) await transaction.CommitAsync();
            else await transaction.RollbackAsync();
        }
    }
}