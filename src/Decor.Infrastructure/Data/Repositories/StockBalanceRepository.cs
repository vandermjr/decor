using Dapper;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Data;
using Decor.Core.Interfaces.Repositories;
using Decor.FluentSqlBuilder;

namespace Decor.Infrastructure.Data.Repositories;

public sealed class StockBalanceRepository(IDatabaseConnection databaseConnection, Func<FluentCommandBuilder> createCommandBuilder) : IStockBalanceRepository
{
    private readonly IDatabaseConnection _databaseConnection = databaseConnection;
    private readonly Func<FluentCommandBuilder> _createCommandBuilder = createCommandBuilder;

    public async Task<StockBalance?> GetByProductAndLocationAsync(int productId, int stockLocationId, CancellationToken cancellationToken = default)
    {
        var (sql, parameters) = _createCommandBuilder()
            .Select(s => s.AllColumns<StockBalance>())
            .From<StockBalance>()
            .Where(w => w.Equals<StockBalance>(sb => sb.ProductID, productId).Equals<StockBalance>(sb => sb.StockLocationID, stockLocationId))
            .Build();

        using var connection = _databaseConnection.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<StockBalance>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }

    public async Task<IEnumerable<StockBalance>> GetByProductAsync(int productId, CancellationToken cancellationToken = default)
    {
        var (sql, parameters) = _createCommandBuilder()
            .Select(s => s.AllColumns<StockBalance>())
            .From<StockBalance>()
            .Where(w => w.Equals<StockBalance>(sb => sb.ProductID, productId))
            .OrderBy(o => o.Ascending<StockBalance>(sb => sb.StockLocationID))
            .Build();

        using var connection = _databaseConnection.CreateConnection();
        var result = await connection.QueryAsync<StockBalance>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        return result.AsList();
    }

    public async Task<IEnumerable<StockBalance>> GetByLocationAsync(int stockLocationId, CancellationToken cancellationToken = default)
    {
        var (sql, parameters) = _createCommandBuilder()
            .Select(s => s.AllColumns<StockBalance>())
            .From<StockBalance>()
            .Where(w => w.Equals<StockBalance>(sb => sb.StockLocationID, stockLocationId))
            .OrderBy(o => o.Ascending<StockBalance>(sb => sb.ProductID))
            .Build();

        using var connection = _databaseConnection.CreateConnection();
        var result = await connection.QueryAsync<StockBalance>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        return result.AsList();
    }
}