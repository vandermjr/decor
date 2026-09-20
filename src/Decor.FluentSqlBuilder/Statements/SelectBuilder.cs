using System.Linq.Expressions;
using System.Text;
using Decor.FluentSqlBuilder.Clauses;
using Decor.FluentSqlBuilder.Dialects;
using Decor.FluentSqlBuilder.Helpers;

namespace Decor.FluentSqlBuilder.Statements
{
    public class SelectBuilder : ISqlSource
    {
        private readonly FluentCommandBuilder _rootCommandBuilder;
        private readonly IDialect _dialect;
        private readonly TableAliasRegistry _aliasRegistry;

        private readonly StringBuilder _sqlBuilder = new();
        private readonly Dictionary<string, object?> _parameters = [];

        // Estado da query SELECT (migrado de FluentCommandBuilder)
        private string _fromTable = "";
        private string _fromAlias = "";
        private Type? _fromEntityType;
        private readonly List<string> _selectColumns = [];
        private readonly List<(string Predicate, string Operator)> _predicatesWithOperators = [];
        private readonly List<(string JoinKeyword, string Clause)> _joinClauses = [];
        private readonly List<OrderDefinition> _structuredOrderings = [];
        private string _legacyOrderByClause = "";
        private uint? _takeCount = null;
        private uint? _dynamicTakeCount = null;
        private uint? _skipCount = null;
        private bool _isCountQuery = false;
        private bool _isDistinct = false;
        private bool _forUpdate = false;
        private SelectClauseBuilder? _selectClauseBuilder;

        internal SelectBuilder(FluentCommandBuilder rootCommandBuilder, TableAliasRegistry aliasRegistry, IDialect dialect)
        {
            _rootCommandBuilder = rootCommandBuilder;
            _aliasRegistry = aliasRegistry;
            _dialect = dialect;
        }

        public SelectBuilder Select(Action<SelectClauseBuilder> action)
        {
            _selectClauseBuilder = null;
            _selectColumns.Clear();
            ConfigureSelect(action);

            return this;
        }

        private void ConfigureSelect(Action<SelectClauseBuilder> action)
        {
            ArgumentNullException.ThrowIfNull(action);

            _selectClauseBuilder ??= new SelectClauseBuilder(_selectColumns, _aliasRegistry, _dialect, _fromAlias);
            action(_selectClauseBuilder);

            // Consulta agregada: COUNT/SUM ignoram ORDER BY/paginação e seguem o mesmo padrão de seleção especial.
            _isCountQuery = _selectColumns.Count == 1 &&
                (_selectColumns[0].StartsWith(_dialect.Keywords.COUNT, StringComparison.OrdinalIgnoreCase) ||
                 _selectColumns[0].StartsWith(_dialect.Keywords.SUM, StringComparison.OrdinalIgnoreCase));
            _isDistinct = _selectClauseBuilder._isDistinct;
        }

        public SelectBuilder WithColumns<TEntity>(params Expression<Func<TEntity, object?>>[] expressions)
        {
            ConfigureSelect(select => select.WithColumns(expressions));
            return this;
        }

        public SelectBuilder AllColumns<TEntity>(bool explicitColumns)
        {
            ConfigureSelect(select => select.AllColumns<TEntity>(explicitColumns));
            return this;
        }

        public SelectBuilder AllColumns<TEntity>()
        {
            ConfigureSelect(select => select.AllColumns<TEntity>());
            return this;
        }

        public SelectBuilder AllColumns()
        {
            ConfigureSelect(select => select.AllColumns());
            return this;
        }

        public SelectBuilder ExceptColumns<TEntity>(params Expression<Func<TEntity, object?>>[] expressions)
        {
            ConfigureSelect(select => select.ExceptColumns(expressions));
            return this;
        }

        public SelectBuilder Count()
        {
            ConfigureSelect(select => select.Count());
            return this;
        }

        public SelectBuilder Count(byte value)
        {
            ConfigureSelect(select => select.Count(value));
            return this;
        }

        public SelectBuilder Count<TEntity>(Expression<Func<TEntity, object?>> expression)
        {
            ConfigureSelect(select => select.Count(expression));
            return this;
        }

        public SelectBuilder Count<TEntity>(Expression<Func<TEntity, object?>> expression, bool isDistinct)
        {
            ConfigureSelect(select => select.Count(expression, isDistinct));
            return this;
        }

        public SelectBuilder Sum<TEntity>(Expression<Func<TEntity, object?>> expression)
        {
            ConfigureSelect(select => select.Sum(expression));
            return this;
        }

        public SelectBuilder Sum<TEntity>(Expression<Func<TEntity, object?>> expression, string? resultAlias)
        {
            ConfigureSelect(select => select.Sum(expression, resultAlias));
            return this;
        }

        public SelectBuilder Sum(string rawExpression)
        {
            ConfigureSelect(select => select.Sum(rawExpression));
            return this;
        }

