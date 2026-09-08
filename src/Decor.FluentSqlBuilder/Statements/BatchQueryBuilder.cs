using System.Text;
using Decor.FluentSqlBuilder.Dialects;
using Decor.FluentSqlBuilder.Helpers;

namespace Decor.FluentSqlBuilder.Statements
{
    /// <summary>
    /// Compõe múltiplas instruções SELECT independentes num único comando, equivalente ao padrão
    /// Dapper QueryMultipleAsync (um result set por instrução adicionada via Select(...)).
    /// </summary>
    public class BatchQueryBuilder
    {
        private readonly FluentCommandBuilder _rootCommandBuilder;
        private readonly TableAliasRegistry _aliasRegistry;
        private readonly IDialect _dialect;
        private readonly List<SelectBuilder> _statements = [];

        internal BatchQueryBuilder(FluentCommandBuilder rootCommandBuilder, TableAliasRegistry aliasRegistry, IDialect dialect)
        {
            _rootCommandBuilder = rootCommandBuilder;
            _aliasRegistry = aliasRegistry;
            _dialect = dialect;
        }

        /// <summary>
        /// Adiciona uma instrução SELECT independente ao lote, lida como mais um result set
        /// (ex: via SqlMapper.GridReader.ReadAsync&lt;T&gt;() do Dapper).
        /// </summary>
        public BatchQueryBuilder Select(Action<SelectBuilder> configure)
        {
            ArgumentNullException.ThrowIfNull(configure);

            var statement = new SelectBuilder(_rootCommandBuilder, _aliasRegistry, _dialect);
            configure(statement);
            _statements.Add(statement);
            return this;
        }

        /// <summary>
        /// Constrói o comando SQL com todas as instruções concatenadas e os parâmetros combinados.
        /// </summary>
        public (string Sql, Dictionary<string, object?> Parameters) Build()
        {
            if (_statements.Count == 0)
            {
                throw new InvalidOperationException("O lote precisa de ao menos uma instrução SELECT. Use Select(...) para adicioná-la.");
            }

            var sqlBuilder = new StringBuilder();
            var parameters = new Dictionary<string, object?>();

            foreach (var statement in _statements)
            {
                var (sql, statementParameters) = statement.Build();
                sqlBuilder.Append(sql);
                ParameterHelper.MergeParameters(parameters, statementParameters);
            }

            return (sqlBuilder.ToString(), parameters);
        }
    }
}
