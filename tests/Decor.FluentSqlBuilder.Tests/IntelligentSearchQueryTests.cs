using Decor.Core.Common;
using Decor.Core.Entities;

namespace Decor.FluentSqlBuilder.Tests;

public sealed class IntelligentSearchQueryTests
{
    [Theory]
    [InlineData("\"piso branco\" arquitech", new[] { "piso branco", "arquitech" })]
    [InlineData("piso \"branco  fosco\"", new[] { "piso", "branco  fosco" })]
    [InlineData("piso \"branco fosco", new[] { "piso", "branco fosco" })]
    [InlineData("\"\" \"   \"", new string[0])]
    [InlineData("\"  piso branco  \"", new[] { "piso branco" })]
    [InlineData("piso\"branco fosco\"marca", new[] { "piso", "branco fosco", "marca" })]
    public void QuotedPhrasesAreSingleTokens(string text, string[] tokens)
    {
        Assert.Equal(tokens, IntelligentSearchQuery.Parse(text).Tokens);
    }

    [Fact]
    public void QuotedProductFiltersAreLiteralAndUnquotedFiltersRemainTyped()
    {
        var query = ProductSearchQuery.Parse("\"sem estoque tipo:servico\" tipo:produto estoque > 2");
        Assert.Equal(new[] { "sem estoque tipo:servico" }, query.Tokens);
        Assert.Equal(ProductType.Good, query.Type);
        Assert.Equal(2m, query.StockGreaterThan);
        Assert.Null(query.StockEquals);
        var unmatched = ProductSearchQuery.Parse("piso \"sem estoque");
        Assert.Equal(new[] { "piso", "sem estoque" }, unmatched.Tokens);
        Assert.Null(unmatched.StockEquals);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t\n ")]
    public void EmptyQueryHasNoTokens(string? text)
    {
        var query = IntelligentSearchQuery.Parse(text);
        Assert.Empty(query.Tokens);
        Assert.False(query.IsIdSearch);
        Assert.Null(query.Id);
        Assert.False(query.TryGetIntegerId(out _));
    }

    [Fact]
    public void TokensPreserveCaseAndSplitAllWhitespace()
    {
        Assert.Equal(new[] { "PISO", "arquitech", "42" },
            IntelligentSearchQuery.Parse("  PISO\t arquitech\n42 ").Tokens);
    }

    [Theory]
    [InlineData("00042", true, 42, true)]
    [InlineData("0", true, 0, true)]
    [InlineData("-42", false, null, true)]
    [InlineData("+42", false, null, true)]
    [InlineData("2147483648", true, null, false)]
    [InlineData("-2147483649", false, null, false)]
    [InlineData("42 piso", false, null, false)]
    [InlineData("ABC42", false, null, false)]
    public void NumericInterpretationPreservesBothExistingPolicies(string text, bool exact, int? id, bool integer)
    {
        var query = IntelligentSearchQuery.Parse(text);
        Assert.Equal(exact, query.IsIdSearch);
        Assert.Equal(id, query.Id);
        Assert.Equal(integer, query.TryGetIntegerId(out _));
    }

    [Fact]
    public void ProductFiltersStillLeaveNumericCodeAndTextTokens()
    {
        var code = ProductSearchQuery.Parse("00042 tipo:produto com estoque");
        Assert.True(code.IsProductIdSearch);
        Assert.Equal(42, code.ProductID);
        Assert.Equal(ProductType.Good, code.Type);
        Assert.Equal(0m, code.StockGreaterThan);
        var text = ProductSearchQuery.Parse("piso estoque < -1,25 arquitech");
        Assert.Equal(new[] { "piso", "arquitech" }, text.Tokens);
        Assert.Equal(-1.25m, text.StockLessThan);
    }
}