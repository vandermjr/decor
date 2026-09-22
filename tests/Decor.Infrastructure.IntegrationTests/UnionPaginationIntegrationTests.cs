using Dapper;
using Decor.Core.Entities;
using Decor.FluentSqlBuilder;
using Decor.FluentSqlBuilder.Dialects.MariaDB;
using Decor.FluentSqlBuilder.Statements;
using FluentAssertions;
using MySqlConnector;
using Xunit;
using Xunit.Abstractions;

namespace Decor.Infrastructure.IntegrationTests;

public sealed class UnionPaginationIntegrationTests(MariaDbFixture fixture, ITestOutputHelper output)
    : IClassFixture<MariaDbFixture>
{
    [Fact]
    public async Task Union_TakeOnFirstMember_ReportsMariaDbExecution()
    {
        var first = CreateProductSelect().Take(10);
        var second = CreateProductSelect();
        var (sql, _) = first.Union(second).BuildInline();

        var ids = await ExecuteAndReportAsync("A - Take no primeiro membro", sql);
        ids.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Union_SkipOnFirstMember_ReportsMariaDbExecution()
    {
        var first = CreateProductSelect().Skip(5);
        var second = CreateProductSelect();
        var (sql, _) = first.Union(second).BuildInline();

        var ids = await ExecuteAndReportAsync("B - Skip no primeiro membro", sql);
        ids.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Union_TakeAndSkipOnFirstMember_ReportsMariaDbExecution()
    {
        var first = CreateProductSelect().Take(10).Skip(5);
        var second = CreateProductSelect();
        var (sql, _) = first.Union(second).BuildInline();

        var ids = await ExecuteAndReportAsync("C - Take + Skip no primeiro membro", sql);
        ids.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Union_PaginationOnBothMembers_ReportsMariaDbExecution()
    {
        var first = CreateProductSelect().Take(10);
        var second = CreateProductSelect().Take(20);
        var (sql, _) = first.Union(second).BuildInline();

        var ids = await ExecuteAndReportAsync("D - Paginação nos dois membros", sql);
        ids.Should().NotBeEmpty();
    }

    [Fact]
    public async Task FromSubquery_WithUnionAndTakeOnFirstMember_ReportsMariaDbExecution()
    {
        var first = CreateProductSelect().Take(10);
        var second = CreateProductSelect();
        var union = first.Union(second);
        var (sql, _) = CreateBuilder()
            .Select<Product>(s => s
                .Column("paged.ProductID")
                .FromSubquery(union, "paged"))
            .Build();

        var ids = await ExecuteAndReportAsync("E - UNION paginado em FromSubquery", sql);
        ids.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Union_ParenthesizedMembers_ReportsAcceptedPaginationSyntax()
    {
        await EnsureProductsAsync();

        const string sql = "(SELECT p.ProductID FROM products AS p LIMIT 10)\nUNION\n(SELECT p.ProductID FROM products AS p)";
        var ids = await ExecuteAndReportAsync("Controle - membros parenthesizados", sql);
        ids.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Union_ParenthesizedPaginatedMembers_ReportsAcceptedPaginationSyntax()
    {
        await EnsureProductsAsync();

        const string sql = "(SELECT p.ProductID FROM products AS p LIMIT 10)\nUNION\n(SELECT p.ProductID FROM products AS p LIMIT 20)";
        var ids = await ExecuteAndReportAsync("Controle - ambos os membros parenthesizados e paginados", sql);
        ids.Should().NotBeEmpty();
    }

    [Fact]
    public async Task FromSubquery_WithParenthesizedUnion_ReportsAcceptedDerivedTableSyntax()
    {
        await EnsureProductsAsync();

        const string sql = @"SELECT paged.ProductID
FROM (
    (SELECT p.ProductID FROM products AS p LIMIT 10)
    UNION
    (SELECT p.ProductID FROM products AS p)
) AS paged;";
        var ids = await ExecuteAndReportAsync("Controle - UNION parenthesizado em FromSubquery", sql);
        ids.Should().NotBeEmpty();
    }

    private SelectBuilder<Product> CreateProductSelect()
    {
        return CreateBuilder()
            .Select<Product>(s => s.WithColumns<Product>(p => p.ProductID));
    }

    private static FluentCommandBuilder CreateBuilder() => FluentCommandBuilder.Create(new MariaDBDialect());

    private async Task<int[]> ExecuteAndReportAsync(string scenario, string sql)
    {
        await EnsureProductsAsync();
        output.WriteLine($"[{scenario}] SQL gerado/executado:");
        output.WriteLine(sql);

        await using var connection = new MySqlConnection(fixture.ConnectionString);
        try
        {
            var ids = (await connection.QueryAsync<int>(sql)).ToArray();
            output.WriteLine($"[{scenario}] SUCESSO: {ids.Length} linhas");
            output.WriteLine($"[{scenario}] IDs: {string.Join(", ", ids)}");
            return ids;
        }
        catch (MySqlException exception)
        {
            output.WriteLine($"[{scenario}] ERRO MariaDB: {exception.GetType().FullName}");
            output.WriteLine($"[{scenario}] Number: {exception.Number}");
            output.WriteLine($"[{scenario}] Message: {exception.Message}");
            throw;
        }
    }

    private async Task EnsureProductsAsync()
    {
        await using var connection = new MySqlConnection(fixture.ConnectionString);
        await connection.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS products (
                ProductID INT NOT NULL AUTO_INCREMENT,
                Description VARCHAR(255) NULL,
                PRIMARY KEY (ProductID)
            ) ENGINE=InnoDB;
            DELETE FROM products;
            INSERT INTO products (Description)
            SELECT CONCAT('UNION pagination product ', sequence_number)
            FROM (
                SELECT 1 AS sequence_number UNION ALL SELECT 2 UNION ALL SELECT 3 UNION ALL SELECT 4 UNION ALL SELECT 5
                UNION ALL SELECT 6 UNION ALL SELECT 7 UNION ALL SELECT 8 UNION ALL SELECT 9 UNION ALL SELECT 10
                UNION ALL SELECT 11 UNION ALL SELECT 12 UNION ALL SELECT 13 UNION ALL SELECT 14 UNION ALL SELECT 15
                UNION ALL SELECT 16 UNION ALL SELECT 17 UNION ALL SELECT 18 UNION ALL SELECT 19 UNION ALL SELECT 20
            ) AS seed;");
    }
}
