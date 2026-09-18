using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Reflection;
using System.Text;
using Decor.FluentSqlBuilder.Dialects;
using Decor.FluentSqlBuilder.Helpers;

namespace Decor.FluentSqlBuilder.Statements
{
    /// <summary>
    /// Construtor para declarações SQL INSERT.
    /// </summary>
    public class InsertBuilder
    {
        private readonly StringBuilder _sqlBuilder = new();
        private readonly Dictionary<string, object?> _parameters = [];

        private readonly TableAliasRegistry _aliasRegistry;
        private readonly FluentCommandBuilder _rootCommandBuilder;
        private readonly IDialect _dialect;
        private readonly SqlKeywords _keywords;

        private string _intoTable = "";
        private Type? _intoEntityType;
        private readonly List<string> _insertColumns = [];
        private readonly List<string> _insertParameterNames = [];
        private System.Collections.IEnumerable? _bulkEntities;
        private bool _returningGeneratedId;

        // Construtor interno, chamado pelo FluentCommandBuilder
        internal InsertBuilder(FluentCommandBuilder rootCommandBuilder, TableAliasRegistry aliasRegistry, IDialect dialect)
        {
            _rootCommandBuilder = rootCommandBuilder;
            _aliasRegistry = aliasRegistry;
            _dialect = dialect;
            _keywords = new SqlKeywords();
        }

        /// <summary>
        /// Define a tabela na qual os registros serão inseridos.
        /// </summary>
        /// <typeparam name="TEntity">O tipo da entidade que representa a tabela.</typeparam>
        /// <returns>O InsertBuilder para encadeamento.</returns>
        private InsertBuilder ConfigureTable<TEntity>()
        {
            Type entityType = typeof(TEntity);
            string tableName = _aliasRegistry.GetTableName(entityType);

            _intoTable = tableName;
            _intoEntityType = entityType;
            return this;
        }

        /// <summary>
        /// Configura a inserção de uma única entidade, inferindo a tabela a partir do tipo da entidade.
        /// </summary>
        /// <typeparam name="TEntity">O tipo da entidade a ser inserida.</typeparam>
        /// <param name="entity">A instância da entidade contendo os valores a serem inseridos.</param>
        /// <returns>O InsertBuilder para encadeamento.</returns>
        public InsertBuilder Entity<TEntity>(TEntity entity)
        {
            return ConfigureTable<TEntity>().ConfigureEntity(entity);
        }

        /// <summary>
        /// Configura a inserção em lote, inferindo a tabela a partir do tipo dos itens da coleção.
        /// </summary>
        /// <typeparam name="TEntity">O tipo das entidades a serem inseridas.</typeparam>
        /// <param name="entities">A coleção de entidades contendo os valores a serem inseridos.</param>
        /// <returns>O InsertBuilder para encadeamento.</returns>
        public InsertBuilder Entities<TEntity>(IEnumerable<TEntity> entities)
        {
            return ConfigureTable<TEntity>().ConfigureBatch(entities);
        }

        /// <summary>
        /// Especifica as colunas a serem inseridas e seus valores, usando uma entidade.
        /// As propriedades da entidade serão mapeadas para colunas e parâmetros.
        /// </summary>
        /// <typeparam name="TEntity">O tipo da entidade a ser inserida.</typeparam>
        /// <param name="entity">A instância da entidade contendo os valores a serem inseridos.</param>
        /// <returns>O InsertBuilder para encadeamento.</returns>
        private InsertBuilder ConfigureEntity<TEntity>(TEntity entity)
        {
            Type entityType = typeof(TEntity);
            foreach (var (prop, propertyName, columnName) in ParameterHelper.GetMappableProperties(entityType, _aliasRegistry))
            {
                if (prop.PropertyType.IsClass && prop.PropertyType != typeof(string))
                {
                    continue;
                }
                // Chaves primárias são geradas pelo banco para as entidades do Decor.
                if (prop.GetCustomAttribute<KeyAttribute>() != null)
                {
                    continue;
                }

                _insertColumns.Add(columnName);
                _insertParameterNames.Add($"{_dialect.GetParameterPrefix()}{propertyName}");

                _parameters.Add(propertyName, prop.GetValue(entity));
            }
            return this;
        }

