using System.Linq.Expressions;

namespace Decor.FluentSqlBuilder.Clauses;

public sealed class OrderByBuilder
{
    private readonly Action<OrderDefinition> _addOrdering;

    internal OrderByBuilder(Action<OrderDefinition> addOrdering)
    {
        _addOrdering = addOrdering;
    }

    public OrderByBuilder Ascending<TEntity>(Expression<Func<TEntity, object?>> propertySelector)
    {
        ArgumentNullException.ThrowIfNull(propertySelector);
        _addOrdering(OrderDefinition.ForProperty(typeof(TEntity), propertySelector, SortDirection.Ascending));
        return this;
    }

    public OrderByBuilder Descending<TEntity>(Expression<Func<TEntity, object?>> propertySelector)
    {
        ArgumentNullException.ThrowIfNull(propertySelector);
        _addOrdering(OrderDefinition.ForProperty(typeof(TEntity), propertySelector, SortDirection.Descending));
        return this;
    }

    public OrderByBuilder Apply<TEntity>(Filters<TEntity> filters)
    {
        ArgumentNullException.ThrowIfNull(filters);

        foreach (var ordering in filters.Orderings)
        {
            _addOrdering(OrderDefinition.ForProperty(ordering.EntityType, ordering.PropertySelector, ordering.Direction));
        }

        return this;
    }
}