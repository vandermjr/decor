namespace Decor.FluentSqlBuilder.Dialects
{
    public abstract class BaseSqlFunctions(SqlKeywords keywords) : ISqlFunctions
    {
        protected readonly SqlKeywords _keywords = keywords;

        public virtual string Concat(params string[] expressions)
        {
            if (expressions is null || expressions.Length == 0)
                return string.Empty;

            var validExpressions = expressions.Where(e => !string.IsNullOrWhiteSpace(e)).ToArray();
            if (validExpressions.Length == 0)
                return string.Empty;

            // Calcula tamanho total da string final
            int totalLength = _keywords.CONCAT.Length +
                              _keywords.LEFT_PARENTHESIS.Length +
                              _keywords.RIGHT_PARENTHESIS.Length +
                              validExpressions.Sum(e => e.Length) +
                              _keywords.COMMA.Length * (validExpressions.Length - 1);

            return string.Create(totalLength, validExpressions, (span, exprs) =>
            {
                int pos = 0;

                // Escreve CONCAT(
                _keywords.CONCAT.AsSpan().CopyTo(span.Slice(pos));
                pos += _keywords.CONCAT.Length;

                // Abre parêntese
                _keywords.LEFT_PARENTHESIS.AsSpan().CopyTo(span.Slice(pos));
                pos += _keywords.LEFT_PARENTHESIS.Length;

                // Escreve expressões separadas por vírgula
                for (int i = 0; i < exprs.Length; i++)
                {
                    var expr = exprs[i];
                    expr.AsSpan().CopyTo(span.Slice(pos));
                    pos += expr.Length;

                    if (i < exprs.Length - 1)
                    {
                        _keywords.COMMA.AsSpan().CopyTo(span.Slice(pos));
                        pos += _keywords.COMMA.Length;
                    }
                }

                // Fecha parêntese
                _keywords.RIGHT_PARENTHESIS.AsSpan().CopyTo(span.Slice(pos));
            });
        }

        /// <summary>
        /// Constrói a função agregada COUNT com uma expressão.
        /// </summary>
        /// <param name="expression">A expressão para contagem.</param>
        /// <returns>A string SQL para a função COUNT.</returns>
        public virtual string Count(string expression)
        {
            // O tratamento para expressão vazia já é feito pelo chamador.
            int totalLength = 
                _keywords.COUNT.Length + 
                _keywords.LEFT_PARENTHESIS.Length + 
                expression.Length + 
                _keywords.RIGHT_PARENTHESIS.Length;

            return string.Create(totalLength, expression, (span, expr) =>
            {
                int pos = 0;
                
                _keywords.COUNT.AsSpan().CopyTo(span.Slice(pos)); 
                pos += _keywords.COUNT.Length;
                
                _keywords.LEFT_PARENTHESIS.AsSpan().CopyTo(span.Slice(pos)); 
                pos += _keywords.LEFT_PARENTHESIS.Length;
                
                expr.AsSpan().CopyTo(span.Slice(pos)); 
                pos += expr.Length;
                
                _keywords.RIGHT_PARENTHESIS.AsSpan().CopyTo(span.Slice(pos));
            });
        }

        /// <summary>
        /// Constrói a função agregada COUNT com uma expressão e a opção DISTINCT.
        /// </summary>
        /// <param name="expression">A expressão para contagem.</param>
        /// <param name="isDistinct">True se deve ser COUNT(DISTINCT ...), caso contrário, false.</param>
        /// <returns>A string SQL para a função COUNT.</returns>
        public virtual string Count(string expression, bool isDistinct)
        {
            if (!isDistinct)
            {
                return Count(expression);
            }

            int totalLength = 
                _keywords.COUNT.Length + 
                _keywords.LEFT_PARENTHESIS.Length + 
                _keywords.DISTINCT.Length + 1 + 
                expression.Length + 
                _keywords.RIGHT_PARENTHESIS.Length;

            return string.Create(totalLength, (expression, _keywords), (span, state) =>
            {
                int pos = 0;
                
                state._keywords.COUNT.AsSpan().CopyTo(span.Slice(pos)); 
                pos += state._keywords.COUNT.Length;
                
                state._keywords.LEFT_PARENTHESIS.AsSpan().CopyTo(span.Slice(pos)); 
                pos += state._keywords.LEFT_PARENTHESIS.Length;
                
                state._keywords.DISTINCT.AsSpan().CopyTo(span.Slice(pos)); 
                pos += state._keywords.DISTINCT.Length;
                
                span[pos++] = ' ';
                state.expression.AsSpan().CopyTo(span.Slice(pos)); 
                pos += state.expression.Length;
                
                state._keywords.RIGHT_PARENTHESIS.AsSpan().CopyTo(span.Slice(pos));
            });
        }
    }
}