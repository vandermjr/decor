using System.Data.Common;
using Decor.Core.Interfaces.Data;
using Decor.Core.Interfaces.Services;

namespace Decor.Infrastructure.Services;

public sealed class DatabaseHealthService(IDatabaseConnection databaseConnection) : IDatabaseHealthService
{
    public string ConnectionDescription
    {
        get
        {
            using var connection = databaseConnection.CreateConnection();
            var server = connection is DbConnection dbConnection ? dbConnection.DataSource : "indisponível";
            var database = connection.Database;
            return $"Banco: {DisplayValue(database)} | Conexão: {DisplayValue(server)}";
        }
    }

    public async Task<bool> IsOnlineAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = databaseConnection.CreateConnection();
            if (connection is not DbConnection dbConnection)
            {
                connection.Open();
                return true;
            }

            await dbConnection.OpenAsync(cancellationToken);
            await using var command = dbConnection.CreateCommand();
            command.CommandText = "SELECT 1";
            return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken), System.Globalization.CultureInfo.InvariantCulture) == 1;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return false;
        }
    }

    private static string DisplayValue(string? value) => string.IsNullOrWhiteSpace(value) ? "não informado" : value;
}