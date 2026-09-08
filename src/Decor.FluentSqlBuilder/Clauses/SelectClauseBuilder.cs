using System.Linq.Expressions;
using Decor.FluentSqlBuilder.Dialects;
using Decor.FluentSqlBuilder.Helpers;

namespace Decor.FluentSqlBuilder.Clauses
{
    internal enum SelectIntent
    {
        None,
        SpecificColumns,
        AllColumnsOnly,
        AllColumnsWithExclusions
    }

    public class SelectClauseBuilder
    {
        private readonly List<string> _selectColumns;
        private readonly TableAliasRegistry _aliasRegistry;
        private readonly IDialect _dialect;
        private readonly Dictionary<Type, SelectIntent> _selectionIntent = [];
        internal bool _isCountQuery = false;
        internal bool _isDistinct = false;
        private readonly string _fromAlias;

        internal SelectClauseBuilder(List<string> selectColumns, TableAliasRegistry aliasRegistry, IDialect dialect, string fromAlias)
        {
            _selectColumns = selectColumns;
            _aliasRegistry = aliasRegistry;
            _dialect = dialect;
            _fromAlias = fromAlias;
        }

        /// <summary>
        /// Marca a seleção para retornar apenas linhas distintas (SELECT DISTINCT).
        /// </summary>
        public SelectClauseBuilder Distinct()
        {
            _isDistinct = true;
            return this;
        }

        /// <summary>
        /// Seleciona uma expressão de coluna literal (ex: "effective.PermissionCode"), útil ao
        /// projetar colunas de uma subconsulta (FROM) que não correspondem a uma entidade mapeada.
        /// </summary>
        public SelectClauseBuilder Column(string rawExpression)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(rawExpression);
            _selectColumns.Add(rawExpression);
            return this;
        }

        /// <summary>
        /// Seleciona todas as colunas mapeadas da entidade.
        /// Opcionalmente, pode usar o wildcard `*` em vez de listar as colunas explicitamente.
        /// </summary>
        /// <typeparam name="TEntity">O tipo da entidade.</typeparam>
        /// <param name="explicitColumns">Se 'true', lista as colunas explicitamente. Se 'false', gera `alias.*`.</param>
        public SelectClauseBuilder AllColumns<TEntity>(bool explicitColumns)
        {
            Type entityType = typeof(TEntity);
            if (_selectionIntent.TryGetValue(entityType, out SelectIntent value) && value != SelectIntent.None)
            {
                throw new InvalidOperationException($"Não é possível usar AllColumns com outra seleção de colunas para o mesmo tipo ('{entityType.Name}').");
            }

            string alias = _aliasRegistry.GetOrAddAlias(entityType);

            if (explicitColumns)
            {
                var mappableProperties = ParameterHelper.GetMappableProperties(entityType);
                foreach (var p in mappableProperties)
                {
                    string colExpr = p.ColumnName != p.PropertyName
                        ? $"{alias}.{p.ColumnName} {_dialect.Keywords.AS} {p.PropertyName}"
                        : $"{alias}.{p.ColumnName}";
                    _selectColumns.Add(colExpr);
                }
            }
            else
            {
                _selectColumns.Add($"{alias}.{_dialect.Keywords.ASTERISK}");
            }

            _selectionIntent[entityType] = SelectIntent.AllColumnsOnly;

            return this;
        }

        /// <summary>
        /// Seleciona todas as colunas da entidade especificada, listando-as explicitamente (comportamento padrão).
        /// </summary>
        /// <typeparam name="TEntity">O tipo da entidade.</typeparam>
        public SelectClauseBuilder AllColumns<TEntity>()
        {
            // O método sem parâmetros agora chama a nova versão com 'true'
            return AllColumns<TEntity>(explicitColumns: false);
        }

        // NOVO: Select(s => s.AllColumns()) -> gera 'SELECT alias.*' para a tabela FROM
        public SelectClauseBuilder AllColumns()
        {
            if (_selectionIntent.Count != 0)
            {
                throw new InvalidOperationException("Não é possível usar AllColumns() com outras seleções de colunas.");
            }
            if (string.IsNullOrEmpty(_fromAlias))
            {
                throw new InvalidOperationException("A cláusula From deve ser especificada antes de AllColumns().");
            }

            _selectColumns.Add($"{_fromAlias}.{_dialect.Keywords.ASTERISK}");
            _selectionIntent[typeof(object)] = SelectIntent.AllColumnsOnly; // Usa um tipo genérico para marcar
            return this;
        }