        public SelectBuilder Sum(string rawExpression, string? resultAlias)
        {
            ConfigureSelect(select => select.Sum(rawExpression, resultAlias));
            return this;
        }

        public SelectBuilder Distinct()
        {
            ConfigureSelect(select => select.Distinct());
            return this;
        }

        public SelectBuilder Column(string rawExpression)
        {
            ConfigureSelect(select => select.Column(rawExpression));

            return this;
        }

        public SelectBuilder From<TEntity>()
        {
            Type entityType = typeof(TEntity);
            string alias = _aliasRegistry.GetOrAddAlias(entityType);
            string tableName = _aliasRegistry.GetTableName(entityType);

            _fromTable = tableName;
            _fromAlias = alias;
            _fromEntityType = entityType;
            return this;
        }

        /// <summary>
        /// Define uma subconsulta componível (SELECT ou UNION) como fonte do FROM, com o alias informado.
        /// Não aplica a ordenação padrão por chave primária, já que a fonte não é uma entidade mapeada.
        /// </summary>
        public SelectBuilder FromSubquery(ISqlSource subquery, string alias)
        {
            ArgumentNullException.ThrowIfNull(subquery);
            ArgumentException.ThrowIfNullOrWhiteSpace(alias);

            var (sql, parameters) = subquery.BuildInline();
            ParameterHelper.MergeParameters(_parameters, parameters);

            _fromTable = $"({sql})";
            _fromAlias = alias;
            _fromEntityType = null;
            return this;
        }

        /// <summary>
        /// Combina esta instrução com outra fonte componível usando UNION.
        /// </summary>
        public UnionSelectBuilder Union(ISqlSource other) => new UnionSelectBuilder(_dialect, this).Union(other);

        /// <summary>
        /// Combina esta instrução com outra fonte componível usando UNION ALL.
        /// </summary>
        public UnionSelectBuilder UnionAll(ISqlSource other) => new UnionSelectBuilder(_dialect, this).UnionAll(other);

        public SelectBuilder Join(Action<JoinClauseBuilder> action)
        {
            var joinBuilder = new JoinClauseBuilder(_joinClauses, _parameters, _aliasRegistry, _dialect);
            action(joinBuilder);
            return this;
        }

        public SelectBuilder Where(Action<WhereClauseBuilder> action)
        {
            var whereBuilder = new WhereClauseBuilder(_predicatesWithOperators, _parameters, _aliasRegistry, AddStructuredOrdering, _dialect, SetDynamicTake);
            action(whereBuilder);
            return this;
        }

        public SelectBuilder OrderBy(string orderByClause)
        {
            _legacyOrderByClause = orderByClause;
            return this;
        }

        public SelectBuilder OrderBy(Action<OrderByBuilder> action)
        {
            ArgumentNullException.ThrowIfNull(action);
            var orderByBuilder = new OrderByBuilder(AddStructuredOrdering);
            action(orderByBuilder);
            return this;
        }

        internal void AddStructuredOrdering(OrderDefinition orderDefinition)
        {
            ArgumentNullException.ThrowIfNull(orderDefinition);

            if (orderDefinition.IsRelevance)
            {
                var existingRelevance = _structuredOrderings.FirstOrDefault(o => o.IsRelevance);
                if (existingRelevance is not null)
                {
                    _structuredOrderings.Remove(existingRelevance);
                }

                _structuredOrderings.Insert(0, orderDefinition);
                return;
            }

            _structuredOrderings.Add(orderDefinition);
        }

        private string BuildStructuredOrderSql(OrderDefinition orderDefinition)
        {
            if (orderDefinition.IsRelevance)
            {
                string directionSql = orderDefinition.Direction == SortDirection.Ascending
                    ? _dialect.Keywords.ASC
                    : _dialect.Keywords.DESC;

                return $"{orderDefinition.RelevanceExpression} {directionSql}";
            }

            string alias = _aliasRegistry.GetOrAddAlias(orderDefinition.EntityType!);
            var propertyInfo = ExpressionHelper.GetPropertyInfo(orderDefinition.PropertySelector!);
            string columnName = ExpressionHelper.GetColumnName(propertyInfo, _aliasRegistry);
            string directionSql2 = orderDefinition.Direction == SortDirection.Ascending
                ? _dialect.Keywords.ASC
                : _dialect.Keywords.DESC;

            return $"{alias}.{columnName} {directionSql2}";
        }

        internal void SetDynamicTake(uint count) => _dynamicTakeCount = count;

