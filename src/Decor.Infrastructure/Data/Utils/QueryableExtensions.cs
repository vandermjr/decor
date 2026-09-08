using Decor.Core.Interfaces.Data;

namespace Decor.Infrastructure.Data.Utils;
// O QuerySingleOrMany só precisa do IQueryContext
public static class QueryableExtensions // Pode ser um nome melhor, pois opera sobre IEnumerable
{
    public static IEnumerable<T> QuerySingleOrMany<T>(this IEnumerable<T> source, IQueryContext context)
    {
        if (context.IsSingleIdSearch)
        {
            var single = source.FirstOrDefault();
            return single != null ? [single] : [];
        }
        return source;
    }
}