        public SelectClauseBuilder Except<TEntity>(params Expression<Func<TEntity, object?>>[] expressions)
        {
            Type entityType = typeof(TEntity);
            string alias = _aliasRegistry.GetOrAddAlias(entityType);

            var excludedPropertyNames = expressions
                .Select(ExpressionHelper.GetPropertyName)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (!_selectionIntent.TryGetValue(entityType, out SelectIntent value) || value == SelectIntent.None)
            {
                var mappableProperties = ParameterHelper.GetMappableProperties(entityType);

                foreach (var p in mappableProperties.Where(p => !excludedPropertyNames.Contains(p.PropertyName)))
                {
                    string colExpr = p.ColumnName != p.PropertyName
                        ? $"{alias}.{p.ColumnName} {_dialect.Keywords.AS} {p.PropertyName}"
                        : $"{alias}.{p.ColumnName}";
                    _selectColumns.Add(colExpr);
                }
            }
            else if (value == SelectIntent.AllColumnsOnly)
            {
                _selectColumns.RemoveAll(column =>
                    excludedPropertyNames.Any(propertyName =>
                        column.Equals($"{alias}.{propertyName}", StringComparison.OrdinalIgnoreCase)));
            }
            else
            {
                throw new InvalidOperationException(
                    $"Não é possível usar Except e AllColumns ou Columns para o mesmo tipo de entidade ('{entityType.Name}').");
            }

            _selectionIntent[entityType] = SelectIntent.AllColumnsWithExclusions;

            return this;
        }

        public SelectClauseBuilder Columns<TEntity>(params Expression<Func<TEntity, object?>>[] expressions)
        {
            Type entityType = typeof(TEntity);
            if (!_selectionIntent.TryGetValue(entityType, out SelectIntent value) || value == SelectIntent.None)
            {
                string alias = _aliasRegistry.GetOrAddAlias(entityType);

                foreach (var expr in expressions)
                {
                    var propInfo = ExpressionHelper.GetPropertyInfo(expr);
                    string propName = propInfo.Name;
                    string colName = ExpressionHelper.GetColumnName(propInfo);
                    string colExpr = colName != propName
                        ? $"{alias}.{colName} {_dialect.Keywords.AS} {propName}"
                        : $"{alias}.{colName}";
                    _selectColumns.Add(colExpr);
                }
                _selectionIntent[entityType] = SelectIntent.SpecificColumns;
            }
            else
            {
                throw new InvalidOperationException($"Não é possível usar Columns e AllColumns ou Except para o mesmo tipo de entidade ('{entityType.Name}').");
            }

            return this;
        }

        public SelectClauseBuilder Count()
        {
            if (_selectColumns.Count != 0)
            {
                throw new InvalidOperationException("Não é possível usar Count() com outras colunas selecionadas.");
            }
            _selectColumns.Add(_dialect.Functions.Count(_dialect.Keywords.ASTERISK));
            _isCountQuery = true;
            return this;
        }

        public SelectClauseBuilder Count(byte value)
        {
            if (value != 1)
            {
                throw new ArgumentException("O método Count(byte) suporta apenas o valor '1' para gerar COUNT(1).", nameof(value));
            }

            if (_selectColumns.Count != 0)
            {
                throw new InvalidOperationException("Não é possível usar Count() com outras colunas selecionadas.");
            }
            _selectColumns.Add(_dialect.Functions.Count(value.ToString()));
            _isCountQuery = true;
            return this;
        }

        public SelectClauseBuilder Count<TEntity>(Expression<Func<TEntity, object?>> expression)
        {
            return Count(expression, false);
        }

        public SelectClauseBuilder Count<TEntity>(Expression<Func<TEntity, object?>> expression, bool isDistinct)
        {
            if (_selectColumns.Count != 0)
            {
                throw new InvalidOperationException("Não é possível usar Count() com outras colunas selecionadas.");
            }

            Type entityType = typeof(TEntity);
            string alias = _aliasRegistry.GetOrAddAlias(entityType);
            string columnName = ExpressionHelper.GetColumnName(expression);

            _selectColumns.Add(_dialect.Functions.Count($"{alias}.{columnName}", isDistinct));
            _isCountQuery = true;
            return this;
        }
    }
}