using System.Security.Cryptography;
using System.Data;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Dapper;
using Decor.Application.Services;
using Decor.Core.DTOs;
using Decor.Core.Validation;
using MySqlConnector;

[assembly: InternalsVisibleTo("Decor.TestDataSeed.Tests")]

namespace Decor.TestDataSeed;

internal static class Program
{
    private const string Marker = "Decor fictitious seed v1";
    private static readonly string[] People = ["Ana Ficticia", "Bruno Ficticio", "Carla Ficticia", "Diego Ficticio", "Elisa Ficticia"];
    private static readonly string[] Customers = ["Casa Aurora", "Casa Horizonte", "Casa Primavera", "Casa Jardim", "Casa Estrela"];
    private static readonly string[] Suppliers = ["Tecidos Aurora", "Trilhos Horizonte", "Persianas Primavera", "Acessorios Jardim", "Ferragens Estrela"];
    private sealed record TaskRole(string Task, string[] Permissions)
    {
        public string Name => "Teste DSEED1 " + Task;
        public string Username => "devseed." + Task.ToLowerInvariant();
    }
    private static readonly TaskRole[] Roles =
    [
        new("Cadastros", ["Customers.View", "Customers.Create", "Customers.Edit", "Suppliers.View", "Suppliers.Create", "Suppliers.Edit", "Employees.View", "Employees.Create", "Employees.Edit"]),
        new("Comercial", ["Customers.View", "Products.View", "Services.View", "Employees.View", "Quotes.View", "Quotes.Create", "Quotes.Edit", "Quotes.Send", "Quotes.Approve", "Orders.View", "Orders.ConvertFromQuote", "PaymentMethods.View", "UnitsOfMeasure.View"]),
        new("Compras", ["Suppliers.View", "Products.View", "StockLocations.View", "UnitsOfMeasure.View", "PurchaseOrders.View", "PurchaseOrders.Create", "PurchaseOrders.Edit", "PurchaseOrderItems.View", "PurchaseOrderItems.Create", "PurchaseOrderItems.Edit", "GoodsReceipts.View", "GoodsReceipts.Register"]),
        new("Estoque", ["Products.View", "UnitsOfMeasure.View", "StockLocations.View", "StockMovements.View", "StockMovements.Entry", "StockMovements.Exit", "StockMovements.Transfer", "StockReservations.View"]),
        new("Financeiro", ["Customers.View", "Suppliers.View", "Orders.View", "PurchaseOrders.View", "PaymentMethods.View", "CashAccounts.View", "CashTransactions.View", "CashTransactions.Create", "OrderInstallments.View", "OrderInstallments.RegisterPayment", "PurchaseOrderInstallments.View", "PurchaseOrderInstallments.RegisterPayment", "AccountsPayable.View", "AccountsPayable.Create", "AccountsPayable.RegisterPayment"])
    ];
    internal static string NewPassword() => "Aa2!" + Convert.ToHexString(RandomNumberGenerator.GetBytes(24));