        /// <summary>
        /// Especifica as colunas a serem inseridas com base numa coleção de entidades, para inserção em lote.
        /// A mesma instrução SQL é reaproveitada pelo Dapper uma vez por item da coleção (multi-exec).
        /// A mesma instrução SQL é reaproveitada pelo Dapper uma vez por item da coleção (multi-exec).
        /// </summary>
        /// <typeparam name="TEntity">O tipo da entidade a ser inserida.</typeparam>
        /// <param name="entities">A coleção de entidades contendo os valores a serem inseridos.</param>
        /// <returns>O InsertBuilder para encadeamento.</returns>
        private InsertBuilder ConfigureBatch<TEntity>(IEnumerable<TEntity> entities)
        {
            ArgumentNullException.ThrowIfNull(entities);

            Type entityType = typeof(TEntity);
            var entityList = entities as IReadOnlyCollection<TEntity> ?? entities.ToList();
            if (entityList.Count == 0)
            {
                throw new ArgumentException("A coleção de entidades para inserção em lote não pode ser vazia.", nameof(entities));
            }
            if (_returningGeneratedId)
            {
                throw new InvalidOperationException($"{nameof(ReturningGeneratedId)}() não é suportado para inserção em lote.");
            }

            foreach (var (prop, propertyName, columnName) in ParameterHelper.GetMappableProperties(entityType, _aliasRegistry))
            {
                if (prop.PropertyType.IsClass && prop.PropertyType != typeof(string))
                {
                    continue;
                }
                // Chaves primárias são geradas pelo banco para as entidades do Decor.
                if (prop.GetCustomAttribute<KeyAttribute>() != null)
                {
                    continue;
                }

                _insertColumns.Add(columnName);
                _insertParameterNames.Add($"{_dialect.GetParameterPrefix()}{propertyName}");
            }

            _bulkEntities = entityList;
            return this;
        }

        /// <summary>
        /// Anexa, após o INSERT, a instrução do dialeto que recupera o identificador autoincrementado
        /// gerado (ex: 'SELECT LAST_INSERT_ID()'), para leitura via Dapper QuerySingleAsync na mesma
        /// conexão/transação. Não é suportado para inserção em lote (<see cref="BuildBatch"/>).
        /// </summary>
        public InsertBuilder ReturningGeneratedId()
        {
            if (_bulkEntities != null)
            {
                throw new InvalidOperationException($"{nameof(ReturningGeneratedId)}() não é suportado para inserção em lote.");
            }
            _returningGeneratedId = true;
            return this;
        }

        /// <summary>
        /// Constrói a declaração SQL INSERT e seus parâmetros para uma única entidade.
        /// </summary>
        /// <returns>Uma tupla contendo a string SQL e o dicionário de parâmetros.</returns>
        public (string Sql, Dictionary<string, object?> Parameters) Build()
        {
            if (_bulkEntities != null)
            {
                throw new InvalidOperationException($"Esta instrução foi configurada para inserção em lote. Use {nameof(BuildBatch)}() em vez de {nameof(Build)}().");
            }

            return (BuildInsertSql(), _parameters);
        }

        /// <summary>
        /// Constrói a declaração SQL INSERT para inserção em lote e a coleção de entidades a executar via Dapper multi-exec.
        /// </summary>
        /// <returns>Uma tupla contendo a string SQL e a coleção de entidades (uma execução por item).</returns>
        public (string Sql, System.Collections.IEnumerable Parameters) BuildBatch()
        {
            if (_bulkEntities == null)
            {
                throw new InvalidOperationException("Nenhuma coleção foi especificada para inserção em lote.");
            }

            return (BuildInsertSql(), _bulkEntities);
        }

        private string BuildInsertSql()
        {
            if (string.IsNullOrEmpty(_intoTable))
            {
                throw new InvalidOperationException("A tabela de destino deve ser especificada usando Entity ou Entities.");
            }
            if (_insertColumns.Count == 0)
            {
                throw new InvalidOperationException("Nenhuma coluna/valor foi especificada para a inserção.");
            }

            _sqlBuilder.Clear();
            _sqlBuilder.Append($"{_keywords.INSERT} {_keywords.INTO} {_intoTable} ({string.Join(", ", _insertColumns)})\n");
            _sqlBuilder.Append($"{_keywords.VALUES} ({string.Join(", ", _insertParameterNames)});\n");

            if (_returningGeneratedId)
            {
                _sqlBuilder.Append($"{_dialect.BuildLastInsertIdSelect()};\n");
            }

            return _sqlBuilder.ToString();
        }

        // Método interno para delegação de WhereClauseBuilder (se INSERT tivesse WHERE)
        // Não há WHERE em INSERT, mas se houvesse, precisaria de GetAliasForType
        internal string GetAliasForType(Type entityType)
        {
            return _rootCommandBuilder.GetAliasForType(entityType);
        }

        // Não há ORDER BY dinâmico para INSERT. Implementação vazia.
        internal void SetDynamicOrderByColumn(string columnWithDirection)
        {
            // INSERT não tem ORDER BY dinâmico. Ignorar.
        }
    }
}
