namespace Decor.FluentSqlBuilder.Dialects.MySql
{
    public class MySqlKeywords : SqlKeywords
    {
        public const string LIMIT = "LIMIT";
        public override string IDENTIFIER_PREFIX { get; } = "`";
        public override string IDENTIFIER_SUFFIX { get; } = "`";
    }
}