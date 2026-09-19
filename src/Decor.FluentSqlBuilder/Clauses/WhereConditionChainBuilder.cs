using System.Linq.Expressions;
using Decor.FluentSqlBuilder.Dialects;

namespace Decor.FluentSqlBuilder.Clauses
{
    public class WhereConditionChainBuilder
    {
        private readonly WhereClauseBuilder _parentWhereBuilder;
        private readonly Action<string>? _onSetDynamicOrderByColumn;
        private readonly string _columnForOrderBy; // Esta string pode ser vazia se nenhum filtro foi aplicado
        private readonly IDialect _dialect;
        private readonly string? _relevanceExpression;

        /// <summary>
        /// Indica se a condição que gerou este builder foi uma busca por ID.
        /// Usado principalmente pelo WithDynamicSearchFilter.
        /// </summary>
        public bool IsIdSearch { get; internal set; }

        internal WhereConditionChainBuilder(WhereClauseBuilder parentWhereBuilder, Action<string>? onSetDynamicOrderByColumn, string columnForOrderBy, IDialect dialect, string? relevanceExpression = null)
        {
            _parentWhereBuilder = parentWhereBuilder;
            _onSetDynamicOrderByColumn = onSetDynamicOrderByColumn;
            _columnForOrderBy = columnForOrderBy;
            _dialect = dialect;
            _relevanceExpression = relevanceExpression;
            IsIdSearch = false; // Valor padrão
        }

        public WhereConditionChainBuilder Equals<TEntity>(Expression<Func<TEntity, object?>> propertySelector, object? value)
            => _parentWhereBuilder.Equals(propertySelector, value);

        public WhereConditionChainBuilder NotEquals<TEntity>(Expression<Func<TEntity, object?>> propertySelector, object? value)
            => _parentWhereBuilder.NotEquals(propertySelector, value);

        public WhereConditionChainBuilder Contains<TEntity>(Expression<Func<TEntity, object?>> propertySelector, string value)
            => _parentWhereBuilder.Contains(propertySelector, value);

        public WhereConditionChainBuilder BeginsWith<TEntity>(Expression<Func<TEntity, object?>> propertySelector, string value)
            => _parentWhereBuilder.BeginsWith(propertySelector, value);

        public WhereConditionChainBuilder EndsWith<TEntity>(Expression<Func<TEntity, object?>> propertySelector, string value)
            => _parentWhereBuilder.EndsWith(propertySelector, value);

        public WhereConditionChainBuilder WithDynamicSearchFilter<TIdEntity, TStringEntity>(
            string? arg,
            Expression<Func<TIdEntity, object?>> idPropertySelector,
            Expression<Func<TStringEntity, object?>> stringPropertySelector)
            where TIdEntity : class
            where TStringEntity : class
            => _parentWhereBuilder.WithDynamicSearchFilter(arg, idPropertySelector, stringPropertySelector);

        public WhereConditionChainBuilder FullText<TEntity>(
            string searchTerm,
            params Expression<Func<TEntity, object?>>[] propertySelectors)
            => _parentWhereBuilder.FullText(searchTerm, propertySelectors);

        public WhereConditionChainBuilder FullText<TEntity>(
            string searchTerm,
            FullTextSearchMode mode,
            params Expression<Func<TEntity, object?>>[] propertySelectors)
            => _parentWhereBuilder.FullText(searchTerm, mode, propertySelectors);

        public WhereConditionChainBuilder WithDynamicFullTextSearch<TIdEntity, TStringEntity>(
            string? arg,
            Expression<Func<TIdEntity, object?>> idPropertySelector,
            params Expression<Func<TStringEntity, object?>>[] textPropertySelectors)
            where TIdEntity : class
            where TStringEntity : class
            => _parentWhereBuilder.WithDynamicFullTextSearch(arg, idPropertySelector, textPropertySelectors);

        public WhereConditionChainBuilder GreaterThan<TEntity>(Expression<Func<TEntity, object?>> propertySelector, object value)
            => _parentWhereBuilder.GreaterThan(propertySelector, value);

        public WhereConditionChainBuilder LessThan<TEntity>(Expression<Func<TEntity, object?>> propertySelector, object value)
            => _parentWhereBuilder.LessThan(propertySelector, value);

        public WhereConditionChainBuilder GreaterThanOrEquals<TEntity>(Expression<Func<TEntity, object?>> propertySelector, object value)
            => _parentWhereBuilder.GreaterThanOrEquals(propertySelector, value);

        public WhereConditionChainBuilder LessThanOrEquals<TEntity>(Expression<Func<TEntity, object?>> propertySelector, object value)
            => _parentWhereBuilder.LessThanOrEquals(propertySelector, value);

        public WhereConditionChainBuilder In<TEntity>(Expression<Func<TEntity, object?>> propertySelector, IEnumerable<object> values)
            => _parentWhereBuilder.In(propertySelector, values);

        public WhereConditionChainBuilder IsNull<TEntity>(Expression<Func<TEntity, object?>> propertySelector)
            => _parentWhereBuilder.IsNull(propertySelector);

        public WhereConditionChainBuilder IsNotNull<TEntity>(Expression<Func<TEntity, object?>> propertySelector)
            => _parentWhereBuilder.IsNotNull(propertySelector);

        public WhereConditionChainBuilder Group(Action<WhereClauseBuilder> action)
            => _parentWhereBuilder.Group(action);

        public WhereConditionChainBuilder And()
        {
            _parentWhereBuilder.And();
            return this;
        }

        public WhereConditionChainBuilder Or()
        {
            _parentWhereBuilder.Or();
            return this;
        }

        /// <summary>
        /// Define a ordenação ascendente para a coluna usada na condição anterior.
        /// Só aplica a ordenação se uma coluna válida foi usada na condição.
        /// </summary>
        /// <returns>O WhereClauseBuilder pai para continuar encadeando condições.</returns>
        public WhereClauseBuilder OrderByAscending()
        {
            if (!string.IsNullOrEmpty(_columnForOrderBy)) // NOVO: Só define se a coluna não é vazia
            {
                _onSetDynamicOrderByColumn?.Invoke($"{_columnForOrderBy} {_dialect.Keywords.ASC}");
            }
            return _parentWhereBuilder;
        }

        /// <summary>
        /// Define a ordenação descendente para a coluna usada na condição anterior.
        /// Só aplica a ordenação se uma coluna válida foi usada na condição.
        /// </summary>
        /// <returns>O WhereClauseBuilder pai para continuar encadeando condições.</returns>
        public WhereClauseBuilder OrderByDescending()
        {
            if (!string.IsNullOrEmpty(_columnForOrderBy)) // NOVO: Só define se a coluna não é vazia
            {
                _onSetDynamicOrderByColumn?.Invoke($"{_columnForOrderBy} {_dialect.Keywords.DESC}");
            }
            return _parentWhereBuilder;
        }

        public WhereClauseBuilder OrderByRelevanceDescending()
        {
            if (!string.IsNullOrEmpty(_relevanceExpression))
            {
                _onSetDynamicOrderByColumn?.Invoke($"{_relevanceExpression} {_dialect.Keywords.DESC}");
            }
            else
            {
                OrderByAscending();
            }

            return _parentWhereBuilder;
        }
    }
}
