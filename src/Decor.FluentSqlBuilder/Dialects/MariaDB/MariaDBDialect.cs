namespace Decor.FluentSqlBuilder.Dialects.MariaDB
{
    public class MariaDBDialect : BaseSqlDialect
    {
        private static readonly MariaDBKeywords _mariaDBKeywords = new();
        private static readonly MariaDBFunctions _mariaDBfunctions = new(_mariaDBKeywords);
        public MariaDBDialect() : base(_mariaDBKeywords, _mariaDBfunctions) { }

        public override string BuildPagination(uint? take, uint? skip)
        {
            return (take, skip) switch
            {
                // Caso 1: Apenas 'take' é fornecido (skip é null ou 0).
                (uint takeVal, null or 0) =>
                    $"{_mariaDBKeywords.LIMIT} {takeVal}",

                // Caso 2: Ambos 'take' e 'skip' são fornecidos.
                (uint takeVal, uint skipVal) =>
                    $"{_mariaDBKeywords.LIMIT} {takeVal} {_mariaDBKeywords.OFFSET} {skipVal}",

                // Caso 3: Apenas 'skip' é fornecido (take é null ou 0).
                (null or 0, uint skipVal) =>
                    $"{_mariaDBKeywords.LIMIT} {uint.MaxValue} {_mariaDBKeywords.OFFSET} {skipVal}",

                // Caso padrão: Nenhuma paginação se ambos são null ou 0.
                _ => string.Empty
            };
        }

        public override string BuildFullTextSearch(IReadOnlyList<string> columns, string parameterName, FullTextSearchMode mode)
        {
            if (columns.Count == 0)
                throw new ArgumentException("A busca full-text exige ao menos uma coluna.", nameof(columns));

            var searchMode = mode == FullTextSearchMode.Boolean
                ? " IN BOOLEAN MODE"
                : " IN NATURAL LANGUAGE MODE";

            return $"MATCH({string.Join(", ", columns)}) AGAINST ({GetParameterPrefix()}{parameterName}{searchMode})";
        }

        public override string BuildLockHint() => "FOR UPDATE";

        public override string BuildLastInsertIdSelect() => "SELECT LAST_INSERT_ID()";

        // O método FormatIdentifier() e outros são herdados da classe base
        // e não precisam ser sobrescritos, pois a lógica já está na classe base.
    }
}