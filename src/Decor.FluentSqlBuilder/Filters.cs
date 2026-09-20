using System.Linq.Expressions;

namespace Decor.FluentSqlBuilder;

public enum SortDirection
{
    Ascending,
    Descending
}

public sealed class Filters<TEntity>
{
    private readonly List<FilterOrdering> _orderings = [];

    public void Add<TOrderEntity>(
        Expression<Func<TOrderEntity, object?>> propertySelector,
        SortDirection direction)
    {
        ArgumentNullException.ThrowIfNull(propertySelector);
        _orderings.Add(new FilterOrdering(typeof(TOrderEntity), propertySelector, direction));
    }

    internal IReadOnlyList<FilterOrdering> Orderings => _orderings;
}

internal sealed record FilterOrdering(
    Type EntityType,
    LambdaExpression PropertySelector,
    SortDirection Direction);