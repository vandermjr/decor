using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using Decor.FluentSqlBuilder.Clauses;
using Decor.FluentSqlBuilder.Dialects;
using Decor.FluentSqlBuilder.Helpers;

namespace Decor.FluentSqlBuilder.Statements
{
    /// <summary>
    /// Construtor para declarações SQL UPDATE.
    /// </summary>
    public class UpdateBuilder
    {
        private readonly StringBuilder _sqlBuilder = new();
        private readonly Dictionary<string, object?> _parameters = [];

        private readonly TableAliasRegistry _aliasRegistry;
        private readonly FluentCommandBuilder _rootCommandBuilder;
        private readonly IDialect _dialect;
        private readonly SqlKeywords _keywords;

        private string _updateTable = "";
        private string _updateAlias = "";
        private readonly List<string> _setClauses = [];
        private readonly HashSet<string> _setColumns = [];
        private readonly List<(string Predicate, string Operator)> _predicatesWithOperators = [];

        // Construtor interno, chamado pelo FluentCommandBuilder
        internal UpdateBuilder(FluentCommandBuilder rootCommandBuilder, TableAliasRegistry aliasRegistry, IDialect dialect)
        {
            _rootCommandBuilder = rootCommandBuilder;
            _aliasRegistry = aliasRegistry;
            _dialect = dialect;
            _keywords = new SqlKeywords();
        }

        /// <summary>
        /// Define a tabela a ser atualizada.
        /// </summary>
        /// <typeparam name="TEntity">O tipo da entidade que representa a tabela.</typeparam>
        /// <returns>O UpdateBuilder para encadeamento.</returns>
        public UpdateBuilder Table<TEntity>()
        {
            Type entityType = typeof(TEntity);
            string alias = _aliasRegistry.GetOrAddAlias(entityType);
            string tableName = _aliasRegistry.GetTableName(entityType);

            _updateTable = tableName;
            _updateAlias = alias;
            return this;
        }

        /// <summary>
        /// Define uma coluna a ser atualizada com um novo valor, usando uma expressão lambda.
        /// </summary>
        /// <typeparam name="TEntity">O tipo da entidade da qual a propriedade é selecionada.</typeparam>
        /// <param name="propertySelector">A expressão lambda que seleciona a propriedade (ex: p => p.Price).</param>
        /// <param name="value">O novo valor para a propriedade.</param>
        /// <returns>O UpdateBuilder para encadeamento.</returns>
        public UpdateBuilder Set<TEntity>(Expression<Func<TEntity, object?>> propertySelector, object? value)
        {
            var propInfo = ExpressionHelper.GetPropertyInfo(propertySelector);
            string propertyName = propInfo.Name;
            string columnName = ExpressionHelper.GetColumnName(propInfo);
            string alias = _aliasRegistry.GetOrAddAlias(typeof(TEntity));

            string paramName = propertyName;

            if (!_setColumns.Add(propertyName))
            {
                throw new InvalidOperationException($"A coluna '{propertyName}' já foi definida na cláusula SET.");
            }

            _setClauses.Add($"{alias}.{columnName} {_keywords.EQUALS} {_dialect.GetParameterPrefix()}{paramName}");
            _parameters[paramName] = value;

            return this;
        }

        /// <summary>
        /// Define as colunas a serem atualizadas com base nas propriedades de uma entidade.
        /// Propriedades com [NotMapped] ou que são chaves primárias ([Key]) são ignoradas.
        /// </summary>
        /// <typeparam name="TEntity">O tipo da entidade.</typeparam>
        /// <param name="entity">A instância da entidade contendo os valores a serem atualizados.</param>
        /// <returns>O UpdateBuilder para encadeamento.</returns>
        public UpdateBuilder Set<TEntity>(TEntity entity)
        {
            Type entityType = typeof(TEntity);
            string alias = _aliasRegistry.GetOrAddAlias(entityType);

            foreach (var (prop, propertyName, columnName) in ParameterHelper.GetMappableProperties(entityType))
            {
                if (prop.PropertyType.IsClass && prop.PropertyType != typeof(string))
                {
                    continue;
                }
                // Ignora propriedades que são chaves primárias ([Key])
                if (prop.GetCustomAttribute<KeyAttribute>() != null)
                {
                    continue;
                }

                if (!_setColumns.Add(propertyName))
                {
                    throw new InvalidOperationException($"A coluna '{propertyName}' já foi definida na cláusula SET.");
                }

                _setClauses.Add($"{alias}.{columnName} {_keywords.EQUALS} {_dialect.GetParameterPrefix()}{propertyName}");
                _parameters[propertyName] = prop.GetValue(entity);
            }
            return this;
        }


        /// <summary>
        /// Adiciona condições WHERE à declaração UPDATE.
        /// </summary>
        /// <param name="action">Uma ação para configurar o WhereClauseBuilder.</param>
        /// <returns>O UpdateBuilder para encadeamento.</returns>
        public UpdateBuilder Where(Action<WhereClauseBuilder> action)
        {
            // Passa as funções de delegação para o WhereClauseBuilder
            var whereBuilder = new WhereClauseBuilder(_predicatesWithOperators, _parameters, _aliasRegistry, SetDynamicOrderByColumn, _dialect);
            action(whereBuilder);
            return this;
        }

        /// <summary>
        /// Constrói a declaração SQL UPDATE e seus parâmetros.
        /// </summary>
        /// <returns>Uma tupla contendo a string SQL e o dicionário de parâmetros.</returns>
        public (string Sql, Dictionary<string, object?> Parameters) Build()
        {
            if (string.IsNullOrEmpty(_updateTable))
            {
                throw new InvalidOperationException($"A tabela a ser atualizada deve ser especificada usando {_keywords.UPDATE}Table<TEntity>().");
            }
            if (_setClauses.Count == 0)
            {
                throw new InvalidOperationException($"Nenhuma coluna/valor foi especificada para atualização. Use {_keywords.SET}<TEntity>(propertySelector, value) ou {_keywords.SET}<TEntity>(entity).");
            }
            if (_predicatesWithOperators.Count == 0)
            {
                throw new InvalidOperationException("UPDATE exige uma cláusula WHERE.");
            }

            _sqlBuilder.Clear();
            _sqlBuilder.Append($"{_keywords.UPDATE} {_updateTable} {_keywords.AS} {_updateAlias}\n");
            _sqlBuilder.Append($"{_keywords.SET} {string.Join(", ", _setClauses)}\n");

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

        // Não há ORDER BY dinâmico para UPDATE. Implementação vazia.
        internal void SetDynamicOrderByColumn(string columnWithDirection)
        {
            // UPDATE não tem ORDER BY dinâmico. Ignorar.
        }
    }
}
