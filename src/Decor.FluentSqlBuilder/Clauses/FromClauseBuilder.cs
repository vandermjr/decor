namespace Decor.FluentSqlBuilder.Clauses
{
    public class FromClauseBuilder(FluentCommandBuilder commandBuilder)
    {
        private readonly FluentCommandBuilder _commandBuilder = commandBuilder;
        private readonly Dictionary<Type, string> _internalAliases = [];

        public FromClauseBuilder Table<TEntity>(string alias)
        {
            if (_internalAliases.ContainsKey(typeof(TEntity)))
            {
                throw new InvalidOperationException($"A tabela para o tipo '{typeof(TEntity).Name}' já foi declarada.");
            }
            _internalAliases[typeof(TEntity)] = alias;
            return this;
        }

        internal List<(Type, string)> GetDeclaredTables()
        {
            var declaredTables = new List<(Type, string)>();
            foreach (var alias in _internalAliases)
            {
                declaredTables.Add((alias.Key, alias.Value));
            }
            return declaredTables;
        }
    }
}