using Decor.Core.Interfaces.Data;

namespace Decor.Infrastructure.Data.Utils;

public class QueryContext : IQueryContext
{
    public bool IsSingleIdSearch { get; set; } = false;
}
