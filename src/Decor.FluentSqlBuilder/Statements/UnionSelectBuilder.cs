using Decor.FluentSqlBuilder.Dialects;
using Decor.FluentSqlBuilder.Helpers;

namespace Decor.FluentSqlBuilder.Statements
{
    /// <summary>
    /// Combina duas ou mais fontes SQL componíveis (<see cref="ISqlSource"/>) com UNION/UNION ALL.
    /// Resultado, por sua vez, também é componível (pode ser usado como subconsulta ou em outro UNION).
    /// </summary>
    public class UnionSelectBuilder : ISqlSource
    {
        private readonly IDialect _dialect;
        private readonly List<(ISqlSource Source, bool All)> _members;

        internal UnionSelectBuilder(IDialect dialect, ISqlSource first)
        {
            _dialect = dialect;
            _members = [(first, false)];
        }

        public UnionSelectBuilder Union(ISqlSource other)
        {
            ArgumentNullException.ThrowIfNull(other);
            _members.Add((other, false));
            return this;
        }

        public UnionSelectBuilder UnionAll(ISqlSource other)
        {
            ArgumentNullException.ThrowIfNull(other);
            _members.Add((other, true));
            return this;
        }

        public (string Sql, Dictionary<string, object?> Parameters) BuildInline()
        {
            var parameters = new Dictionary<string, object?>();
            var parts = new List<string>();

            for (int i = 0; i < _members.Count; i++)
            {
                var (source, all) = _members[i];
                if (i > 0)
                {
                    parts.Add(all ? $"{_dialect.Keywords.UNION} {_dialect.Keywords.ALL}" : _dialect.Keywords.UNION);
                }

                var (sql, memberParameters) = source.BuildInline();
                parts.Add(sql);
                ParameterHelper.MergeParameters(parameters, memberParameters);
            }

            return (string.Join("\n", parts), parameters);
        }
    }
}
