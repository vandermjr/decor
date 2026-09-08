namespace Decor.FluentSqlBuilder.Dialects.MariaDB
{
    public class MariaDBFunctions(MariaDBKeywords keywords) : BaseSqlFunctions(keywords), IMariaDBFunctions
    {
        private readonly MariaDBKeywords _mariaDBKeywords = keywords;

        /// <summary>
        /// Concatena uma lista de strings com um separador.
        /// <para>Esta é uma função específica do MariaDB/MySQL.</para>
        /// </summary>
        /// <param name="separator">A string usada para separar os valores.</param>
        /// <param name="expressions">As expressões SQL a serem concatenadas.</param>
        /// <returns>A string SQL da função CONCAT_WS.</returns>
        public string ConcatWS(string separator, params string[] expressions)
        {
            if (expressions is null || expressions.Length == 0)
            {
                return string.Empty;
            }

            var validExpressions = expressions.Where(e => !string.IsNullOrWhiteSpace(e)).ToArray();
            if (validExpressions.Length == 0)
            {
                return string.Empty;
            }

            // Calcula o tamanho total da string final
            int totalLength = _mariaDBKeywords.CONCAT_WS.Length +
                              _mariaDBKeywords.LEFT_PARENTHESIS.Length +
                              _mariaDBKeywords.RIGHT_PARENTHESIS.Length +
                              separator.Length +
                              validExpressions.Sum(e => e.Length) +
                              _mariaDBKeywords.COMMA.Length * validExpressions.Length;

            return string.Create(totalLength, (validExpressions, separator), (span, state) =>
            {
                var (exprs, sep) = state;
                int pos = 0;

                // Escreve CONCAT_WS(
                _mariaDBKeywords.CONCAT_WS.AsSpan().CopyTo(span.Slice(pos));
                pos += _mariaDBKeywords.CONCAT_WS.Length;
                _mariaDBKeywords.LEFT_PARENTHESIS.AsSpan().CopyTo(span.Slice(pos));
                pos += _mariaDBKeywords.LEFT_PARENTHESIS.Length;

                // Escreve o separador
                sep.AsSpan().CopyTo(span.Slice(pos));
                pos += sep.Length;

                // Escreve a vírgula para separar do primeiro parâmetro
                _mariaDBKeywords.COMMA.AsSpan().CopyTo(span.Slice(pos));
                pos += _mariaDBKeywords.COMMA.Length;

                // Escreve expressões separadas por vírgula
                for (int i = 0; i < exprs.Length; i++)
                {
                    var expr = exprs[i];
                    expr.AsSpan().CopyTo(span.Slice(pos));
                    pos += expr.Length;

                    if (i < exprs.Length - 1)
                    {
                        _mariaDBKeywords.COMMA.AsSpan().CopyTo(span.Slice(pos));
                        pos += _mariaDBKeywords.COMMA.Length;
                    }
                }

                // Fecha parêntese
                _mariaDBKeywords.RIGHT_PARENTHESIS.AsSpan().CopyTo(span.Slice(pos));
            });
        }
    }
}