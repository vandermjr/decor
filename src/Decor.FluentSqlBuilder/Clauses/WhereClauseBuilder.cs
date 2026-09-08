using System.Linq.Expressions;
using Decor.FluentSqlBuilder.Dialects;
using Decor.FluentSqlBuilder.Helpers;

namespace Decor.FluentSqlBuilder.Clauses
{
    public class WhereClauseBuilder
    {
        internal readonly List<(string Predicate, string Operator)> _predicatesWithOperators;
        internal readonly Dictionary<string, object?> _parameters;
        private readonly Action<string>? _onSetDynamicOrderByColumn;
        private readonly Action<uint>? _onSetDynamicTake;
        private readonly TableAliasRegistry _aliasRegistry;
        private readonly IDialect _dialect;

        private string _nextOperator;

        internal WhereClauseBuilder(
            List<(string Predicate, string Operator)> predicatesWithOperators,
            Dictionary<string, object?> parameters,
            TableAliasRegistry aliasRegistry,
            Action<string>? onSetDynamicOrderByColumn,
            IDialect dialect,
            Action<uint>? onSetDynamicTake = null)
        {
            _predicatesWithOperators = predicatesWithOperators;
            _parameters = parameters;
            _aliasRegistry = aliasRegistry;
            _onSetDynamicOrderByColumn = onSetDynamicOrderByColumn;
            _dialect = dialect;
            _onSetDynamicTake = onSetDynamicTake;

            _nextOperator = dialect.Keywords.AND; // Inicializa com AND
        }

        internal void AddPredicate(string predicate)
        {
            _predicatesWithOperators.Add((predicate, _nextOperator));
            _nextOperator = _dialect.Keywords.AND; // Reseta para AND após adicionar um predicado
        }

        /// <summary>
        /// Adiciona uma condição de igualdade (ex: 'coluna = @param').
        /// </summary>
        /// <typeparam name="TEntity">O tipo da entidade.</typeparam>
        /// <param name="propertySelector">A expressão que seleciona a propriedade.</param>
        /// <param name="value">O valor para a comparação.</param>
        /// <returns>Um WhereConditionChainBuilder para encadeamento.</returns>
        public WhereConditionChainBuilder Equals<TEntity>(Expression<Func<TEntity, object?>> propertySelector, object? value)
        {
            string alias = _aliasRegistry.GetOrAddAlias(typeof(TEntity));
            var propInfo = ExpressionHelper.GetPropertyInfo(propertySelector);
            string propertyName = propInfo.Name;
            string columnName = ExpressionHelper.GetColumnName(propInfo);

            string paramName = ParameterHelper.GenerateUniqueParameterName(propertyName, _parameters);

            AddPredicate($"{alias}.{columnName} {_dialect.Keywords.EQUALS} {_dialect.GetParameterPrefix()}{paramName}");
            _parameters[paramName] = value;

            _onSetDynamicOrderByColumn?.Invoke($"{alias}.{columnName}");

            return new WhereConditionChainBuilder(this, _onSetDynamicOrderByColumn, $"{alias}.{columnName}", _dialect);
        }

        /// <summary>
        /// Adiciona uma condição de desigualdade (ex: 'coluna &lt;&gt; @param').
        /// </summary>
        /// <typeparam name="TEntity">O tipo da entidade.</typeparam>
        /// <param name="propertySelector">A expressão que seleciona a propriedade.</param>
        /// <param name="value">O valor para a comparação.</param>
        /// <returns>Um WhereConditionChainBuilder para encadeamento.</returns>
        public WhereConditionChainBuilder NotEquals<TEntity>(Expression<Func<TEntity, object?>> propertySelector, object? value)
        {
            string alias = _aliasRegistry.GetOrAddAlias(typeof(TEntity));
            var propInfo = ExpressionHelper.GetPropertyInfo(propertySelector);
            string propertyName = propInfo.Name;
            string columnName = ExpressionHelper.GetColumnName(propInfo);

            string paramName = ParameterHelper.GenerateUniqueParameterName(propertyName, _parameters);

            AddPredicate($"{alias}.{columnName} {_dialect.Keywords.NOT_EQUALS} {_dialect.GetParameterPrefix()}{paramName}");
            _parameters[paramName] = value;

            _onSetDynamicOrderByColumn?.Invoke($"{alias}.{columnName}");

            return new WhereConditionChainBuilder(this, _onSetDynamicOrderByColumn, $"{alias}.{columnName}", _dialect);
        }

