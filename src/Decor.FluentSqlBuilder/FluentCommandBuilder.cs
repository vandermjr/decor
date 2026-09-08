using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;
using Decor.FluentSqlBuilder.Clauses;
using Decor.FluentSqlBuilder.Dialects;
using Decor.FluentSqlBuilder.Dialects.MariaDB;
using Decor.FluentSqlBuilder.Helpers;
using Decor.FluentSqlBuilder.Statements;

namespace Decor.FluentSqlBuilder
{
    /// <summary>
    /// Ponto de entrada principal para iniciar a construção de qualquer declaração SQL (SELECT, INSERT, UPDATE, DELETE).
    /// Gerencia o registro de aliases de entidades e nomes de tabelas.
    /// </summary>
    public class FluentCommandBuilder
    {
        // Instância do dialeto SQL
        private readonly IDialect _dialect;

        // Gerenciador de aliases e tabelas
        private readonly TableAliasRegistry _aliasRegistry = new();

        internal TableAliasRegistry AliasRegistry => _aliasRegistry;

        internal FluentCommandBuilder(IDialect dialect)
        {
            _dialect = dialect ?? throw new ArgumentNullException(nameof(dialect));
        }

        /// <summary>
        /// Inicia uma nova instância do FluentCommandBuilder com o dialeto MariaDB padrão.
        /// </summary>
        /// <returns>Uma nova instância do FluentCommandBuilder.</returns>
        public static FluentCommandBuilder Create()
        {
            return new FluentCommandBuilder(new MariaDBDialect());
        }

        /// <summary>
        /// Inicia uma nova instância do FluentCommandBuilder com um dialeto SQL específico.
        /// </summary>
        /// <param name="dialect">A instância do dialeto SQL a ser usada.</param>
        /// <returns>Uma nova instância do FluentCommandBuilder.</returns>
        public static FluentCommandBuilder Create(IDialect dialect)
        {
            return new FluentCommandBuilder(dialect);
        }

        /// <summary>
        /// Registra um alias para um tipo de entidade, que será usado na construção de SQL.
        /// O nome da tabela é inferido do atributo [Table] ou do nome do tipo pluralizado.
        /// </summary>
        /// <typeparam name="TEntity">O tipo da entidade a ser registrada.</typeparam>
        /// <param name="alias">O alias curto a ser usado na SQL para esta entidade.</param>
        /// <param name="tableName">Opcional: O nome da tabela no banco de dados. Se não fornecido, será inferido.</param>
        /// <returns>O FluentCommandBuilder para encadeamento.</returns>
        [Obsolete("O registro manual de aliases é obsoleto e será removido em versões futuras. Os aliases agora são resolvidos automaticamente.")]
        public FluentCommandBuilder RegisterAlias<TEntity>(string alias, string? tableName = null)
        {
            _aliasRegistry.RegisterAlias(typeof(TEntity), alias, tableName);
            return this;
        }

        // --- Método de fábrica para SELECT ---
        /// <summary>
        /// Inicia a construção de uma declaração SELECT e permite a configuração de colunas.
        /// </summary>
        /// <param name="action">Uma ação para configurar o SelectClauseBuilder.</param>
        /// <returns>Um SelectBuilder para encadeamento.</returns>
        public SelectBuilder Select(Action<SelectClauseBuilder> action)
        {
            var selectBuilder = new SelectBuilder(this, _aliasRegistry, _dialect);
            selectBuilder.Select(action);
            return selectBuilder;
        }

        // --- Métodos para outros Builders DML (permanecem como fábricas) ---
        /// <summary>
        /// Inicia a construção de uma declaração INSERT.
        /// </summary>
        /// <returns>Um InsertBuilder para configurar a query INSERT.</returns>
        public InsertBuilder Insert()
        {
            return new InsertBuilder(this, _aliasRegistry, _dialect);
        }

        /// <summary>
        /// Inicia a construção de uma declaração UPDATE.
        /// </summary>
        /// <returns>Um UpdateBuilder para configurar a query UPDATE.</returns>
        public UpdateBuilder Update()
        {
            return new UpdateBuilder(this, _aliasRegistry, _dialect);
        }

        /// <summary>
        /// Inicia a construção de uma declaração DELETE.
        /// </summary>
        /// <typeparam name="TEntity">O tipo da entidade a ser excluída.</typeparam>
        /// <returns>Um DeleteBuilder para configurar a query DELETE.</returns>
        public DeleteBuilder Delete<TEntity>()
        {
            return new DeleteBuilder(this, _aliasRegistry, _dialect)
                .From<TEntity>();
        }

        /// <summary>
        /// Inicia a construção de um lote de instruções SELECT independentes, equivalente ao
        /// padrão Dapper QueryMultipleAsync (um result set por instrução adicionada).
        /// </summary>
        /// <returns>Um BatchQueryBuilder para adicionar as instruções SELECT do lote.</returns>
        public BatchQueryBuilder Batch()
        {
            return new BatchQueryBuilder(this, _aliasRegistry, _dialect);
        }

        // Métodos internos para obter aliases (úteis para todos os builders)
        internal string GetAliasForType(Type entityType)
        {
            return _aliasRegistry.GetOrAddAlias(entityType);
        }

        internal string GetTableNameForAlias(string alias)
        {
            return _aliasRegistry.GetTableNameForAlias(alias);
        }
    }
}