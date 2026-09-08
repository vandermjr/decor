using System.ComponentModel.DataAnnotations; // Necessário para [Key]
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq.Expressions;
using System.Reflection;

namespace Decor.FluentSqlBuilder.Helpers
{
    /// <summary>
    /// Classe auxiliar para manipulação de expressões e parâmetros.
    /// </summary>
    public static class ParameterHelper
    {
        /// <summary>
        /// Extrai o nome da propriedade de uma expressão lambda.
        /// </summary>
        public static string GetPropertyName<TEntity, TProperty>(Expression<Func<TEntity, TProperty>> propertySelector)
        {
            ArgumentNullException.ThrowIfNull(propertySelector);

            if (propertySelector.Body is MemberExpression memberExpression)
            {
                return memberExpression.Member.Name;
            }
            if (propertySelector.Body is UnaryExpression unaryExpression && unaryExpression.Operand is MemberExpression operandMemberExpression)
            {
                return operandMemberExpression.Member.Name;
            }
            throw new ArgumentException("A expressão deve ser um acesso de membro (e.g., x => x.Property).", nameof(propertySelector));
        }

        /// <summary>
        /// Extrai o nome da propriedade de uma expressão lambda como 'p => p.PropertyName'.
        /// </summary>
        public static string GetPropertyName<TEntity>(Expression<Func<TEntity, object?>> propertySelector)
        {
            return ExpressionHelper.GetPropertyName(propertySelector);
        }

        /// <summary>
        /// Extrai o nome da propriedade e o ParameterExpression de uma expressão lambda.
        /// </summary>
        public static (string PropertyName, ParameterExpression Parameter) GetPropertyNameAndParameter<TEntity>(Expression<Func<TEntity, object?>> propertySelector)
        {
            return ExpressionHelper.GetPropertyNameAndParameter(propertySelector);
        }

        /// <summary>
        /// Gera um nome de parâmetro único para evitar colisões em queries com múltiplos parâmetros.
        /// </summary>
        public static string GenerateUniqueParameterName(string baseName, Dictionary<string, object?> existingParameters)
        {
            ArgumentNullException.ThrowIfNull(baseName);
            ArgumentNullException.ThrowIfNull(existingParameters);

            string uniqueName = baseName;
            int counter = 0;
            while (existingParameters.ContainsKey(uniqueName))
            {
                uniqueName = $"{baseName}_{counter++}";
            }
            return uniqueName;
        }

        /// <summary>
        /// Extensão para Dictionary<string, object?> para facilitar a definição de parâmetros.
        /// </summary>
        public static Dictionary<string, object?> SetParam<TEntity, TProperty>(this Dictionary<string, object?> parameters, Expression<Func<TEntity, TProperty>> propertySelector, TProperty value)
        {
            ArgumentNullException.ThrowIfNull(parameters);
            string paramName = GetPropertyName(propertySelector);
            parameters[paramName] = value;
            return parameters;
        }

        /// <summary>
        /// Retorna a propriedade PropertyInfo que é a chave primária da entidade (marcada com [Key]).
        /// </summary>
        public static PropertyInfo? GetPrimaryKeyProperty(Type entityType)
        {
            ArgumentNullException.ThrowIfNull(entityType);

            foreach (var prop in entityType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (prop.GetCustomAttribute<KeyAttribute>() != null)
                {
                    return prop;
                }
            }
            return null;
        }

        /// <summary>
        /// Retorna o nome da propriedade que é a chave primária da entidade (marcada com [Key]).
        /// </summary>
        public static string? GetPrimaryKeyPropertyName(Type entityType)
        {
            var pkProp = GetPrimaryKeyProperty(entityType);
            return pkProp?.Name;
        }

        /// <summary>
        /// Retorna o nome da coluna do banco que é a chave primária da entidade (marcada com [Key]).
        /// </summary>
        public static string? GetPrimaryKeyColumnName(Type entityType)
        {
            var pkProp = GetPrimaryKeyProperty(entityType);
            return pkProp != null ? ExpressionHelper.GetColumnName(pkProp) : null;
        }

        /// <summary>
        /// Retorna apenas os nomes das propriedades que são mapeadas para o banco de dados.
        /// </summary>
        public static IEnumerable<string> GetMappablePropertyNames(Type entityType)
        {
            return GetMappableProperties(entityType).Select(p => p.PropertyName);
        }

        /// <summary>
        /// Retorna as propriedades mapeáveis junto com seus nomes de propriedade e nomes de coluna.
        /// </summary>
        public static IEnumerable<(PropertyInfo Property, string PropertyName, string ColumnName)> GetMappableProperties(Type entityType)
        {
            ArgumentNullException.ThrowIfNull(entityType);

            return entityType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                             .Where(p => p.GetCustomAttribute<NotMappedAttribute>() == null)
                             .Select(p => (Property: p, PropertyName: p.Name, ColumnName: ExpressionHelper.GetColumnName(p)));
        }

        /// <summary>
        /// Combina os parâmetros de uma instrução componível (subconsulta/UNION/lote) em um dicionário alvo.
        /// Nomes repetidos com o mesmo valor são deduplicados; nomes repetidos com valores diferentes
        /// indicam uma composição ambígua e geram exceção.
        /// </summary>
        public static void MergeParameters(Dictionary<string, object?> target, IReadOnlyDictionary<string, object?> source)
        {
            ArgumentNullException.ThrowIfNull(target);
            ArgumentNullException.ThrowIfNull(source);

            foreach (var (key, value) in source)
            {
                if (target.TryGetValue(key, out var existingValue))
                {
                    if (!Equals(existingValue, value))
                    {
                        throw new InvalidOperationException($"O parâmetro '{key}' foi gerado com valores diferentes em instruções compostas (batch/union/subconsulta). Utilize seletores de propriedade que gerem nomes distintos ou garanta o mesmo valor de filtro.");
                    }
                    continue;
                }
                target[key] = value;
            }
        }
    }
}