        /// <summary>
        /// Adiciona uma condição WHERE que verifica se o valor de uma coluna contém a string de busca (LIKE '%value%').
        /// </summary>
        /// <typeparam name="TEntity">O tipo da entidade.</typeparam>
        /// <param name="propertySelector">A expressão lambda para a propriedade da coluna.</param>
        /// <param name="value">A string de busca.</param>
        /// <returns>Um WhereConditionChainBuilder para encadeamento com operadores lógicos e ORDER BY.</returns>
        public WhereConditionChainBuilder Contains<TEntity>(Expression<Func<TEntity, object?>> propertySelector, string value)
        {
            string alias = _aliasRegistry.GetOrAddAlias(typeof(TEntity));
            var propInfo = ExpressionHelper.GetPropertyInfo(propertySelector);
            string propertyName = propInfo.Name;
            string columnName = ExpressionHelper.GetColumnName(propInfo);

            string paramName = ParameterHelper.GenerateUniqueParameterName(propertyName, _parameters);

            string likePattern = _dialect.Functions.Concat(
                $"'{_dialect.Keywords.ANY_STRING_WILDCARD}'",
                $"{_dialect.GetParameterPrefix()}{paramName}",
                $"'{_dialect.Keywords.ANY_STRING_WILDCARD}'"
            );

            AddPredicate($"{alias}.{columnName} {_dialect.Keywords.LIKE} {likePattern}");
            _parameters[paramName] = value;

            _onSetDynamicOrderByColumn?.Invoke($"{alias}.{columnName}");

            return new WhereConditionChainBuilder(this, _onSetDynamicOrderByColumn, $"{alias}.{columnName}", _dialect);
        }

        /// <summary>
        /// Adiciona uma condição WHERE que verifica se o valor de uma coluna começa com a string de busca (LIKE 'value%').
        /// </summary>
        /// <typeparam name="TEntity">O tipo da entidade.</typeparam>
        /// <param name="propertySelector">A expressão lambda para a propriedade da coluna.</param>
        /// <param name="value">A string de busca.</param>
        /// <returns>Um WhereConditionChainBuilder para encadeamento com operadores lógicos e ORDER BY.</returns>
        public WhereConditionChainBuilder BeginsWith<TEntity>(Expression<Func<TEntity, object?>> propertySelector, string value)
        {
            string alias = _aliasRegistry.GetOrAddAlias(typeof(TEntity));
            var propInfo = ExpressionHelper.GetPropertyInfo(propertySelector);
            string propertyName = propInfo.Name;
            string columnName = ExpressionHelper.GetColumnName(propInfo);

            string paramName = ParameterHelper.GenerateUniqueParameterName(propertyName, _parameters);

            string likePattern = _dialect.Functions.Concat(
                $"{_dialect.GetParameterPrefix()}{paramName}",
                $"'{_dialect.Keywords.ANY_STRING_WILDCARD}'"
            );

            AddPredicate($"{alias}.{columnName} {_dialect.Keywords.LIKE} {likePattern}");
            _parameters[paramName] = value;

            _onSetDynamicOrderByColumn?.Invoke($"{alias}.{columnName}");

            return new WhereConditionChainBuilder(this, _onSetDynamicOrderByColumn, $"{alias}.{columnName}", _dialect);
        }

