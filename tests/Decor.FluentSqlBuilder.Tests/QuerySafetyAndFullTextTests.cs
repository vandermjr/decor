using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Decor.Core.Entities;
using FluentAssertions;
using Decor.FluentSqlBuilder;
using Decor.FluentSqlBuilder.Dialects;
using Decor.FluentSqlBuilder.Dialects.MariaDB;

namespace Decor.FluentSqlBuilder.Tests;

public sealed class QuerySafetyAndFullTextTests
{
    [Table("products")]
    private sealed class SearchProduct
    {
        [Key]
        public int ProductID { get; set; }
        public string? Description { get; set; }
        public string? ManufacturerRef { get; set; }
        public bool IsActive { get; set; }
    }

    [Fact]
    public void IsNull_ShouldGenerateIsNullPredicate()
    {
        var (sql, _) = CreateBuilder()
            .Select(select => select.AllColumns<SearchProduct>())
            .From<SearchProduct>()
            .Where(where => where.IsNull((SearchProduct p) => p.Description))
            .Build();

        sql.Should().Contain("WHERE sp.Description IS NULL");
    }

    [Fact]
    public void IsNotNull_ShouldGenerateIsNotNullPredicate()
    {
        var (sql, _) = CreateBuilder()
            .Select(select => select.AllColumns<SearchProduct>())
            .From<SearchProduct>()
            .Where(where => where.IsNotNull((SearchProduct p) => p.Description))
            .Build();

        sql.Should().Contain("WHERE sp.Description IS NOT NULL");
    }

    [Fact]
    public void Update_WithoutWhere_ShouldThrow()
    {
        Action action = () => CreateBuilder()
            .Update().Table<SearchProduct>()
            .Set((SearchProduct p) => p.Description, "novo valor")
            .Build();

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("UPDATE exige uma cláusula WHERE.");
    }

    [Fact]
    public void Delete_WithoutWhere_ShouldThrow()
    {
        Action action = () => CreateBuilder()
            .Delete<SearchProduct>()
            .Build();

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("DELETE exige uma cláusula WHERE.");
    }

    [Fact]
    public void Insert_WithDifferentEntityType_ShouldThrow()
    {
        Action action = () => CreateBuilder()
            .Insert().Into<SearchProduct>()
            .Values(new Brand())
            .Build();

        action.Should().Throw<ArgumentException>()
            .WithMessage("O tipo de Values deve ser o mesmo tipo informado em Into.*");
    }

    [Fact]
    public void Update_WithDuplicateSetColumn_ShouldThrow()
    {
        Action action = () => CreateBuilder()
            .Update().Table<SearchProduct>()
            .Set((SearchProduct p) => p.Description, "primeiro")
            .Set((SearchProduct p) => p.Description, "segundo");

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("A coluna 'Description' já foi definida na cláusula SET.");
    }

    [Fact]
    public void DynamicFullTextSearch_WithId_ShouldUseEqualityAndLimitOne()
    {
        var (sql, parameters) = CreateBuilder()
            .Select(select => select.AllColumns<SearchProduct>())
            .From<SearchProduct>()
            .Where(where => where.WithDynamicFullTextSearch<SearchProduct, SearchProduct>(
                "42",
                p => p.ProductID,
                p => p.Description)
                .OrderByRelevanceDescending())
            .Build();

        sql.Should().Contain("WHERE sp.ProductID = @ProductID");
        sql.Should().Contain("ORDER BY\n    sp.ProductID ASC");
        sql.Should().Contain("LIMIT 1;");
        parameters.Should().ContainSingle().Which.Value.Should().Be(42);
    }

    [Fact]
    public void FullText_WithMultipleColumns_ShouldGenerateParameterizedMariaDbSqlAndRank()
    {
        var searchTerm = "cimento branco' OR 1=1 --";

        var (sql, parameters) = CreateBuilder()
            .Select(select => select.AllColumns<SearchProduct>())
            .From<SearchProduct>()
            .Where(where => where.FullText(
                searchTerm,
                (SearchProduct p) => p.Description,
                p => p.ManufacturerRef)
                .OrderByRelevanceDescending())
            .Take(25)
            .Skip(50)
            .Build();

        const string matchExpression = "MATCH(sp.Description, sp.ManufacturerRef) AGAINST (@FullTextSearch IN NATURAL LANGUAGE MODE)";
        sql.Should().Contain($"WHERE {matchExpression}");
        sql.Should().Contain($"ORDER BY\n    {matchExpression} DESC");
        sql.Should().Contain("LIMIT 25 OFFSET 50;");
        sql.Should().NotContain(searchTerm);
        parameters.Should().ContainSingle().Which.Key.Should().Be("FullTextSearch");
        parameters["FullTextSearch"].Should().Be(searchTerm);
    }

    [Fact]
    public void FullText_WithOneColumnAndAdditionalWhere_ShouldComposePredicates()
    {
        var (sql, parameters) = CreateBuilder()
            .Select(select => select.AllColumns<SearchProduct>())
            .From<SearchProduct>()
            .Where(where =>
            {
                where.FullText("cimento", (SearchProduct p) => p.Description)
                    .OrderByRelevanceDescending()
                    .And()
                    .Equals((SearchProduct p) => p.IsActive, true);
            })
            .Build();

        sql.Should().Contain("MATCH(sp.Description) AGAINST (@FullTextSearch IN NATURAL LANGUAGE MODE) AND sp.IsActive = @IsActive");
        parameters.Should().ContainKeys("FullTextSearch", "IsActive");
    }

    [Fact]
    public void FullText_InBooleanMode_ShouldGeneratePrefixCapableSql()
    {
        var (sql, parameters) = CreateBuilder()
            .Select(select => select.AllColumns<SearchProduct>())
            .From<SearchProduct>()
            .Where(where => where.FullText(
                "ciment*",
                FullTextSearchMode.Boolean,
                (SearchProduct p) => p.Description))
            .Build();

        sql.Should().Contain("MATCH(sp.Description) AGAINST (@FullTextSearch IN BOOLEAN MODE)");
        parameters["FullTextSearch"].Should().Be("ciment*");
    }

    [Fact]
    public void FullText_WithEmptySearchTerm_ShouldThrow()
    {
        Action action = () => CreateBuilder()
            .Select(select => select.AllColumns<SearchProduct>())
            .From<SearchProduct>()
            .Where(where => where.FullText("   ", (SearchProduct p) => p.Description));

        action.Should().Throw<ArgumentException>()
            .WithMessage("O termo de busca full-text não pode ser vazio.*");
    }

    private static FluentCommandBuilder CreateBuilder() => FluentCommandBuilder.Create(new MariaDBDialect());
}