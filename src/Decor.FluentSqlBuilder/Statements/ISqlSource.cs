namespace Decor.FluentSqlBuilder.Statements
{
    /// <summary>
    /// Representa uma fonte de SQL componível (um SELECT ou uma combinação UNION) que pode ser
    /// usada como subconsulta em FROM, combinada com UNION/UNION ALL, ou agrupada em um lote.
    /// Ao contrário de Build(), BuildInline() não anexa o terminador ';' de instrução top-level
    /// nem cláusulas de ORDER BY/paginação/lock hint, que não fazem sentido dentro de uma subconsulta.
    /// </summary>
    public interface ISqlSource
    {
        (string Sql, Dictionary<string, object?> Parameters) BuildInline();
    }
}
