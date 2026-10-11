using Decor.Core.Common;
using Decor.Core.Entities;

namespace Decor.Application.Tests;

public sealed class ProductSearchQueryTests
{
    [Theory]
    [InlineData("sem estoque", false)]
    [InlineData("SEM ESTOQUE", false)]
    [InlineData("estoque = 0", false)]
    [InlineData("com estoque negativo", true)]
    [InlineData("estoque negativo", true)]
    [InlineData("estoque < 0", true)]
    [InlineData("estoque abaixo de 0", true)]
    public void ZeroAndNegativeStockAreNotTextTokens(string phrase, bool negative)
    {
        var query = ProductSearchQuery.Parse("piso " + phrase + " tipo:produto arquitech");
        Assert.Equal(new[] { "piso", "arquitech" }, query.Tokens);
        Assert.Equal(ProductType.Good, query.Type);
        Assert.Null(query.StockGreaterThan);
        Assert.Equal(negative ? 0m : null, query.StockLessThan);
        Assert.Equal(negative ? null : 0m, query.StockEquals);
    }

    [Fact]
    public void StockBoundsAcceptDecimalsAndRejectContradictions()
    {
        var query = ProductSearchQuery.Parse("estoque > -5 estoque < -1,25 estoque < 0");
        Assert.Equal(-5m, query.StockGreaterThan);
        Assert.Equal(-1.25m, query.StockLessThan);
        Assert.Empty(query.Tokens);
        Assert.Throws<ArgumentException>(() => ProductSearchQuery.Parse("com estoque sem estoque"));
        Assert.Throws<ArgumentException>(() => ProductSearchQuery.Parse("sem estoque com estoque negativo"));
        Assert.Throws<ArgumentException>(() => ProductSearchQuery.Parse("estoque = 1 estoque = 2"));
        Assert.Throws<ArgumentException>(() => ProductSearchQuery.Parse("estoque > 2 estoque < 1"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t\n ")]
    public void EmptySearchHasNoFilters(string? text)
    {
        var query = ProductSearchQuery.Parse(text);
        Assert.Empty(query.Tokens);
        Assert.Null(query.StockGreaterThan);
        Assert.Null(query.Type);
    }

    [Fact]
    public void DescriptionAndBrandRemainSeparateTokens()
    {
        Assert.Equal(new[] { "piso", "arquitech" }, ProductSearchQuery.Parse("  piso\t arquitech ").Tokens);
    }

    [Theory]
    [InlineData("estoque acima de 0")]
    [InlineData("estoque > 0")]
    [InlineData("com estoque")]
    [InlineData("COM ESTOQUE")]
    public void NaturalStockPhrasesBecomeTypedFilters(string text)
    {
        var query = ProductSearchQuery.Parse(text);
        Assert.Equal(0m, query.StockGreaterThan);
        Assert.Empty(query.Tokens);
    }

    [Theory]
    [InlineData("piso estoque > 1,25 arquitech")]
    [InlineData("piso estoque acima de 1.25 arquitech")]
    public void StockAcceptsDecimalCommaAndPoint(string text)
    {
        var query = ProductSearchQuery.Parse(text);
        Assert.Equal(1.25m, query.StockGreaterThan);
        Assert.Equal(new[] { "piso", "arquitech" }, query.Tokens);
    }

    [Theory]
    [InlineData("tipo:produto", ProductType.Good)]
    [InlineData("tipo:servico", ProductType.Service)]
    [InlineData("TIPO:SERVIÇO", ProductType.Service)]
    public void TypePhraseIsRemovedFromTokens(string text, ProductType type)
    {
        var query = ProductSearchQuery.Parse("piso " + text + " com estoque");
        Assert.Equal(type, query.Type);
        Assert.Equal(0m, query.StockGreaterThan);
        Assert.Equal(new[] { "piso" }, query.Tokens);
    }

    [Theory]
    [InlineData("tipo:desconhecido estoque > abc")]
    [InlineData("estoque > 999999999999999999999999999999999999")]
    public void UnsupportedFiltersRemainLiteralTokens(string text)
    {
        var query = ProductSearchQuery.Parse(text);
        Assert.Null(query.Type);
        Assert.Null(query.StockGreaterThan);
        Assert.Equal(text.Split(' '), query.Tokens);
    }

    [Fact]
    public void TokensPreserveAccentsCaseAndTypos()
    {
        Assert.Equal(new[] { "PÍSO", "architec" }, ProductSearchQuery.Parse("PÍSO architec").Tokens);
    }

    [Fact]
    public void NumericTokenIsPreservedAlongsideFilters()
    {
        var query = ProductSearchQuery.Parse("42 tipo:produto");
        Assert.Equal(new[] { "42" }, query.Tokens);
        Assert.Equal(ProductType.Good, query.Type);
    }

    [Theory]
    [InlineData("42", true, 42)]
    [InlineData("00042 tipo:produto", true, 42)]
    [InlineData("tipo:servico 00042", true, 42)]
    [InlineData("2147483648", true, null)]
    [InlineData("ABC42", false, null)]
    [InlineData("42 piso", false, null)]
    [InlineData("+42", false, null)]
    public void OnlySingleNumericCodesUseExactIdSearch(string text, bool exact, int? id)
    {
        var query = ProductSearchQuery.Parse(text);
        Assert.Equal(exact, query.IsProductIdSearch);
        Assert.Equal(id, query.ProductID);
    }

    [Fact]
    public void RepeatedStockFiltersUseStrongestBound()
    {
        var query = ProductSearchQuery.Parse("estoque > -2 com estoque estoque acima de 5,5 estoque > 1");
        Assert.Equal(5.5m, query.StockGreaterThan);
        Assert.Empty(query.Tokens);
    }

    [Fact]
    public void ConflictingTypesAreRejectedAndRepeatedSameTypeIsAllowed()
    {
        Assert.Throws<ArgumentException>(() => ProductSearchQuery.Parse("tipo:produto tipo:servico"));
        Assert.Equal(ProductType.Good, ProductSearchQuery.Parse("tipo:produto tipo:produto").Type);
    }
}