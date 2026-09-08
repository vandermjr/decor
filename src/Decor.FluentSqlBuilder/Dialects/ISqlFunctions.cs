namespace Decor.FluentSqlBuilder.Dialects
{
    /// <summary>
    /// Defines the <see cref="ISqlFunctions" />
    /// </summary>
    public interface ISqlFunctions
    {
        /// <summary>
        /// Gera uma expressão SQL utilizando a função <c>CONCAT</c>, concatenando múltiplas expressões SQL em uma única string.
        /// <code>Syntax:
        ///  CONCAT(expr1, expr2, ...).
        /// </code>
        /// </summary>
        /// <param name="expressions">The expressions<see cref="string[]"/></param>
        /// <returns>CONCAT('A', 'B', 'C')</returns>
        string Concat(params string[] expressions);

        string Count(string expression);
        string Count(string expression, bool isDistinct);
    }
}
