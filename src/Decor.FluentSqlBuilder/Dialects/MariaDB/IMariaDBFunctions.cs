namespace Decor.FluentSqlBuilder.Dialects.MariaDB;
public interface IMariaDBFunctions : ISqlFunctions
{
    string ConcatWS(string separator, params string[] expressions);
}
