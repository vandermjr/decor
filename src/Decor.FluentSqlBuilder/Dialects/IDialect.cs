namespace Decor.FluentSqlBuilder.Dialects
{
    public enum LikeOperator
    {
        Contains,
        BeginsWith,
        EndsWith
    }

    public enum FullTextSearchMode
    {
        NaturalLanguage,
        Boolean
    }

    public interface IDialect
    {
        SqlKeywords Keywords { get; }
        ISqlFunctions Functions { get; }
        string GetParameterPrefix();
        string FormatIdentifier(string identifier);

        /// <summary>
        /// Constrói a cláusula SQL de paginação com base nos valores de skip e take.
        /// A sintaxe gerada depende do dialeto de banco de dados específico.
        /// </summary>
        /// <param name="skip">O número de registros a pular. Pode ser nulo.</param>
        /// <param name="take">O número de registros a retornar. Pode ser nulo.</param>
        /// <returns>A string SQL da cláusula de paginação.</returns>
        string BuildPagination(uint? take, uint? skip);
        string BuildTop(uint take);
        string BuildLike(string propertyName, string parameterName, LikeOperator likeOperator);
        string BuildFullTextSearch(IReadOnlyList<string> columns, string parameterName, FullTextSearchMode mode);

        /// <summary>
        /// Constrói o hint de bloqueio pessimista (ex: 'FOR UPDATE') anexado ao final de um SELECT.
        /// </summary>
        string BuildLockHint();

        /// <summary>
        /// Constrói a instrução que recupera o identificador autoincrementado gerado pelo último
        /// INSERT na mesma conexão/transação (ex: 'SELECT LAST_INSERT_ID()').
        /// </summary>
        string BuildLastInsertIdSelect();
    }
}