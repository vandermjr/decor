using System.Linq.Expressions;
using System.Reflection;
using Decor.FluentSqlBuilder.Dialects;
using Decor.FluentSqlBuilder.Helpers;

namespace Decor.FluentSqlBuilder.Clauses
{
    public class JoinClauseBuilder
    {
        private readonly List<(string JoinKeyword, string Clause)> _joinClauses;
        private readonly Dictionary<string, object?> _parameters;
        private readonly TableAliasRegistry _aliasRegistry;
        private readonly IDialect _dialect;

        internal JoinClauseBuilder(List<(string JoinKeyword, string Clause)> joinClauses, Dictionary<string, object?> parameters, TableAliasRegistry aliasRegistry, IDialect dialect)
        {
            _joinClauses = joinClauses;
            _parameters = parameters;
            _aliasRegistry = aliasRegistry;
            _dialect = dialect;
        }

        /// <summary>
        /// Adiciona um INNER JOIN. A condição ON aceita uma ou mais igualdades combinadas com &amp;&amp;
        /// (ex: (l, r) => l.Id == r.Id &amp;&amp; r.IsActive == true).
        /// </summary>
        public JoinClauseBuilder Inner<TLeft, TRight>(Expression<Func<TLeft, TRight, bool>> onCondition) =>
            AddJoin(_dialect.Keywords.INNER_JOIN, typeof(TRight), onCondition);

        /// <summary>
        /// Adiciona um LEFT OUTER JOIN. A condição ON aceita uma ou mais igualdades combinadas com &amp;&amp;
        /// (ex: (l, r) => l.Id == r.Id &amp;&amp; r.IsActive == true).
        /// </summary>
        public JoinClauseBuilder Left<TLeft, TRight>(Expression<Func<TLeft, TRight, bool>> onCondition) =>
            AddJoin(_dialect.Keywords.LEFT_OUTER_JOIN, typeof(TRight), onCondition);

        /// <summary>
        /// Adiciona um INNER JOIN cuja condição ON referencia duas entidades já presentes na consulta
        /// (FROM ou JOINs anteriores) além da entidade sendo unida agora (ex: (a, b, r) => a.Id == r.AId &amp;&amp; b.Id == r.BId).
        /// </summary>
        public JoinClauseBuilder Inner<TA, TB, TRight>(Expression<Func<TA, TB, TRight, bool>> onCondition) =>
            AddJoin(_dialect.Keywords.INNER_JOIN, typeof(TRight), onCondition);

        /// <summary>
        /// Adiciona um LEFT OUTER JOIN cuja condição ON referencia duas entidades já presentes na consulta
        /// (FROM ou JOINs anteriores) além da entidade sendo unida agora (ex: (a, b, r) => a.Id == r.AId &amp;&amp; b.Id == r.BId).
        /// </summary>
        public JoinClauseBuilder Left<TA, TB, TRight>(Expression<Func<TA, TB, TRight, bool>> onCondition) =>
            AddJoin(_dialect.Keywords.LEFT_OUTER_JOIN, typeof(TRight), onCondition);

        private JoinClauseBuilder AddJoin(string joinKeyword, Type rightEntityType, LambdaExpression onCondition)
        {
            ArgumentNullException.ThrowIfNull(onCondition);

            var parameterAliases = onCondition.Parameters
                .Select(p => (Parameter: p, Alias: _aliasRegistry.GetOrAddAlias(p.Type)))
                .ToArray();

            string rightAlias = _aliasRegistry.GetOrAddAlias(rightEntityType);
            string rightTableName = _aliasRegistry.GetTableName(rightEntityType);

            string onClause = BuildOnClause(onCondition.Body, parameterAliases);

            _joinClauses.Add((joinKeyword, $"{rightTableName} {_dialect.Keywords.AS} {rightAlias} {_dialect.Keywords.ON} {onClause}"));
            return this;
        }

        private string BuildOnClause(Expression body, (ParameterExpression Parameter, string Alias)[] parameterAliases)
        {
            if (body is BinaryExpression { NodeType: ExpressionType.AndAlso } andExpression)
            {
                string left = BuildOnClause(andExpression.Left, parameterAliases);
                string right = BuildOnClause(andExpression.Right, parameterAliases);
                return $"{left} {_dialect.Keywords.AND} {right}";
            }

            if (body is BinaryExpression { NodeType: ExpressionType.Equal } equalExpression)
            {
                string leftSide = ResolveOnSide(equalExpression.Left, parameterAliases);
                string rightSide = ResolveOnSide(equalExpression.Right, parameterAliases);
                return $"{leftSide} {_dialect.Keywords.EQUALS} {rightSide}";
            }

            throw new NotSupportedException($"A condição de JOIN '{body}' não é suportada. Combine apenas igualdades com && (ex: (l, r) => l.Id == r.Id && r.IsActive == true).");
        }

        private string ResolveOnSide(Expression expression, (ParameterExpression Parameter, string Alias)[] parameterAliases)
        {
            if (expression is UnaryExpression unary)
            {
                expression = unary.Operand;
            }

            if (expression is MemberExpression { Member: PropertyInfo propertyInfo } member && member.Expression is ParameterExpression rootParam)
            {
                foreach (var (parameter, alias) in parameterAliases)
                {
                    if (parameter == rootParam)
                    {
                        return $"{alias}.{ExpressionHelper.GetColumnName(propertyInfo)}";
                    }
                }
            }

            // Não é uma propriedade de nenhuma das entidades do JOIN: trata como valor constante parametrizado.
            object? value = expression is ConstantExpression constant ? constant.Value : Expression.Lambda(expression).Compile().DynamicInvoke();
            string paramName = ParameterHelper.GenerateUniqueParameterName("JoinParam", _parameters);
            _parameters[paramName] = value;
            return $"{_dialect.GetParameterPrefix()}{paramName}";
        }
    }
}

