using System.Text;
using Decor.FluentSqlBuilder.Clauses;
using Decor.FluentSqlBuilder.Dialects;
using Decor.FluentSqlBuilder.Helpers;

namespace Decor.FluentSqlBuilder.Statements
{
    /// <summary>
    /// Construtor para declarações SQL DELETE.
    /// </summary>
    public class DeleteBuilder
    {
        private readonly StringBuilder _sqlBuilder = new();
        private readonly Dictionary<string, object?> _parameters = [];

        private readonly TableAliasRegistry _aliasRegistry;
        private readonly FluentCommandBuilder _rootCommandBuilder;
        private readonly IDialect _dialect;
        private readonly SqlKeywords _keywords;

        internal string _fromTable = "";
        internal string _fromAlias = "";
        private readonly List<(string Predicate, string Operator)> _predicatesWithOperators = [];

        // Construtor interno, chamado pelo FluentCommandBuilder
        internal DeleteBuilder(FluentCommandBuilder rootCommandBuilder, TableAliasRegistry aliasRegistry, IDialect dialect)
        {
            _rootCommandBuilder = rootCommandBuilder;
            _aliasRegistry = aliasRegistry;
            _dialect = dialect;
            _keywords = new SqlKeywords();
        }

        /// <summary>
        /// Define a tabela da qual os registros serão excluídos.
        /// </summary>
        /// <typeparam name="TEntity">O tipo da entidade que representa a tabela.</typeparam>
        /// <returns>O DeleteBuilder para encadeamento.</returns>
        public DeleteBuilder From<TEntity>()
        {
            Type entityType = typeof(TEntity);
            string alias = _aliasRegistry.GetOrAddAlias(entityType);
            string tableName = _aliasRegistry.GetTableName(entityType);

            _fromTable = tableName;
            _fromAlias = alias;
            return this;
        }

        /// <summary>
        /// Adiciona condições WHERE à declaração DELETE.
        /// </summary>
        /// <param name="action">Uma ação para configurar o WhereClauseBuilder.</param>
        /// <returns>O DeleteBuilder para encadeamento.</returns>
        public DeleteBuilder Where(Action<WhereClauseBuilder> action)
        {
            // Passa as funções de delegação para o WhereClauseBuilder
            var whereBuilder = new WhereClauseBuilder(_predicatesWithOperators, _parameters, _aliasRegistry, SetDynamicOrderByColumn, _dialect);
            action(whereBuilder);
            return this;
        }

        /// <summary>
        /// Constrói a declaração SQL DELETE e seus parâmetros.
        /// </summary>
        /// <returns>Uma tupla contendo a string SQL e o dicionário de parâmetros.</returns>
        public (string Sql, Dictionary<string, object?> Parameters) Build()
        {
            if (string.IsNullOrEmpty(_fromTable))
            {
                throw new InvalidOperationException($"A cláusula {_keywords.FROM} deve ser especificada para uma declaração {_keywords.DELETE} {_keywords.FROM}.");
            }
            if (_predicatesWithOperators.Count == 0)
            {
                throw new InvalidOperationException("DELETE exige uma cláusula WHERE.");
            }

            _sqlBuilder.Clear();
            // Sintaxe de DELETE multi-tabela (DELETE alias FROM tabela AS alias ...): o MariaDB não
            // aceita "DELETE FROM tabela AS alias" (alias em DELETE single-table) mas aceita esta forma.
            _sqlBuilder.Append($"{_keywords.DELETE} {_fromAlias} {_keywords.FROM} {_fromTable} {_keywords.AS} {_fromAlias}\n");

            // Cláusula WHERE
            if (_predicatesWithOperators.Count > 0)
            {
                _sqlBuilder.Append($"{_keywords.WHERE} ");
                for (int i = 0; i < _predicatesWithOperators.Count; i++)
                {
                    var item = _predicatesWithOperators[i];
                    if (i > 0)
                    {
                        _sqlBuilder.Append($" {item.Operator} ");
                    }
                    _sqlBuilder.Append(item.Predicate);
                }
                _sqlBuilder.Append(";\n");
            }
            return (_sqlBuilder.ToString(), _parameters);
        }

        // Métodos internos para delegação de WhereClauseBuilder
        internal string GetAliasForType(Type entityType)
        {
            return _rootCommandBuilder.GetAliasForType(entityType);
        }

        // Não há ORDER BY dinâmico para DELETE, mas o WhereClauseBuilder espera a Action.
        // Implementação vazia ou throw se for um cenário inválido.
        internal void SetDynamicOrderByColumn(string columnWithDirection)
        {
            // DELETE não tem ORDER BY dinâmico. Ignorar.
        }
    }
}
