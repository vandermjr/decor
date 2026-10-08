using Decor.Core.Entities;
using Decor.Core.Interfaces.Data;
using Decor.FluentSqlBuilder;
using Decor.Infrastructure.Data.Repositories;
using Moq;

namespace Decor.Application.Tests;

public sealed class ProductRepositorySearchTests
{
    [Theory]
    [InlineData("sem estoque", "=")]
    [InlineData("estoque = 0", "=")]
    [InlineData("com estoque negativo", "<")]
    [InlineData("estoque < 0", "<")]
    public async Task ZeroAndNegativeStockUseParameterizedComparisons(string phrase, string comparison)
    {
        foreach (var synchronous in new[] { false, true })
        {
            var (sql, parameters) = await BuildQuery("piso " + phrase, synchronous);
            Assert.Contains($"p.StockQuantity {comparison} @StockQuantity", sql);
            Assert.Equal(0m, Assert.IsType<decimal>(parameters["StockQuantity"]));
            Assert.Equal(4, parameters.Values.Count(value => Equals(value, "piso")));
            Assert.DoesNotContain("estoque", parameters.Values);
        }
    }

    [Fact]
    public async Task EachTokenUsesOrAcrossFieldsAndAndBetweenTokens()
    {
        var (sql, parameters) = await BuildQuery("piso arquitech");
        Assert.Matches(@"(?i)LEFT(?: OUTER)? JOIN\s+`?brands`?", sql);
        Assert.Contains("p.IsActive = @IsActive AND (", sql);
        Assert.Contains(") AND (", sql);
        foreach (var column in new[] { "p.Description", "b.BrandName", "p.Barcode", "p.ManufacturerRef" })
            Assert.Equal(2, sql.Split(column + " LIKE").Length - 1);
        Assert.Equal(4, parameters.Values.Count(value => Equals(value, "piso")));
        Assert.Equal(4, parameters.Values.Count(value => Equals(value, "arquitech")));
        Assert.DoesNotContain("MATCH(", sql);
    }

    [Theory]
    [InlineData("tipo:produto", ProductType.Good)]
    [InlineData("tipo:servico", ProductType.Service)]
    public async Task TypeAndDecimalStockAreAppliedBeforePagination(string typePhrase, ProductType type)
    {
        var (sql, parameters) = await BuildQuery("piso estoque acima de 1,25 " + typePhrase);
        Assert.Contains("p.ProductType = @ProductType", sql);
        Assert.Contains("p.StockQuantity > @StockQuantity", sql);
        Assert.Equal(type, parameters["ProductType"]);
        Assert.Equal(1.25m, Assert.IsType<decimal>(parameters["StockQuantity"]));
        Assert.True(sql.IndexOf("p.ProductType =", StringComparison.Ordinal) < sql.IndexOf("LIMIT", StringComparison.Ordinal));
        Assert.Contains("LIMIT 5", sql);
        Assert.Contains("OFFSET 5", sql);
    }

    [Theory]
    [InlineData("42")]
    [InlineData("00042")]
    [InlineData("00042 tipo:produto")]
    [InlineData("tipo:servico 42")]
    public async Task NumericSearchMatchesOnlyExactId(string search)
    {
        var (sql, parameters) = await BuildQuery(search);
        Assert.Contains("AND p.ProductID = @ProductID", sql);
        Assert.Equal(42, Assert.IsType<int>(parameters["ProductID"]));
        Assert.DoesNotContain(" LIKE", sql);
        Assert.DoesNotContain(" OR ", sql);
        Assert.DoesNotContain("LIMIT 1", sql);
    }

    [Fact]
    public async Task NonnumericBarcodeKeepsMultipleFieldSearch()
    {
        var (sql, parameters) = await BuildQuery("ABC42 tipo:produto");
        Assert.Contains("p.Barcode LIKE", sql);
        Assert.Contains("OR b.BrandName LIKE", sql);
        Assert.DoesNotContain("p.ProductID =", sql);
        Assert.Contains("ABC42", parameters.Values);
    }

    [Fact]
    public async Task OutOfRangeNumericCodeDoesNotFallBackToTextSearch()
    {
        var (sql, parameters) = await BuildQuery("2147483648 tipo:produto");
        Assert.Contains("AND p.ProductID = @ProductID", sql);
        Assert.DoesNotContain(" LIKE", sql);
        Assert.Equal(0, parameters["ProductID"]);
    }

    [Fact]
    public async Task UserTextNeverBecomesSqlSyntax()
    {
        var (sql, parameters) = await BuildQuery("piso' OR 1=1 -- tipo:produto estoque > 0");
        Assert.DoesNotContain("piso'", sql);
        Assert.DoesNotContain("1=1", sql);
        Assert.DoesNotContain("--", sql);
        Assert.Contains("piso'", parameters.Values);
        Assert.Contains("1=1", parameters.Values);
        Assert.Contains("--", parameters.Values);
        Assert.IsType<decimal>(parameters["StockQuantity"]);
    }

    [Fact]
    public async Task SyncAndAsyncUseSameFiltersWithOnlyAsyncPagination()
    {
        var (asyncSql, asyncParameters) = await BuildQuery("piso arquitech com estoque tipo:produto");
        var (syncSql, syncParameters) = await BuildQuery("piso arquitech com estoque tipo:produto", synchronous: true);
        Assert.Equal(asyncParameters, syncParameters);
        Assert.Equal(asyncSql[..asyncSql.IndexOf("LIMIT", StringComparison.Ordinal)].TrimEnd(), syncSql.TrimEnd(';', '\n', '\r', ' '));
        Assert.DoesNotContain("LIMIT", syncSql);
    }

    private static async Task<(string Sql, Dictionary<string, object?> Parameters)> BuildQuery(string search, bool synchronous = false)
    {
        var builder = FluentCommandBuilder.Create();
        var database = new Mock<IDatabaseConnection>(MockBehavior.Strict);
        var stop = new InvalidOperationException("SQL capture: no connection is opened.");
        database.Setup(connection => connection.CreateConnection()).Throws(stop);
        var context = new Mock<IQueryContext>();
        context.SetupProperty(query => query.IsSingleIdSearch, true);
        var repository = new ProductRepository(database.Object, () => context.Object, () => builder);
        var exception = synchronous
            ? Assert.Throws<InvalidOperationException>(() => repository.SearchGetBy(search))
            : await Assert.ThrowsAsync<InvalidOperationException>(() => repository.SearchGetByAsync(search, 2, 5));
        Assert.Same(stop, exception);
        Assert.False(context.Object.IsSingleIdSearch);
        var buildQuery = typeof(ProductRepository).GetMethod("BuildSearchQuery",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        return ((string Sql, Dictionary<string, object?> Parameters))buildQuery.Invoke(repository,
            new object?[] { search, synchronous ? null : 2, synchronous ? null : 5 })!;
    }
}