        /// <summary>
        /// Adiciona uma condição WHERE que verifica se o valor de uma coluna termina com a string de busca (LIKE '%value').
        /// </summary>
        /// <typeparam name="TEntity">O tipo da entidade.</typeparam>
        /// <param name="propertySelector">A expressão lambda para a propriedade da coluna.</param>
        /// <param name="value">A string de busca.</param>
        /// <returns>Um WhereConditionChainBuilder para encadeamento com operadores lógicos e ORDER BY.</returns>
        public WhereConditionChainBuilder EndsWith<TEntity>(Expression<Func<TEntity, object?>> propertySelector, string value)
        {
            string alias = _aliasRegistry.GetOrAddAlias(typeof(TEntity));
            var propInfo = ExpressionHelper.GetPropertyInfo(propertySelector);
            string propertyName = propInfo.Name;
            string columnName = ExpressionHelper.GetColumnName(propInfo);

            string paramName = ParameterHelper.GenerateUniqueParameterName(propertyName, _parameters);

            string likePattern = _dialect.Functions.Concat(
                $"'{_dialect.Keywords.ANY_STRING_WILDCARD}'",
                $"{_dialect.GetParameterPrefix()}{paramName}"
            );

            AddPredicate($"{alias}.{columnName} {_dialect.Keywords.LIKE} {likePattern}");
            _parameters[paramName] = value;

            _onSetDynamicOrderByColumn?.Invoke($"{alias}.{columnName}");

            return new WhereConditionChainBuilder(this, _onSetDynamicOrderByColumn, $"{alias}.{columnName}", _dialect);
        }

        /// <summary>
        /// Aplica um filtro de busca dinâmico (por ID ou por texto) baseado no termo fornecido.
        /// Se o termo for um inteiro, busca por ID; caso contrário, busca por texto.
        /// Não adiciona condição WHERE se o termo for nulo ou vazio/espaços em branco.
        /// </summary>
        /// <typeparam name="TIdEntity">O tipo da entidade para a propriedade ID.</typeparam>
        /// <typeparam name="TStringEntity">O tipo da entidade para a propriedade de texto.</typeparam>
        /// <param name="arg">O termo de busca (string).</param>
        /// <param name="idPropertySelector">A expressão lambda para a propriedade ID (ex: p => p.ProductID).</param>
        /// <param name="stringPropertySelector">A expressão lambda para a propriedade de texto (ex: p => p.Description).</param>
        /// <returns>Um WhereConditionChainBuilder com a propriedade IsIdSearch definida, permitindo encadeamento para ORDER BY.</returns>
        public WhereConditionChainBuilder WithDynamicSearchFilter<TIdEntity, TStringEntity>(
            string? arg,
            Expression<Func<TIdEntity, object?>> idPropertySelector,
            Expression<Func<TStringEntity, object?>> stringPropertySelector)
            where TIdEntity : class
            where TStringEntity : class
        {
            string columnForOrderBy = "";
            bool isIdSearch = false;
            WhereConditionChainBuilder resultChainBuilder;

            if (!string.IsNullOrWhiteSpace(arg))
            {
                if (int.TryParse(arg, out int id))
                {
                    // Lógica para Equals (busca por ID)
                    string alias = _aliasRegistry.GetOrAddAlias(typeof(TIdEntity));
                    var propInfo = ExpressionHelper.GetPropertyInfo(idPropertySelector);
                    string propertyName = propInfo.Name;
                    string columnName = ExpressionHelper.GetColumnName(propInfo);

                    string paramName = ParameterHelper.GenerateUniqueParameterName(propertyName, _parameters);
                    AddPredicate($"{alias}.{columnName} {_dialect.Keywords.EQUALS} {_dialect.GetParameterPrefix()}{paramName}");
                    _parameters[paramName] = id;
                    columnForOrderBy = $"{alias}.{columnName}";
                    isIdSearch = true;
                }
                else
                {
                    // Lógica para Contains (busca por texto)
                    string alias = _aliasRegistry.GetOrAddAlias(typeof(TStringEntity));
                    var propInfo = ExpressionHelper.GetPropertyInfo(stringPropertySelector);
                    string propertyName = propInfo.Name;
                    string columnName = ExpressionHelper.GetColumnName(propInfo);

                    string paramName = ParameterHelper.GenerateUniqueParameterName(propertyName, _parameters);
                    string likePattern = _dialect.Functions.Concat(
                        $"'{_dialect.Keywords.ANY_STRING_WILDCARD}'",
                        $"{_dialect.GetParameterPrefix()}{paramName}",
                        $"'{_dialect.Keywords.ANY_STRING_WILDCARD}'"
                    );
                    AddPredicate($"{alias}.{columnName} {_dialect.Keywords.LIKE} {likePattern}");
                    _parameters[paramName] = arg;
                    columnForOrderBy = $"{alias}.{columnName}";
                    isIdSearch = false;
                }
            }
            // Se arg for nulo/vazio, nenhuma condição WHERE é adicionada.
            // columnForOrderBy permanece vazio, isIdSearch permanece false.

            resultChainBuilder = new WhereConditionChainBuilder(this, _onSetDynamicOrderByColumn, columnForOrderBy, _dialect)
            {
                IsIdSearch = isIdSearch
            };

            return resultChainBuilder;
        }

