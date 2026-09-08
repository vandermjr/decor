namespace Decor.FluentSqlBuilder.Dialects.MsSql
{
    public class MsSqlKeywords : SqlKeywords
    {
        public const string TOP = "TOP";
        public override string IDENTIFIER_PREFIX { get; } = "[";
        public override string IDENTIFIER_SUFFIX { get; } = "]";
    }
}