using Decor.Core.Entities;

namespace Decor.FluentSqlBuilder.Tests;

public sealed class IntelligentSearchFilterTests
{
    [Fact]
    public void SearchIsOneOperandWhenPrecededByOrAndPreservesOrdering()
    {
        var (sql, parameters) = FluentCommandBuilder.Create()
            .Select<Brand>(select => select.WithColumns<Brand>(brand => brand.BrandID))
            .Where(where => where.Equals<Brand>(brand => brand.BrandID, 9).Or()
                .WithDynamicSearchFilter<Brand, Brand>("piso branco", brand => brand.BrandID, brand => brand.BrandName)
                .OrderByAscending())
            .Build();
        Assert.Contains("WHERE b.BrandID = @BrandID OR (b.BrandName LIKE CONCAT('%', @BrandName, '%') AND b.BrandName LIKE CONCAT('%', @BrandName_0, '%'))", sql);
        Assert.Matches(@"ORDER BY\s+b\.BrandID ASC", sql);
        Assert.Equal("piso", parameters["BrandName"]);
        Assert.Equal("branco", parameters["BrandName_0"]);
    }

    [Fact]
    public void QuotedPhraseUsesOneParameterizedLikeAndAnotherTokenUsesAnd()
    {
        var (sql, parameters) = FluentCommandBuilder.Create()
            .Select<Brand>(select => select.WithColumns<Brand>(brand => brand.BrandID))
            .Where(where => where.WithDynamicSearchFilter<Brand, Brand>(
                "\"piso branco\" O'Reilly", brand => brand.BrandID, brand => brand.BrandName))
            .Build();
        Assert.Contains(" AND b.BrandName LIKE", sql);
        Assert.DoesNotContain("O'Reilly", sql);
        Assert.Equal("piso branco", parameters["BrandName"]);
        Assert.Equal("O'Reilly", parameters["BrandName_0"]);
    }

    [Fact]
    public void TokensAreAndedWithUniqueParametersAndRemainChainable()
    {
        var (sql, parameters) = FluentCommandBuilder.Create()
            .Select<Brand>(select => select.WithColumns<Brand>(brand => brand.BrandID))
            .Where(where => where.WithDynamicSearchFilter<Brand, Brand>(
                "  piso\t branco ", brand => brand.BrandID, brand => brand.BrandName)
                .Equals<Brand>(brand => brand.BrandID, 9))
            .Build();
        Assert.Contains("WHERE (b.BrandName LIKE CONCAT('%', @BrandName, '%') AND b.BrandName LIKE CONCAT('%', @BrandName_0, '%')) AND b.BrandID = @BrandID", sql);
        Assert.Equal("piso", parameters["BrandName"]);
        Assert.Equal("branco", parameters["BrandName_0"]);
        Assert.Equal(9, parameters["BrandID"]);
    }

    [Theory]
    [InlineData("42", 42)]
    [InlineData(" 00042 ", 42)]
    [InlineData("+42", 42)]
    [InlineData("0", 0)]
    [InlineData("-42", -42)]
    public void SingleIntegerStillUsesExactId(string text, int id)
    {
        bool isIdSearch = false;
        var (sql, parameters) = FluentCommandBuilder.Create()
            .Select<Brand>(select => select.WithColumns<Brand>(brand => brand.BrandID))
            .Where(where => isIdSearch = where.WithDynamicSearchFilter<Brand, Brand>(
                text, brand => brand.BrandID, brand => brand.BrandName).IsIdSearch)
            .Build();
        Assert.True(isIdSearch);
        Assert.Contains("WHERE b.BrandID = @BrandID", sql);
        Assert.Equal(id, parameters["BrandID"]);
        Assert.Single(parameters);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \n\t ")]
    public void EmptyQueryAddsNoWhere(string? text)
    {
        var (sql, parameters) = FluentCommandBuilder.Create()
            .Select<Brand>(select => select.WithColumns<Brand>(brand => brand.BrandID))
            .Where(where => where.WithDynamicSearchFilter<Brand, Brand>(
                text, brand => brand.BrandID, brand => brand.BrandName))
            .Build();
        Assert.DoesNotContain("WHERE", sql);
        Assert.Empty(parameters);
    }

    [Fact]
    public void IntegerOverflowRemainsTextInGenericFilter()
    {
        var (sql, parameters) = FluentCommandBuilder.Create()
            .Select<Brand>(select => select.WithColumns<Brand>(brand => brand.BrandID))
            .Where(where => where.WithDynamicSearchFilter<Brand, Brand>(
                "2147483648", brand => brand.BrandID, brand => brand.BrandName))
            .Build();
        Assert.Contains("b.BrandName LIKE", sql);
        Assert.Equal("2147483648", parameters["BrandName"]);
    }

    [Fact]
    public void UserTokensCanMatchDifferentColumnsWithoutOrLeaking()
    {
        var (sql, parameters) = FluentCommandBuilder.Create()
            .Select<ApplicationUser>(select => select.WithColumns<ApplicationUser>(user => user.UserID))
            .Where(where => where.Equals<ApplicationUser>(user => user.IsActive, true)
                .WithDynamicSearchFilter<ApplicationUser, ApplicationUser>(
                    "junior silva", user => user.UserID, user => user.Username, user => user.DisplayName))
            .Build();
        Assert.Contains("WHERE au.IsActive = @IsActive AND ((au.Username LIKE CONCAT('%', @Username, '%') OR au.DisplayName LIKE CONCAT('%', @DisplayName, '%')) AND (au.Username LIKE CONCAT('%', @Username_0, '%') OR au.DisplayName LIKE CONCAT('%', @DisplayName_0, '%')))", sql);
        Assert.Equal("junior", parameters["Username"]);
        Assert.Equal("junior", parameters["DisplayName"]);
        Assert.Equal("silva", parameters["Username_0"]);
        Assert.Equal("silva", parameters["DisplayName_0"]);
    }
}