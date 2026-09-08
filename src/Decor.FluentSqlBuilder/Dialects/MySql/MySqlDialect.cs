namespace Decor.FluentSqlBuilder.Dialects.MySql
{
    public class MySqlDialect : BaseSqlDialect
    {
        private static readonly MySqlKeywords _mySqlKeywords = new();
        private static readonly MySqlFunctions _mySqlfunctions = new(_mySqlKeywords);
        public MySqlDialect() : base(_mySqlKeywords, _mySqlfunctions) { }

        public override string BuildPagination(uint? take, uint? skip)
        {
            return (take, skip) switch
            {
                // Caso 1: Apenas 'take' é fornecido (skip é null ou 0).
                (uint takeVal, null or 0) =>
                    $"{MySqlKeywords.LIMIT} {takeVal}",

                // Caso 2: Ambos 'take' e 'skip' são fornecidos.
                (uint takeVal, uint skipVal) =>
                    $"{MySqlKeywords.LIMIT} {takeVal} {Keywords.OFFSET} {skipVal}",

                // Caso 3: Apenas 'skip' é fornecido (take é null ou 0).
                (null or 0, uint skipVal) =>
                    $"{MySqlKeywords.LIMIT} {uint.MaxValue} {Keywords.OFFSET} {skipVal}",

                // Caso padrão: Nenhuma paginação se ambos são null ou 0.
                _ => string.Empty
            };
        }
    }
}