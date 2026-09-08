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
