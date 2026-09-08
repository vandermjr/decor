namespace Decor.FluentSqlBuilder.Dialects.MariaDB;
public class MariaDBKeywords : SqlKeywords
{
    public string LIMIT { get; } = "LIMIT";
    public string CONCAT_WS { get; } = "CONCAT_WS";
    public override string IDENTIFIER_PREFIX { get; } = "`";
    public override string IDENTIFIER_SUFFIX { get; } = "`";
}
