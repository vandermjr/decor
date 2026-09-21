using Decor.Core.Entities;
using Decor.FluentSqlBuilder;

namespace Decor.FluentSqlBuilder.Tests;

public class TypedOrderingTests
{
    [Fact]
    public void OrderBySupportsMultipleTypedCriteria()
    {
        var (sql, parameters) = FluentCommandBuilder.Create()
            .Select<ApplicationUser>(s => s.WithColumns<ApplicationUser>(u => u.UserID, u => u.Username))
            .OrderBy(o => o
                .Ascending<ApplicationUser>(u => u.Username)
                .Descending<ApplicationUser>(u => u.UserID))
            .Build();

        Assert.Contains("ORDER BY\n    au.Username ASC,\n    au.UserID DESC", sql);
        Assert.Empty(parameters);
    }

    [Fact]
    public void ApplyUsesStructuredCriteriaFromMultipleEntities()
    {
        var filters = new Filters<Product>();
        filters.Add<Product>(p => p.Description, SortDirection.Ascending);
        filters.Add<Brand>(b => b.BrandName, SortDirection.Descending);

        var (sql, parameters) = FluentCommandBuilder.Create()
            .Select<Product>(s =>
            {
                s.WithColumns<Product>(p => p.ProductID, p => p.Description);
                s.WithColumns<Brand>(b => b.BrandName);
            })
            .Join(j => j.Inner<Product, Brand>((p, b) => p.BrandID == b.BrandID))
            .OrderBy(o => o.Apply(filters))
            .Build();

        Assert.Contains("ORDER BY\n    p.Description ASC,\n    b.BrandName DESC", sql);
        Assert.DoesNotContain("@", sql);
        Assert.Empty(parameters);
    }

    [Fact]
    public void ApplyPreservesCriteriaOrderAndDirections()
    {
        var filters = new Filters<Product>();
        filters.Add<Product>(p => p.Description, SortDirection.Ascending);
        filters.Add<Brand>(b => b.BrandName, SortDirection.Descending);
        filters.Add<Product>(p => p.ProductID, SortDirection.Descending);

        var (sql, _) = FluentCommandBuilder.Create()
            .Select<Product>(s =>
            {
                s.WithColumns<Product>(p => p.ProductID, p => p.Description);
                s.WithColumns<Brand>(b => b.BrandName);
            })
            .Join(j => j.Inner<Product, Brand>((p, b) => p.BrandID == b.BrandID))
            .OrderBy(o => o.Apply(filters))
            .Build();

        int descriptionIndex = sql.IndexOf("p.Description ASC", StringComparison.Ordinal);
        int brandIndex = sql.IndexOf("b.BrandName DESC", StringComparison.Ordinal);
        int productIdIndex = sql.IndexOf("p.ProductID DESC", StringComparison.Ordinal);

        Assert.True(descriptionIndex >= 0);
        Assert.True(brandIndex > descriptionIndex);
        Assert.True(productIdIndex > brandIndex);
    }

    [Fact]
    public void FullTextRelevancePrecedesStructuredOrderingRegardlessOfBuilderSequence()
    {
        var filters = new Filters<Product>();
        filters.Add<Product>(p => p.Description, SortDirection.Ascending);
        filters.Add<Product>(p => p.ProductID, SortDirection.Descending);

        var (sql, parameters) = FluentCommandBuilder.Create()
            .Select<Product>(s => s.WithColumns<Product>(p => p.ProductID, p => p.Description))
            .Where(w => w.FullText<Product>("cimento", p => p.Description))
            .OrderBy(o => o.Apply(filters))
            .Build();

        Assert.Contains("MATCH(p.Description) AGAINST (@FullTextSearch IN NATURAL LANGUAGE MODE) DESC", sql);
        Assert.True(sql.IndexOf("MATCH(p.Description) AGAINST (@FullTextSearch IN NATURAL LANGUAGE MODE) DESC", StringComparison.Ordinal) < sql.IndexOf("p.Description ASC", StringComparison.Ordinal));
        Assert.True(sql.IndexOf("p.Description ASC", StringComparison.Ordinal) < sql.IndexOf("p.ProductID DESC", StringComparison.Ordinal));
        Assert.Contains("FullTextSearch", parameters.Keys);
    }

    [Fact]
    public void FullTextRelevancePrecedesExplicitOrderingWhenConfiguredFirst()
    {
        var (sql, _) = FluentCommandBuilder.Create()
            .Select<Product>(s => s.WithColumns<Product>(p => p.ProductID, p => p.Description))
            .OrderBy(o => o.Ascending<Product>(p => p.ProductID))
            .Where(w => w.FullText<Product>("cimento", p => p.Description))
            .Build();

        var relevanceIndex = sql.IndexOf("MATCH(p.Description) AGAINST (@FullTextSearch IN NATURAL LANGUAGE MODE) DESC", StringComparison.Ordinal);
        var explicitIndex = sql.IndexOf("p.ProductID ASC", StringComparison.Ordinal);

        Assert.True(relevanceIndex >= 0);
        Assert.True(explicitIndex > relevanceIndex);
    }

    [Fact]
    public void FullTextRelevancePrecedesDynamicOrderingWhenFiltersConfiguredFirst()
    {
        var filters = new Filters<Product>();
        filters.Add<Product>(p => p.ProductID, SortDirection.Descending);

        var (sql, _) = FluentCommandBuilder.Create()
            .Select<Product>(s => s.WithColumns<Product>(p => p.ProductID, p => p.Description))
            .OrderBy(o => o.Apply(filters))
            .Where(w => w.FullText<Product>("cimento", p => p.Description))
            .Build();

        var relevanceIndex = sql.IndexOf("MATCH(p.Description) AGAINST (@FullTextSearch IN NATURAL LANGUAGE MODE) DESC", StringComparison.Ordinal);
        var dynamicIndex = sql.IndexOf("p.ProductID DESC", StringComparison.Ordinal);

        Assert.True(relevanceIndex >= 0);
        Assert.True(dynamicIndex > relevanceIndex);
    }

    [Fact]
    public void LegacyStringOrderByRemainsCompatibleButLowerThanStructuredOrderings()
    {
        var filters = new Filters<Product>();
        filters.Add<Product>(p => p.Description, SortDirection.Ascending);

        var (sql, _) = FluentCommandBuilder.Create()
            .Select<Product>(s => s.WithColumns<Product>(p => p.ProductID, p => p.Description))
            .OrderBy("p.ProductID DESC")
            .OrderBy(o => o.Apply(filters))
            .Build();

        Assert.Contains("p.Description ASC", sql);
        Assert.DoesNotContain("p.ProductID DESC", sql);
        Assert.True(sql.IndexOf("p.Description ASC", StringComparison.Ordinal) >= 0);
    }
}