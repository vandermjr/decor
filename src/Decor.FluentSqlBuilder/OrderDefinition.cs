using System.Linq.Expressions;

namespace Decor.FluentSqlBuilder;

public sealed class OrderDefinition
{
    private OrderDefinition(Type? entityType, LambdaExpression? propertySelector, SortDirection direction, string? relevanceExpression)
    {
        EntityType = entityType;
        PropertySelector = propertySelector;
        Direction = direction;
        RelevanceExpression = relevanceExpression;
    }

    public Type? EntityType { get; }
    public LambdaExpression? PropertySelector { get; }
    public SortDirection Direction { get; }
    public string? RelevanceExpression { get; }
    public bool IsRelevance => !string.IsNullOrWhiteSpace(RelevanceExpression);

    public static OrderDefinition ForProperty<TEntity>(Expression<Func<TEntity, object?>> propertySelector, SortDirection direction)
        => ForProperty(typeof(TEntity), propertySelector, direction);

    public static OrderDefinition ForProperty(Type entityType, LambdaExpression propertySelector, SortDirection direction)
    {
        ArgumentNullException.ThrowIfNull(entityType);
        ArgumentNullException.ThrowIfNull(propertySelector);

        return new OrderDefinition(entityType, propertySelector, direction, null);
    }

    public static OrderDefinition ForRelevance(string relevanceExpression, SortDirection direction)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relevanceExpression);

        return new OrderDefinition(null, null, direction, relevanceExpression);
    }
}
