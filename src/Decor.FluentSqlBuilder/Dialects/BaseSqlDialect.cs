using System.Text;

namespace Decor.FluentSqlBuilder.Dialects
{
    public abstract class BaseSqlDialect(SqlKeywords keywords, ISqlFunctions functions) : IDialect
    {
        protected readonly SqlKeywords _keywords = keywords;
        protected readonly ISqlFunctions _functions = functions;

        public SqlKeywords Keywords => _keywords;
        public ISqlFunctions Functions => _functions;

        public virtual string GetParameterPrefix() => _keywords.PARAMETER_PREFIX;

        public virtual string FormatIdentifier(string identifier)
        {
            return $"{_keywords.IDENTIFIER_PREFIX}{identifier}{_keywords.IDENTIFIER_SUFFIX}";
        }

        public abstract string BuildPagination(uint? take, uint? skip);

        public virtual string BuildTop(uint take) => string.Empty;

        public virtual string BuildLike(string propertyName, string parameterName, LikeOperator likeOperator)
        {
            var sb = new StringBuilder();
            sb.Append(FormatIdentifier(propertyName));
            sb.Append(' ');
            sb.Append(_keywords.LIKE);
            sb.Append(' ');

            // Adiciona a lógica para CONCAT com base no tipo de LikeOperator
            string prefix = likeOperator is LikeOperator.EndsWith or LikeOperator.Contains ? _keywords.ANY_STRING_WILDCARD : string.Empty;
            string suffix = likeOperator is LikeOperator.BeginsWith or LikeOperator.Contains ? _keywords.ANY_STRING_WILDCARD : string.Empty;

            sb.Append(_functions.Concat(
                $"'{prefix}'",
                $"{GetParameterPrefix()}{parameterName}",
                $"'{suffix}'"
            ));
            return sb.ToString();
        }

        public virtual string BuildFullTextSearch(IReadOnlyList<string> columns, string parameterName, FullTextSearchMode mode)
        {
            throw new NotSupportedException("O dialeto SQL atual não suporta busca full-text.");
        }

        public virtual string BuildLockHint()
        {
            throw new NotSupportedException("O dialeto SQL atual não suporta hints de bloqueio.");
        }

        public virtual string BuildLastInsertIdSelect()
        {
            throw new NotSupportedException("O dialeto SQL atual não suporta recuperação do identificador autoincrementado.");
        }
    }
}