        public SelectBuilder Take(uint count)
        {
            if (count == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count), "O valor de take deve ser maior que zero.");
            }
            _takeCount = count;
            return this;
        }

        public SelectBuilder Skip(uint count)
        {
            _skipCount = count;
            return this;
        }

        /// <summary>
        /// Anexa um hint de bloqueio pessimista (ex: 'FOR UPDATE') ao final do SELECT.
        /// Requer que a consulta seja executada dentro de uma transação aberta.
        /// </summary>
        public SelectBuilder ForUpdate()
        {
            _forUpdate = true;
            return this;
        }

        public (string Sql, Dictionary<string, object?> Parameters) Build()
        {
            return ($"{BuildSql(includeTail: true)};\n", _parameters);
        }

        /// <summary>
        /// Constrói a instrução sem o terminador ';' nem cláusulas de ORDER BY/paginação/lock hint,
        /// para uso como subconsulta (FROM) ou membro de UNION. Ver <see cref="ISqlSource"/>.
        /// </summary>
        public (string Sql, Dictionary<string, object?> Parameters) BuildInline()
        {
            return (BuildSql(includeTail: false), _parameters);
        }

        private string BuildSql(bool includeTail)
        {
            if (string.IsNullOrEmpty(_fromTable))
            {
                throw new InvalidOperationException($"A cláusula {_dialect.Keywords.FROM} deve ser especificada para uma declaração {_dialect.Keywords.SELECT}.");
            }
            if (_selectColumns.Count == 0)
            {
                throw new InvalidOperationException($"Nenhuma coluna foi especificada para a seleção. Use {_dialect.Keywords.SELECT}(s => s.WithColumns<TEntity>(...)) ou {_dialect.Keywords.SELECT}(s => s.AllColumns<TEntity>()).");
            }

            _sqlBuilder.Clear();

            // SELECT clause
            _sqlBuilder.Append(_isDistinct
                ? $"{_dialect.Keywords.SELECT} {_dialect.Keywords.DISTINCT}\n"
                : $"{_dialect.Keywords.SELECT}\n");
            _sqlBuilder.Append(string.Join(",\n", _selectColumns.Select(col => $"    {col}")));
            _sqlBuilder.Append('\n');

            // FROM clause
            _sqlBuilder.Append($"{_dialect.Keywords.FROM}\n    {_fromTable} {_dialect.Keywords.AS} {_fromAlias}\n");

            // JOIN clauses
            foreach (var join in _joinClauses)
            {
                _sqlBuilder.Append($"{join.JoinKeyword}\n    {join.Clause}\n");
            }

            // WHERE clause
            if (_predicatesWithOperators.Count > 0)
            {
                _sqlBuilder.Append($"{_dialect.Keywords.WHERE} ");
                for (int i = 0; i < _predicatesWithOperators.Count; i++)
                {
                    var item = _predicatesWithOperators[i];
                    if (i > 0)
                    {
                        _sqlBuilder.Append($" {item.Operator} ");
                    }
                    _sqlBuilder.Append(item.Predicate);
                }
                _sqlBuilder.Append('\n');
            }

            // Subconsultas/membros de UNION não recebem ORDER BY, paginação ou lock hint próprios.
            if (!includeTail)
            {
                return _sqlBuilder.ToString();
            }

            // CORREÇÃO: Ignora ORDER BY e paginação se for uma query de contagem
            if (!_isCountQuery)
            {
                // ORDER BY clause
                if (_structuredOrderings.Count > 0)
                {
                    _sqlBuilder.Append($"{_dialect.Keywords.ORDER_BY}\n    {string.Join(",\n    ", _structuredOrderings.Select(BuildStructuredOrderSql))}\n");
                }
                else if (!string.IsNullOrEmpty(_legacyOrderByClause))
                {
                    _sqlBuilder.Append($"{_dialect.Keywords.ORDER_BY}\n    {_legacyOrderByClause}\n");
                }
                else
                {
                    if (_fromEntityType != null)
                    {
                        string? pkColumnName = ParameterHelper.GetPrimaryKeyColumnName(_fromEntityType, _aliasRegistry);
                        if (!string.IsNullOrEmpty(pkColumnName))
                        {
                            _sqlBuilder.Append($"{_dialect.Keywords.ORDER_BY}\n    {_fromAlias}.{pkColumnName} {_dialect.Keywords.ASC}\n");
                        }
                        else
                        {
                            throw new InvalidOperationException($"Não foi possível determinar a chave primária para o tipo '{_fromEntityType.Name}' para ordenação padrão. Por favor, especifique uma propriedade com [Key] ou use .OrderBy() explicitamente.");
                        }
                    }
                }

                // Paginação clause
                string paginationClause = _dialect.BuildPagination(_dynamicTakeCount ?? _takeCount, _skipCount);
                if (!string.IsNullOrEmpty(paginationClause))
                {
                    _sqlBuilder.Append(paginationClause);
                }

                if (_forUpdate)
                {
                    if (!string.IsNullOrEmpty(paginationClause))
                    {
                        _sqlBuilder.Append('\n');
                    }
                    _sqlBuilder.Append(_dialect.BuildLockHint());
                }
            }
            else if (_forUpdate)
            {
                _sqlBuilder.Append(_dialect.BuildLockHint());
            }

            return _sqlBuilder.ToString();
        }

        internal string GetAliasForType(Type entityType)
        {
            return _rootCommandBuilder.GetAliasForType(entityType);
        }
    }
}