using System.Data;

namespace Decor.Core.Interfaces.Data;
public interface IDatabaseConnection
{
    IDbConnection CreateConnection();
}