    private static async Task<int> Main(string[] args)
    {
        var committed = false;
        try
        {
            ValidatePlan();
            if (args.SequenceEqual(new[] { "--self-test" }))
            {
                SelfTest();
                return 0;
            }
            var options = Parse(args);
            var builder = ReadConfiguration(options["--config"]);
            var local = IsLoopback(builder);
            Console.WriteLine($"Configured destination: {(local ? "loopback" : "NONLOCAL/BLOCKED")}; schema={builder.Database}; environment not inferred from hostname.");
            if (options.ContainsKey("--inspect-config")) return 0;
            Require(local, "Remote destinations are never contacted by this tool.");
            Require(options["--expected-database"].Equals(builder.Database, StringComparison.Ordinal), "Expected database does not match configuration.");
            Require(!IsProductionName(builder.Database), "Production-like schema name is blocked.");
            Require(options.ContainsKey("--confirm-local-development"), "Explicit local development confirmation is required even for database dry-run.");
            builder.Pooling = false;
            await using var connection = new MySqlConnection(builder.ConnectionString);
            await connection.OpenAsync();
            var tables = new[] { "customers", "suppliers", "employees", "users", "roles", "permissions", "user_roles", "role_permissions" };
            var engines = (await connection.QueryAsync<string>("SELECT ENGINE FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME IN @tables", new { tables })).ToArray();
            Require(engines.Length == tables.Length && engines.All(engine => engine.Equals("InnoDB", StringComparison.OrdinalIgnoreCase)), "Required transactional schema is missing or not InnoDB. No migrations will run.");
            var locked = await connection.ExecuteScalarAsync<int?>("SELECT GET_LOCK('Decor.TestDataSeed.v1', 0)");
            Require(locked == 1, "Another seed is running; no changes made.");
            try
            {
                await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable);
                var apply = options.ContainsKey("--apply");
                await Seed(connection, transaction, apply);
                if (apply)
                {
                    await transaction.CommitAsync();
                    committed = true;
                }
                else await transaction.RollbackAsync();
                Console.WriteLine(apply ? "COMMITTED. Credentials were neither printed nor persisted. Existing records were not modified." : "DRY RUN: zero writes; transaction rolled back. Counts above are existing/planned, not inserted.");
            }
            finally
            {
                await connection.ExecuteScalarAsync<int?>("SELECT RELEASE_LOCK('Decor.TestDataSeed.v1')");
            }
            return 0;
        }
        catch (SeedBlocked exception)
        {
            Console.WriteLine("BLOCKED: " + exception.Message);
            return 2;
        }
        catch (MySqlException exception)
        {
            Console.WriteLine($"BLOCKED: database error code {exception.Number}; {(committed ? "data already committed; cleanup failed" : "no successful commit")}. Check connectivity/schema offline. Details suppressed to protect secrets.");
            return 3;
        }
        catch (Exception)
        {
            Console.WriteLine($"BLOCKED: configuration, schema, validation or cleanup failed; {(committed ? "data already committed" : "no successful commit")}. Details suppressed to protect secrets.");
            return 2;
        }
    }

    private static Dictionary<string, string> Parse(string[] args)
    {
        var options = new Dictionary<string, string>();
        for (var index = 0; index < args.Length; index++)
        {
            var option = args[index];
            Require(new[] { "--config", "--expected-database", "--inspect-config", "--dry-run", "--apply", "--confirm-local-development" }.Contains(option), "Unknown option. Use documented flags, never credentials on the command line.");
            var value = "";
            if (option is "--config" or "--expected-database")
            {
                Require(index + 1 < args.Length && !args[index + 1].StartsWith("--"), "Missing option value.");
                value = args[++index];
            }
            Require(options.TryAdd(option, value), "Duplicate option.");
        }
        Require(options.ContainsKey("--config"), "Supply --config pointing to an existing private appsettings file.");
        Require(options.ContainsKey("--inspect-config") ? !options.ContainsKey("--apply") && !options.ContainsKey("--dry-run") : options.ContainsKey("--apply") != options.ContainsKey("--dry-run"), "Choose exactly one: --inspect-config, --dry-run or --apply.");
        Require(options.ContainsKey("--inspect-config") || options.ContainsKey("--expected-database"), "Supply --expected-database.");
        return options;
    }

    private static MySqlConnectionStringBuilder ReadConfiguration(string path)
    {
        using var json = JsonDocument.Parse(File.ReadAllText(path));
        var value = json.RootElement.GetProperty("ConnectionStrings").GetProperty("MariaDBConnector").GetString();
        value = Environment.GetEnvironmentVariable("ConnectionStrings__MariaDBConnector") ?? value;
        var builder = new MySqlConnectionStringBuilder(value ?? throw new SeedBlocked("Missing connection configuration."));
        Require(!string.IsNullOrWhiteSpace(builder.Database) && builder.Database.All(character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-'), "Missing or unsupported schema name.");
        return builder;
    }

    private static bool IsLoopback(MySqlConnectionStringBuilder builder) =>
        builder.ConnectionProtocol == MySqlConnectionProtocol.Tcp &&
        new[] { "127.0.0.1", "localhost", "::1" }.Contains(builder.Server, StringComparer.OrdinalIgnoreCase);

    private static bool IsProductionName(string name) =>
        new[] { "prod", "live", "production" }.Any(token => name.Contains(token, StringComparison.OrdinalIgnoreCase));

    private static string Identity(string name) => "[FICTICIO DSEED1] " + name;
    private static string Document(string kind, int index) => $"DSEED1-{kind}{index + 1:00}";
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new SeedBlocked(message);
    }
    private sealed class SeedBlocked(string message) : Exception(message);

    private static void ValidatePlan()
    {
        var catalog = typeof(Decor.Core.Common.DecorPermissions).GetFields().Select(field => (string)field.GetRawConstantValue()!).ToHashSet();
        Require(Roles.Length == 5 && Roles.Select(role => role.Username).Distinct().Count() == 5, "Invalid seed identities.");
        foreach (var role in Roles)
            Require(role.Permissions.Distinct().Count() == role.Permissions.Length && role.Permissions.All(permission => catalog.Contains(permission) && !permission.StartsWith("Users.") && !permission.StartsWith("Roles.") && !permission.StartsWith("DatabaseMaintenance.")), "Invalid task permissions.");
        for (var index = 0; index < 5; index++)
        {
            Require(!new EmployeeDTOValidator().Validate(new EmployeeDTO(0, Identity(People[index]), Roles[index].Task, null, null, Document("E", index), null, true, null)).Any(), "Employee DTO validation failed.");
            Require(!new CustomerDTOValidator().Validate(new CustomerDTO(0, Identity(Customers[index]), Document("C", index), null, $"cliente{index + 1}@example.invalid", null, true)).Any(), "Customer DTO validation failed.");
            Require(!new SupplierDTOValidator().Validate(new SupplierDTO(0, Identity(Suppliers[index]), Document("S", index), null, $"fornecedor{index + 1}@example.invalid", true)).Any(), "Supplier DTO validation failed.");
        }
    }

    private static void SelfTest()
    {
        var password = NewPassword();
        var hasher = new Pbkdf2PasswordHasher();
        var hash = hasher.Hash(password);
        Require(new PasswordPolicy().IsValid(password) && hasher.Verify(password, hash) && !hasher.Verify("wrong", hash), "Password service check failed.");
        Require(IsLoopback(new MySqlConnectionStringBuilder { Server = "127.0.0.1" }) && !IsLoopback(new MySqlConnectionStringBuilder { Server = "db.example.invalid" }) && !IsLoopback(new MySqlConnectionStringBuilder { Server = "127.0.0.1,db.example.invalid" }) && IsProductionName("decor_production"), "Destination guard failed.");
        foreach (var invalid in new[] { Array.Empty<string>(), new[] { "--config", "unused", "--apply", "--dry-run" }, new[] { "--config", "unused", "--apply" }, new[] { "--config", "unused", "--inspect-config", "--apply" } })
        {
            var rejected = false;
            try { Parse(invalid); }
            catch (SeedBlocked) { rejected = true; }
            Require(rejected, "Unsafe options accepted.");
        }
        Console.WriteLine("PASS: 15 master-data DTOs and 5 user identities, 5 task roles, permission catalog, password policy/hash, destination and option guards. No database accessed.");
        foreach (var role in Roles) Console.WriteLine($"PLAN: {role.Username} -> {role.Name}: {string.Join(", ", role.Permissions)}");
    }

    internal static async Task Seed(MySqlConnection connection, MySqlTransaction transaction, bool apply)
    {
        var permissionIds = (await connection.QueryAsync<(int PermissionID, string PermissionCode)>("SELECT PermissionID, PermissionCode FROM permissions", transaction: transaction)).ToDictionary(item => item.PermissionCode, item => item.PermissionID, StringComparer.OrdinalIgnoreCase);
        var missing = Roles.SelectMany(role => role.Permissions).Distinct().Where(permission => !permissionIds.ContainsKey(permission)).ToArray();
        foreach (var permission in missing.Where(permission => permission == "Services.View"))
            Console.WriteLine($"OMITTED: {permission} from Comercial; optional newly separated module absent from catalog. No permission created and no migration run.");
        var requiredMissing = missing.Where(permission => permission != "Services.View").ToArray();
        Require(requiredMissing.Length == 0, "Missing catalog permissions: " + string.Join(", ", requiredMissing) + ". No migrations will run.");
        var created = new Dictionary<string, int> { ["roles"] = 0, ["users"] = 0, ["employees"] = 0, ["customers"] = 0, ["suppliers"] = 0, ["role_permissions"] = 0, ["user_roles"] = 0 };
        var existing = new Dictionary<string, int>(created);
        for (var index = 0; index < Roles.Length; index++)
        {
            var role = Roles[index];
            var availablePermissions = role.Permissions.Where(permission => permissionIds.ContainsKey(permission)).ToArray();
            var foundRoles = (await connection.QueryAsync<(int RoleID, string? Description, bool IsSystemProtected)>("SELECT RoleID, Description, IsSystemProtected FROM roles WHERE RoleName=@Name FOR UPDATE", new { role.Name }, transaction)).ToArray();
            Require(foundRoles.Length <= 1 && foundRoles.All(item => item.Description == Marker && !item.IsSystemProtected), "Role identity collision: " + role.Name);
            var roleId = foundRoles.FirstOrDefault().RoleID;
            if (roleId == 0)
            {
                created["roles"]++;
                created["role_permissions"] += availablePermissions.Length;
                if (apply)
                {
                    roleId = await connection.ExecuteScalarAsync<int>("INSERT INTO roles (RoleName, Description, HierarchyLevel, IsSystemProtected) VALUES (@Name, @Marker, 10, 0); SELECT LAST_INSERT_ID();", new { role.Name, Marker }, transaction);
                    foreach (var permission in availablePermissions)
                        await connection.ExecuteAsync("INSERT INTO role_permissions (RoleID, PermissionID) VALUES (@roleId, @permissionId)", new { roleId, permissionId = permissionIds[permission] }, transaction);
                }
            }
            else existing["roles"]++;
            var foundUsers = (await connection.QueryAsync<(int UserID, string DisplayName)>("SELECT UserID, DisplayName FROM users WHERE Username=@Username FOR UPDATE", new { role.Username }, transaction)).ToArray();
            Require(foundUsers.Length <= 1 && foundUsers.All(item => item.DisplayName == Marker + ":" + role.Username), "User identity collision: " + role.Username);
            var userId = foundUsers.FirstOrDefault().UserID;
            if (userId == 0)
            {
                created["users"]++;
                if (foundRoles.Length == 0) created["user_roles"]++;
                if (apply)
                {
                    var password = NewPassword();
                    Require(new PasswordPolicy().IsValid(password), "Password policy failed.");
                    userId = await connection.ExecuteScalarAsync<int>("INSERT INTO users (Username, DisplayName, PasswordHash, IsActive, MustChangePassword) VALUES (@Username, @displayName, @hash, 1, 1); SELECT LAST_INSERT_ID();", new { role.Username, displayName = Marker + ":" + role.Username, hash = new Pbkdf2PasswordHasher().Hash(password) }, transaction);
                    if (foundRoles.Length == 0)
                        await connection.ExecuteAsync("INSERT INTO user_roles (UserID, RoleID) VALUES (@userId, @roleId)", new { userId, roleId }, transaction);
                }
            }
            else existing["users"]++;
            Require(foundUsers.Length == 0 || foundRoles.Length != 0, "Existing user with missing role: refusing to change its associations.");
            Require(foundUsers.Length != 0 || foundRoles.Length == 0, "Existing role with missing user: refusing to attach a new user to existing permissions.");
            if (foundUsers.Length != 0)
            {
                var associated = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM user_roles WHERE UserID=@userId AND RoleID=@roleId", new { userId, roleId }, transaction);
                Require(associated == 1, "Existing seed association was changed; no restoration performed: " + role.Username);
                existing["user_roles"]++;
                var actual = (await connection.QueryAsync<string>("SELECT p.PermissionCode FROM permissions p JOIN role_permissions rp ON rp.PermissionID=p.PermissionID WHERE rp.RoleID=@roleId ORDER BY p.PermissionCode", new { roleId }, transaction)).ToArray();
                existing["role_permissions"] += actual.Length;
                Console.WriteLine($"EXISTING: {role.Username} -> {role.Name}; stored role permissions: {string.Join(", ", actual)}; no restoration of revocations.");
            }
            else Console.WriteLine($"{(apply ? "NEW" : "PLANNED")}: {role.Username} -> {role.Name}: {string.Join(", ", availablePermissions)}");
            await Master("employees", "EmployeeID", "Name", Identity(People[index]), Document("E", index), userId);
            await Master("customers", "CustomerID", "Name", Identity(Customers[index]), Document("C", index), null);
            await Master("suppliers", "SupplierID", "CorporateName", Identity(Suppliers[index]), Document("S", index), null);
        }
        if (apply)
        {
            foreach (var table in new[] { "employees", "customers", "suppliers" })
                Require(await connection.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM {table} WHERE Document IN @documents", new { documents = Enumerable.Range(0, 5).Select(index => Document(table == "employees" ? "E" : table == "customers" ? "C" : "S", index)).ToArray() }, transaction) == 5, "Final count mismatch: " + table);
            Require(await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM users WHERE Username IN @names", new { names = Roles.Select(role => role.Username).ToArray() }, transaction) == 5, "Final user count mismatch.");
        }
        foreach (var table in created.Keys) Console.WriteLine($"COUNT {table}: existing={existing[table]}, {(apply ? "inserted-pending-commit" : "planned")}={created[table]}, total={existing[table] + created[table]}");

        async Task Master(string table, string key, string nameColumn, string name, string document, int? userId)
        {
            var rows = (await connection.QueryAsync<(int ID, string Name, string? Document)>($"SELECT {key} AS ID, {nameColumn} AS Name, Document FROM {table} WHERE Document=@document OR {nameColumn}=@name FOR UPDATE", new { document, name }, transaction)).ToArray();
            Require(rows.Length <= 1 && rows.All(row => row.Name == name && row.Document == document), "Master data identity collision: " + name);
            if (rows.Length == 1)
            {
                if (table == "employees")
                    Require(await connection.ExecuteScalarAsync<int?>("SELECT UserID FROM employees WHERE EmployeeID=@id", new { id = rows[0].ID }, transaction) == userId, "Employee user association was changed; no restoration performed.");
                existing[table]++;
            }
            else
            {
                created[table]++;
                if (apply)
                {
                    var extraColumn = table == "employees" ? ", UserID" : ", Email";
                    var extraValue = table == "employees" ? ", @userId" : ", @email";
                    await connection.ExecuteAsync($"INSERT INTO {table} ({nameColumn}, Document, IsActive{extraColumn}) VALUES (@name, @document, 1{extraValue})", new { name, document, userId, email = document.ToLowerInvariant() + "@example.invalid" }, transaction);
                }
            }
            Console.WriteLine($"IDENTITY {table}: {name}; {(rows.Length == 1 ? "existing-preserved" : apply ? "new-pending-commit" : "planned")}");
        }
    }
}