        public WhereConditionChainBuilder FullText<TEntity>(
            string searchTerm,
            params Expression<Func<TEntity, object?>>[] propertySelectors)
        {
            return FullText(searchTerm, FullTextSearchMode.NaturalLanguage, propertySelectors);
        }

        public WhereConditionChainBuilder FullText<TEntity>(
            string searchTerm,
            FullTextSearchMode mode,
            params Expression<Func<TEntity, object?>>[] propertySelectors)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                throw new ArgumentException("O termo de busca full-text não pode ser vazio.", nameof(searchTerm));
            }
            if (propertySelectors is null || propertySelectors.Length == 0)
            {
                throw new ArgumentException("A busca full-text exige ao menos uma coluna.", nameof(propertySelectors));
            }

            string alias = _aliasRegistry.GetOrAddAlias(typeof(TEntity));
            var columns = new List<string>(propertySelectors.Length);
            foreach (var propertySelector in propertySelectors)
            {
                var propInfo = ExpressionHelper.GetPropertyInfo(propertySelector);
                columns.Add($"{alias}.{ExpressionHelper.GetColumnName(propInfo)}");
            }

            var parameterName = ParameterHelper.GenerateUniqueParameterName("FullTextSearch", _parameters);
            var expression = _dialect.BuildFullTextSearch(columns, parameterName, mode);
            AddPredicate(expression);
            _parameters[parameterName] = searchTerm;

