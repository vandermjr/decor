namespace Decor.FluentSqlBuilder.Dialects.MsSql
{
    public class MsSqlDialect : BaseSqlDialect
    {
        private static readonly MsSqlKeywords _msSqlKeywords = new();
        private static readonly MsSqlFunctions _msSqlfunctions = new(_msSqlKeywords);
        public MsSqlDialect() : base(_msSqlKeywords, _msSqlfunctions) { }


        /// <summary>
        /// Gera a cláusula de paginação para o dialeto MySQL.
        /// Usa a sintaxe LIMIT/OFFSET moderna.
        /// </summary>
        /// <param name="take">Número máximo de registros a retornar.</param>
        /// <param name="skip">Número de registros a pular antes de começar a retornar.</param>
        /// <returns>Cláusula SQL de paginação.</returns>
        public override string BuildPagination(uint? take, uint? skip)
        {
            return (take, skip) switch
            {
                // Apenas 'take' fornecido
                (uint takeVal, null or 0) =>
                    $"{Keywords.OFFSET} 0 {Keywords.ROWS} {Keywords.FETCH} {Keywords.NEXT} {takeVal} {Keywords.ROWS} {Keywords.ONLY}",

                // Ambos 'take' e 'skip' fornecidos
                (uint takeVal, uint skipVal) =>
                    $"{Keywords.OFFSET} {skipVal} {Keywords.ROWS} {Keywords.FETCH} {Keywords.NEXT} {takeVal} {Keywords.ROWS} {Keywords.ONLY}",

                // Apenas 'skip' fornecido
                (null or 0, uint skipVal) =>
                    $"{Keywords.OFFSET} {skipVal} {Keywords.ROWS}",

                // Nenhuma paginação
                _ => string.Empty
            };
        }



        public override string BuildTop(uint take)
        {
            if (take == 0)
            {
                throw new ArgumentException("Take must be greater than 0.");
            }
            return $"{MsSqlKeywords.TOP} ({take})";
        }
    }
}