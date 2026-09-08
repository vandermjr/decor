using System.ComponentModel.DataAnnotations.Schema;
using System.Linq.Expressions;
using System.Reflection;

namespace Decor.FluentSqlBuilder.Helpers;

public static class ExpressionHelper
{
    /// <summary>
    /// Extrai o objeto PropertyInfo de uma expressão lambda como 'p => p.PropertyName'.
    /// </summary>
    public static PropertyInfo GetPropertyInfo<TEntity>(Expression<Func<TEntity, object?>> propertySelector)
    {
        ArgumentNullException.ThrowIfNull(propertySelector);

        Expression body = propertySelector.Body;

        if (body is UnaryExpression unaryExpression)
        {
            body = unaryExpression.Operand;
        }

        if (body is MemberExpression memberExpression && memberExpression.Member is PropertyInfo propertyInfo)
        {
            return propertyInfo;
        }

        throw new ArgumentException($"A expressão '{propertySelector}' não é um acesso de membro de propriedade válido (ex: x => x.Property).", nameof(propertySelector));
    }

    /// <summary>
    /// Extrai o nome da coluna no banco de dados para a propriedade (considerando [Column("col_name")] ou o nome da propriedade).
    /// </summary>
    public static string GetColumnName(PropertyInfo propertyInfo)
    {
        ArgumentNullException.ThrowIfNull(propertyInfo);

        var columnAttr = propertyInfo.GetCustomAttribute<ColumnAttribute>();
        return !string.IsNullOrEmpty(columnAttr?.Name) ? columnAttr.Name : propertyInfo.Name;
    }

    /// <summary>
    /// Extrai o nome da propriedade de uma expressão lambda como 'p => p.PropertyName'.
    /// </summary>
    public static string GetPropertyName<TEntity>(Expression<Func<TEntity, object?>> propertySelector)
    {
        return GetPropertyInfo(propertySelector).Name;
    }

    /// <summary>
    /// Extrai o nome da coluna no banco de dados de uma expressão lambda.
    /// </summary>
    public static string GetColumnName<TEntity>(Expression<Func<TEntity, object?>> propertySelector)
    {
        return GetColumnName(GetPropertyInfo(propertySelector));
    }

    /// <summary>
    /// Extrai o nome da propriedade, nome da coluna e o ParameterExpression de uma expressão lambda.
    /// </summary>
    public static (string PropertyName, string ColumnName, ParameterExpression Parameter) GetPropertyAndColumnNameAndParameter<TEntity>(Expression<Func<TEntity, object?>> propertySelector)
    {
        ArgumentNullException.ThrowIfNull(propertySelector);

        Expression body = propertySelector.Body;

        if (body is UnaryExpression unaryExpression)
        {
            body = unaryExpression.Operand;
        }

        if (body is MemberExpression memberExpression &&
            memberExpression.Member is PropertyInfo propertyInfo &&
            memberExpression.Expression is ParameterExpression parameterExpression)
        {
            string columnName = GetColumnName(propertyInfo);
            return (propertyInfo.Name, columnName, parameterExpression);
        }

        throw new ArgumentException($"A expressão '{propertySelector}' não é um acesso de membro de propriedade válido ou não contém um ParameterExpression.", nameof(propertySelector));
    }

    /// <summary>
    /// Extrai o nome da propriedade e o ParameterExpression de uma expressão lambda.
    /// </summary>
    public static (string PropertyName, ParameterExpression Parameter) GetPropertyNameAndParameter<TEntity>(Expression<Func<TEntity, object?>> propertySelector)
    {
        var (propertyName, _, parameter) = GetPropertyAndColumnNameAndParameter(propertySelector);
        return (propertyName, parameter);
    }

    /// <summary>
    /// Extrai os nomes das colunas e os ParameterExpressions de uma expressão de condição JOIN.
    /// </summary>
    public static (string LeftColumnName, ParameterExpression LeftParameter, string RightColumnName, ParameterExpression RightParameter) GetJoinPropertiesAndParameters<TLeft, TRight>(Expression<Func<TLeft, TRight, bool>> onCondition)
    {
        ArgumentNullException.ThrowIfNull(onCondition);

        if (onCondition.Body is BinaryExpression binaryExpression &&
            binaryExpression.NodeType == ExpressionType.Equal)
        {
            MemberExpression? leftMember = GetMemberExpression(binaryExpression.Left);
            MemberExpression? rightMember = GetMemberExpression(binaryExpression.Right);

            if (leftMember?.Member is PropertyInfo leftProperty && leftMember.Expression is ParameterExpression leftParam &&
                rightMember?.Member is PropertyInfo rightProperty && rightMember.Expression is ParameterExpression rightParam)
            {
                if (leftParam.Type == typeof(TRight) && rightParam.Type == typeof(TLeft))
                {
                    return (GetColumnName(rightProperty), rightParam, GetColumnName(leftProperty), leftParam);
                }

                return (GetColumnName(leftProperty), leftParam, GetColumnName(rightProperty), rightParam);
            }
        }

        throw new ArgumentException($"A expressão de condição JOIN '{onCondition}' não é uma comparação de igualdade de membros válida (ex: (l, r) => l.Id == r.Id).", nameof(onCondition));
    }

    private static MemberExpression? GetMemberExpression(Expression expression)
    {
        if (expression is MemberExpression memberExpression)
        {
            return memberExpression;
        }
        if (expression is UnaryExpression unaryExpression && unaryExpression.Operand is MemberExpression unaryMemberExpression)
        {
            return unaryMemberExpression;
        }
        return null;
    }
}