            return new WhereConditionChainBuilder(this, _onSetDynamicOrderByColumn, string.Empty, _dialect, expression);
        }

        public WhereConditionChainBuilder WithDynamicFullTextSearch<TIdEntity, TStringEntity>(
            string? arg,
            Expression<Func<TIdEntity, object?>> idPropertySelector,
            params Expression<Func<TStringEntity, object?>>[] textPropertySelectors)
            where TIdEntity : class
            where TStringEntity : class
        {
            if (string.IsNullOrWhiteSpace(arg))
            {
                return new WhereConditionChainBuilder(this, _onSetDynamicOrderByColumn, string.Empty, _dialect);
            }

            if (int.TryParse(arg, out var id))
            {
                var chain = Equals(idPropertySelector, id);
                _onSetDynamicTake?.Invoke(1);
                chain.IsIdSearch = true;
                return chain;
            }

            return FullText(arg, textPropertySelectors);
        }

        // Métodos para encadeamento lógico (And/Or)
        public WhereClauseBuilder And()
        {
            _nextOperator = _dialect.Keywords.AND;
            return this;
        }

        public WhereClauseBuilder Or()
        {
            _nextOperator = _dialect.Keywords.OR;
            return this;
        }

        /// <summary>
        /// Agrupa condições entre parênteses (ex: '(coluna IS NULL OR coluna = @param)'), permitindo
        /// combinar AND/OR de forma explícita dentro do grupo, independente dos predicados externos.
        /// </summary>
        public WhereConditionChainBuilder Group(Action<WhereClauseBuilder> action)
        {
            ArgumentNullException.ThrowIfNull(action);

            var groupPredicates = new List<(string Predicate, string Operator)>();
            var groupBuilder = new WhereClauseBuilder(groupPredicates, _parameters, _aliasRegistry, null, _dialect);
            action(groupBuilder);

            if (groupPredicates.Count == 0)
            {
                throw new InvalidOperationException("O grupo de condições (Group) não pode ficar vazio.");
            }

            var groupSql = new System.Text.StringBuilder("(");
            for (int i = 0; i < groupPredicates.Count; i++)
            {
                if (i > 0)
                {
                    groupSql.Append($" {groupPredicates[i].Operator} ");
                }
                groupSql.Append(groupPredicates[i].Predicate);
            }
            groupSql.Append(')');

            AddPredicate(groupSql.ToString());
            return new WhereConditionChainBuilder(this, _onSetDynamicOrderByColumn, string.Empty, _dialect);
        }

        public WhereConditionChainBuilder GreaterThan<TEntity>(Expression<Func<TEntity, object?>> propertySelector, object value)
        {
            string alias = _aliasRegistry.GetOrAddAlias(typeof(TEntity));
            var propInfo = ExpressionHelper.GetPropertyInfo(propertySelector);
            string propertyName = propInfo.Name;
            string columnName = ExpressionHelper.GetColumnName(propInfo);

            string paramName = ParameterHelper.GenerateUniqueParameterName(propertyName, _parameters);
            AddPredicate($"{alias}.{columnName} {_dialect.Keywords.GREATER_THAN} {_dialect.GetParameterPrefix()}{paramName}");
            _parameters[paramName] = value;
            return new WhereConditionChainBuilder(this, _onSetDynamicOrderByColumn, $"{alias}.{columnName}", _dialect);
        }

        public WhereConditionChainBuilder LessThan<TEntity>(Expression<Func<TEntity, object?>> propertySelector, object value)
        {
            string alias = _aliasRegistry.GetOrAddAlias(typeof(TEntity));
            var propInfo = ExpressionHelper.GetPropertyInfo(propertySelector);
            string propertyName = propInfo.Name;
            string columnName = ExpressionHelper.GetColumnName(propInfo);

            string paramName = ParameterHelper.GenerateUniqueParameterName(propertyName, _parameters);
            AddPredicate($"{alias}.{columnName} {_dialect.Keywords.LESS_THAN} {_dialect.GetParameterPrefix()}{paramName}");
            _parameters[paramName] = value;
            return new WhereConditionChainBuilder(this, _onSetDynamicOrderByColumn, $"{alias}.{columnName}", _dialect);
        }

        public WhereConditionChainBuilder GreaterThanOrEquals<TEntity>(Expression<Func<TEntity, object?>> propertySelector, object value)
        {
            string alias = _aliasRegistry.GetOrAddAlias(typeof(TEntity));
            var propInfo = ExpressionHelper.GetPropertyInfo(propertySelector);
            string propertyName = propInfo.Name;
            string columnName = ExpressionHelper.GetColumnName(propInfo);

            string paramName = ParameterHelper.GenerateUniqueParameterName(propertyName, _parameters);
            AddPredicate($"{alias}.{columnName} {_dialect.Keywords.GREATER_THAN_OR_EQUALS} {_dialect.GetParameterPrefix()}{paramName}");
            _parameters[paramName] = value;
            return new WhereConditionChainBuilder(this, _onSetDynamicOrderByColumn, $"{alias}.{columnName}", _dialect);
        }

        public WhereConditionChainBuilder LessThanOrEquals<TEntity>(Expression<Func<TEntity, object?>> propertySelector, object value)
        {
            string alias = _aliasRegistry.GetOrAddAlias(typeof(TEntity));
            var propInfo = ExpressionHelper.GetPropertyInfo(propertySelector);
            string propertyName = propInfo.Name;
            string columnName = ExpressionHelper.GetColumnName(propInfo);

            string paramName = ParameterHelper.GenerateUniqueParameterName(propertyName, _parameters);
            AddPredicate($"{alias}.{columnName} {_dialect.Keywords.LESS_THAN_OR_EQUALS} {_dialect.GetParameterPrefix()}{paramName}");
            _parameters[paramName] = value;
            return new WhereConditionChainBuilder(this, _onSetDynamicOrderByColumn, $"{alias}.{columnName}", _dialect);
        }

        public WhereConditionChainBuilder In<TEntity>(Expression<Func<TEntity, object?>> propertySelector, IEnumerable<object> values)
        {
            if (values == null || !values.Any()) throw new ArgumentException("A lista de valores para a cláusula IN não pode ser nula ou vazia.", nameof(values));
            string alias = _aliasRegistry.GetOrAddAlias(typeof(TEntity));
            var propInfo = ExpressionHelper.GetPropertyInfo(propertySelector);
            string propertyName = propInfo.Name;
            string columnName = ExpressionHelper.GetColumnName(propInfo);

            var parameterNames = new List<string>();
            int i = 0;
            foreach (var val in values)
            {
                string paramName = ParameterHelper.GenerateUniqueParameterName($"{propertyName}_{i++}", _parameters);
                parameterNames.Add($"{_dialect.GetParameterPrefix()}{paramName}");
                _parameters[paramName] = val;
            }
            AddPredicate($"{alias}.{columnName} {_dialect.Keywords.IN} ({string.Join(", ", parameterNames)})");
            return new WhereConditionChainBuilder(this, _onSetDynamicOrderByColumn, $"{alias}.{columnName}", _dialect);
        }

        public WhereConditionChainBuilder IsNull<TEntity>(Expression<Func<TEntity, object?>> propertySelector)
        {
            string alias = _aliasRegistry.GetOrAddAlias(typeof(TEntity));
            var propInfo = ExpressionHelper.GetPropertyInfo(propertySelector);
            string columnName = ExpressionHelper.GetColumnName(propInfo);

            AddPredicate($"{alias}.{columnName} {_dialect.Keywords.IS} {_dialect.Keywords.NULL}");
            return new WhereConditionChainBuilder(this, _onSetDynamicOrderByColumn, $"{alias}.{columnName}", _dialect);
        }

        public WhereConditionChainBuilder IsNotNull<TEntity>(Expression<Func<TEntity, object?>> propertySelector)
        {
            string alias = _aliasRegistry.GetOrAddAlias(typeof(TEntity));
            var propInfo = ExpressionHelper.GetPropertyInfo(propertySelector);
            string columnName = ExpressionHelper.GetColumnName(propInfo);

            AddPredicate($"{alias}.{columnName} {_dialect.Keywords.IS} {_dialect.Keywords.NOT_NULL}");
            return new WhereConditionChainBuilder(this, _onSetDynamicOrderByColumn, $"{alias}.{columnName}", _dialect);
        }

        // Este método OrderBy é um aviso, pois o OrderBy real deve ser no QueryBuilder principal.
        public WhereClauseBuilder OrderBy(string orderByClause)
        {
            Console.WriteLine($"Warning: OrderBy('{orderByClause}') called within Where clause. This is not standard SQL and will be ignored for WHERE clause generation. Use the top-level .OrderBy() instead.");
            return this;
        }
    }
}
