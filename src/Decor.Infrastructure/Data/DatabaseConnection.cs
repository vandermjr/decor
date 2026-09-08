using System.Data;
using Decor.Core.Interfaces.Data;
using MySqlConnector;

namespace Decor.Infrastructure.Data;
public class DatabaseConnection(string ConnectionString) : IDatabaseConnection
{
    private readonly string _connectionString =
            string.IsNullOrWhiteSpace(ConnectionString)
            ? throw new ArgumentException("A string de conexão não pode ser nula ou vazia.", nameof(ConnectionString))
            : ConnectionString;

    public IDbConnection CreateConnection()
    {
        return new MySqlConnection(_connectionString);
    }
}
