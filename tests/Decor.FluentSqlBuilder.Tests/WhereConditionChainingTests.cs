using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Decor.FluentSqlBuilder;
using FluentAssertions;

namespace Decor.FluentSqlBuilder.Tests;

public sealed class WhereConditionChainingTests
{
    [Table("chain_entities")]
    private sealed class ChainEntity
    {
        [Key]
        public int EntityID { get; set; }
        public bool IsActive { get; set; }
        public string? Description { get; set; }
        public int Amount { get; set; }
        public int CategoryID { get; set; }
    }

    [Fact]
    public void Equals_CanChainWithImplicitAnd()
    {
        var (sql, parameters) = FluentCommandBuilder.Create()
            .Select(s => s.WithColumns<ChainEntity>(e => e.EntityID))
            .From<ChainEntity>()
            .Where(w => w
                .Equals<ChainEntity>(e => e.EntityID, 1)
                .Equals<ChainEntity>(e => e.IsActive, true))
            .Build();

        sql.Should().Contain("WHERE ce.EntityID = @EntityID AND ce.IsActive = @IsActive");
        parameters.Should().Contain("EntityID", 1).And.Contain("IsActive", true);
    }

    [Fact]
    public void Equals_Or_Contains_CanChain()
    {
        var (sql, parameters) = FluentCommandBuilder.Create()
            .Select(s => s.WithColumns<ChainEntity>(e => e.EntityID))
            .From<ChainEntity>()
            .Where(w => w
                .Equals<ChainEntity>(e => e.EntityID, 1)
                .Or()
                .Contains<ChainEntity>(e => e.Description, "chair"))
            .Build();

        sql.Should().Contain("WHERE ce.EntityID = @EntityID OR ce.Description LIKE CONCAT('%', @Description, '%')");
        parameters.Should().Contain("EntityID", 1).And.Contain("Description", "chair");
    }

    [Fact]
    public void IsNull_Or_Equals_CanChain()
    {
        var (sql, parameters) = FluentCommandBuilder.Create()
            .Select(s => s.WithColumns<ChainEntity>(e => e.EntityID))
            .From<ChainEntity>()
            .Where(w => w
                .IsNull<ChainEntity>(e => e.Description)
                .Or()
                .Equals<ChainEntity>(e => e.Description, "unknown"))
            .Build();

        sql.Should().Contain("WHERE ce.Description IS NULL OR ce.Description = @Description");
        parameters.Should().Contain("Description", "unknown");
    }

    [Fact]
    public void GreaterThan_LessThan_CanChain()
    {
        var (sql, parameters) = FluentCommandBuilder.Create()
            .Select(s => s.WithColumns<ChainEntity>(e => e.EntityID))
            .From<ChainEntity>()
            .Where(w => w
                .GreaterThan<ChainEntity>(e => e.Amount, 10)
                .LessThan<ChainEntity>(e => e.Amount, 100))
            .Build();

        sql.Should().Contain("WHERE ce.Amount > @Amount AND ce.Amount < @Amount_0");
        parameters.Should().Contain("Amount", 10).And.Contain("Amount_0", 100);
    }

    [Fact]
    public void In_CanChainWithAnotherCondition()
    {
        var (sql, parameters) = FluentCommandBuilder.Create()
            .Select(s => s.WithColumns<ChainEntity>(e => e.EntityID))
            .From<ChainEntity>()
            .Where(w => w
                .In<ChainEntity>(e => e.CategoryID, new object[] { 2, 3 })
                .Equals<ChainEntity>(e => e.IsActive, true))
            .Build();

        sql.Should().Contain("WHERE ce.CategoryID IN (@CategoryID_0, @CategoryID_1) AND ce.IsActive = @IsActive");
        parameters.Should().Contain("CategoryID_0", 2).And.Contain("CategoryID_1", 3).And.Contain("IsActive", true);
    }

    [Fact]
    public void Group_CanBeChained()
    {
        var (sql, parameters) = FluentCommandBuilder.Create()
            .Select(s => s.WithColumns<ChainEntity>(e => e.EntityID))
            .From<ChainEntity>()
            .Where(w => w
                .Equals<ChainEntity>(e => e.IsActive, true)
                .Group(g => g
                    .IsNull<ChainEntity>(e => e.Description)
                    .Or()
                    .Equals<ChainEntity>(e => e.Description, "unknown")))
            .Build();

        sql.Should().Contain("WHERE ce.IsActive = @IsActive AND (ce.Description IS NULL OR ce.Description = @Description)");
        parameters.Should().Contain("IsActive", true).And.Contain("Description", "unknown");
    }

