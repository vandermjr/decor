using Dapper;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Data;
using Decor.Core.Interfaces.Repositories;
using Decor.FluentSqlBuilder;

namespace Decor.Infrastructure.Data.Repositories;
public sealed class StockBalanceRepository(IDatabaseConnection databaseConnection, Func<FluentCommandBuilder> createCommandBuilder) : IStockBalanceRepository
{
    public async Task<StockBalance?> GetByProductAndLocationAsync(int productId, int stockLocationId, CancellationToken cancellationToken = default)
    {
        var (sql, parameters) = createCommandBuilder()
            .Select(s => s.AllColumns<StockBalance>())
            .From<StockBalance>()
            .Where(w =>
            {
                w.Equals((StockBalance sb) => sb.ProductID, productId);
                w.Equals((StockBalance sb) => sb.StockLocationID, stockLocationId);
            })
            .Build();

        using var connection = databaseConnection.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<StockBalance>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }

    public async Task<IEnumerable<StockBalance>> GetByProductAsync(int productId, CancellationToken cancellationToken = default)
    {
        var (sql, parameters) = createCommandBuilder()
            .Select(s => s.AllColumns<StockBalance>())
            .From<StockBalance>()
            .Where(w => w.Equals((StockBalance sb) => sb.ProductID, productId))
            .OrderBy("sb.StockLocationID ASC")
            .Build();

        using var connection = databaseConnection.CreateConnection();
        var result = await connection.QueryAsync<StockBalance>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        return result.AsList();
    }

    public async Task<IEnumerable<StockBalance>> GetByLocationAsync(int stockLocationId, CancellationToken cancellationToken = default)
    {
        var (sql, parameters) = createCommandBuilder()
            .Select(s => s.AllColumns<StockBalance>())
            .From<StockBalance>()
            .Where(w => w.Equals((StockBalance sb) => sb.StockLocationID, stockLocationId))
            .OrderBy("sb.ProductID ASC")
            .Build();

        using var connection = databaseConnection.CreateConnection();
        var result = await connection.QueryAsync<StockBalance>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        return result.AsList();
    }
}