using Dapper;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Data;
using Decor.Core.Interfaces.Repositories;
using Decor.FluentSqlBuilder;

namespace Decor.Infrastructure.Data.Repositories;
public class StockLocationRepository(IDatabaseConnection dbConnection, Func<FluentCommandBuilder> createCommandBuilder) : IStockLocationRepository
{
    private readonly IDatabaseConnection _dbConnection = dbConnection;
    private readonly Func<FluentCommandBuilder> _createCommandBuilder = createCommandBuilder;

    public int Save(StockLocation stockLocation)
    {
        using var conn = _dbConnection.CreateConnection();

        var (sql, parameters) = stockLocation.StockLocationID != 0
            ? _createCommandBuilder().Update().Table<StockLocation>().Set(stockLocation).Where(w => w.Equals((StockLocation sl) => sl.StockLocationID, stockLocation.StockLocationID)).Build()
            : _createCommandBuilder().Insert().Into<StockLocation>().Values(stockLocation).Build();

        return conn.Execute(sql, parameters);
    }

    public async Task<int> SaveAsync(StockLocation stockLocation, CancellationToken cancellationToken = default)
    {
        var (sql, parameters) = stockLocation.StockLocationID != 0
            ? _createCommandBuilder().Update().Table<StockLocation>().Set(stockLocation).Where(w => w.Equals((StockLocation sl) => sl.StockLocationID, stockLocation.StockLocationID)).Build()
            : _createCommandBuilder().Insert().Into<StockLocation>().Values(stockLocation).Build();

        using var connection = _dbConnection.CreateConnection();
        return await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }

    public int Delete(int stockLocationId)
    {
        var (sql, parameters) = _createCommandBuilder()
            .Delete<StockLocation>()
            .Where(w => w.Equals((StockLocation sl) => sl.StockLocationID, stockLocationId))
            .Build();

        using var conn = _dbConnection.CreateConnection();
        return conn.Execute(sql, parameters);
    }

    public async Task<int> DeleteAsync(int stockLocationId, CancellationToken cancellationToken = default)
    {
        var (sql, parameters) = _createCommandBuilder()
            .Delete<StockLocation>()
            .Where(w => w.Equals((StockLocation sl) => sl.StockLocationID, stockLocationId))
            .Build();

        using var connection = _dbConnection.CreateConnection();
        return await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }

    public IEnumerable<StockLocation> SearchGetBy(string? arg = null)
    {
        var (sql, parameters) = _createCommandBuilder()
            .Select(s => s.AllColumns<StockLocation>())
            .From<StockLocation>()
            .Where(w => w.WithDynamicSearchFilter<StockLocation, StockLocation>(arg, sl => sl.StockLocationID, sl => sl.Name))
            .OrderBy("sl.Name ASC")
            .Build();

        using var conn = _dbConnection.CreateConnection();
        return conn.Query<StockLocation>(sql, parameters);
    }

    public async Task<IReadOnlyList<StockLocation>> SearchGetByAsync(string? arg = null, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default)
    {
        ValidatePage(page, pageSize);
        var (sql, parameters) = _createCommandBuilder()
            .Select(s => s.AllColumns<StockLocation>())
            .From<StockLocation>()
            .Where(w => w.WithDynamicSearchFilter<StockLocation, StockLocation>(arg, sl => sl.StockLocationID, sl => sl.Name))
            .OrderBy("sl.Name ASC")
            .Take((uint)pageSize)
            .Skip((uint)((page - 1) * pageSize))
            .Build();

        using var connection = _dbConnection.CreateConnection();
        var result = await connection.QueryAsync<StockLocation>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        return result.AsList();
    }

    private static void ValidatePage(int page, int pageSize)
    {
        if (page < 1) throw new ArgumentOutOfRangeException(nameof(page));
        if (pageSize is < 1 or > 500) throw new ArgumentOutOfRangeException(nameof(pageSize));
    }
}