    [Fact]
    public void FullText_OrderByRelevance_RemainsSupported()
    {
        var (sql, parameters) = FluentCommandBuilder.Create()
            .Select(s => s.WithColumns<ChainEntity>(e => e.EntityID))
            .From<ChainEntity>()
            .Where(w => w
                .FullText<ChainEntity>("chair", e => e.Description!)
                .OrderByRelevanceDescending())
            .Build();

        sql.Should().Contain("MATCH(ce.Description) AGAINST (@FullTextSearch IN NATURAL LANGUAGE MODE)");
        sql.Should().Contain("ORDER BY");
        sql.Should().Contain("DESC");
        parameters.Should().Contain("FullTextSearch", "chair");
    }

    [Fact]
    public void WithDynamicSearchFilter_CanBeFollowedByAnotherCondition()
    {
        var (sql, parameters) = FluentCommandBuilder.Create()
            .Select(s => s.WithColumns<ChainEntity>(e => e.EntityID))
            .From<ChainEntity>()
            .Where(w => w
                .WithDynamicSearchFilter<ChainEntity, ChainEntity>("chair", e => e.EntityID, e => e.Description!)
                .Equals<ChainEntity>(e => e.IsActive, true))
            .Build();

        sql.Should().Contain("WHERE ce.Description LIKE CONCAT('%', @Description, '%') AND ce.IsActive = @IsActive");
        parameters.Should().Contain("Description", "chair").And.Contain("IsActive", true);
    }

    [Fact]
    public void RepeatedPropertyNames_ReceiveUniqueParameters()
    {
        var (sql, parameters) = FluentCommandBuilder.Create()
            .Select(s => s.WithColumns<ChainEntity>(e => e.EntityID))
            .From<ChainEntity>()
            .Where(w => w
                .Equals<ChainEntity>(e => e.Description, "first")
                .Equals<ChainEntity>(e => e.Description, "second"))
            .Build();

        sql.Should().Contain("ce.Description = @Description AND ce.Description = @Description_0");
        parameters.Should().Contain("Description", "first").And.Contain("Description_0", "second");
    }

    [Fact]
    public void DifferentEntities_CanBeChainedInOneWhere()
    {
        var (sql, parameters) = FluentCommandBuilder.Create()
            .Select(s => s.WithColumns<ChainEntity>(e => e.EntityID))
            .From<ChainEntity>()
            .Where(w => w
                .Equals<ChainEntity>(e => e.EntityID, 1)
                .Equals<OtherEntity>(e => e.OtherID, 2))
            .Build();

        sql.Should().Contain("WHERE ce.EntityID = @EntityID AND oe.OtherID = @OtherID");
        parameters.Should().Contain("EntityID", 1).And.Contain("OtherID", 2);
    }

    [Fact]
    public void Chaining_WorksInUpdate()
    {
        var (sql, parameters) = FluentCommandBuilder.Create()
            .Update(u => u.Entity<ChainEntity>(e => e.Description, "updated"))
            .Where(w => w
                .Equals<ChainEntity>(e => e.EntityID, 1)
                .Equals<ChainEntity>(e => e.IsActive, true))
            .Build();

        sql.Should().Contain("WHERE ce.EntityID = @EntityID AND ce.IsActive = @IsActive");
        parameters.Should().Contain("Description", "updated").And.Contain("EntityID", 1).And.Contain("IsActive", true);
    }

    [Fact]
    public void Chaining_WorksInDelete()
    {
        var (sql, parameters) = FluentCommandBuilder.Create()
            .Delete<ChainEntity>()
            .Where(w => w
                .Equals<ChainEntity>(e => e.EntityID, 1)
                .Equals<ChainEntity>(e => e.IsActive, true))
            .Build();

        sql.Should().Contain("WHERE ce.EntityID = @EntityID AND ce.IsActive = @IsActive");
        parameters.Should().Contain("EntityID", 1).And.Contain("IsActive", true);
    }

    [Table("other_entities")]
    private sealed class OtherEntity
    {
        [Key]
        public int OtherID { get; set; }
    }
}
