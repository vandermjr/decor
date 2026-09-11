using Dapper;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Data;
using Decor.Core.Interfaces.Repositories;
using Decor.FluentSqlBuilder;

namespace Decor.Infrastructure.Data.Repositories;

public class PurchaseOrderItemRepository(IDatabaseConnection dbConnection, Func<FluentCommandBuilder> createCommandBuilder) : IPurchaseOrderItemRepository
{
    private readonly IDatabaseConnection _dbConnection = dbConnection;
    private readonly Func<FluentCommandBuilder> _createCommandBuilder = createCommandBuilder;

    public int Save(PurchaseOrderItem purchaseOrderItem)
    {
        using var connection = _dbConnection.CreateConnection();

        var (sql, parameters) = purchaseOrderItem.PurchaseOrderItemID != 0
            ? _createCommandBuilder().Update().Table<PurchaseOrderItem>().Set(purchaseOrderItem).Where(w => w.Equals((PurchaseOrderItem poi) => poi.PurchaseOrderItemID, purchaseOrderItem.PurchaseOrderItemID)).Build()
            : _createCommandBuilder().Insert().Into<PurchaseOrderItem>().Values(purchaseOrderItem).Build();

        return connection.Execute(sql, parameters);
    }

    public async Task<int> SaveAsync(PurchaseOrderItem purchaseOrderItem, CancellationToken cancellationToken = default)
    {
        var (sql, parameters) = purchaseOrderItem.PurchaseOrderItemID != 0
            ? _createCommandBuilder().Update().Table<PurchaseOrderItem>().Set(purchaseOrderItem).Where(w => w.Equals((PurchaseOrderItem poi) => poi.PurchaseOrderItemID, purchaseOrderItem.PurchaseOrderItemID)).Build()
            : _createCommandBuilder().Insert().Into<PurchaseOrderItem>().Values(purchaseOrderItem).Build();

        using var connection = _dbConnection.CreateConnection();
        return await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }

    public int Delete(int purchaseOrderItemId)
    {
        var (sql, parameters) = _createCommandBuilder()
            .Delete<PurchaseOrderItem>()
            .Where(w => w.Equals((PurchaseOrderItem poi) => poi.PurchaseOrderItemID, purchaseOrderItemId))
            .Build();

        using var connection = _dbConnection.CreateConnection();
        return connection.Execute(sql, parameters);
    }

    public async Task<int> DeleteAsync(int purchaseOrderItemId, CancellationToken cancellationToken = default)
    {
        var (sql, parameters) = _createCommandBuilder()
            .Delete<PurchaseOrderItem>()
            .Where(w => w.Equals((PurchaseOrderItem poi) => poi.PurchaseOrderItemID, purchaseOrderItemId))
            .Build();

        using var connection = _dbConnection.CreateConnection();
        return await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }

    public IEnumerable<PurchaseOrderItem> SearchGetBy(string? arg = null)
    {
        var (sql, parameters) = _createCommandBuilder()
            .Select(s => s.AllColumns<PurchaseOrderItem>())
            .From<PurchaseOrderItem>()
            .Where(w => w.WithDynamicSearchFilter<PurchaseOrderItem, PurchaseOrderItem>(arg, poi => poi.PurchaseOrderItemID, poi => poi.ProductID))
            .OrderBy("poi.PurchaseOrderItemID ASC")
            .Build();

        using var connection = _dbConnection.CreateConnection();
        return connection.Query<PurchaseOrderItem>(sql, parameters);
    }

    public async Task<IReadOnlyList<PurchaseOrderItem>> SearchGetByAsync(string? arg = null, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default)
    {
        ValidatePage(page, pageSize);
        var (sql, parameters) = _createCommandBuilder()
            .Select(s => s.AllColumns<PurchaseOrderItem>())
            .From<PurchaseOrderItem>()
            .Where(w => w.WithDynamicSearchFilter<PurchaseOrderItem, PurchaseOrderItem>(arg, poi => poi.PurchaseOrderItemID, poi => poi.ProductID))
            .OrderBy("poi.PurchaseOrderItemID ASC")
            .Take((uint)pageSize)
            .Skip((uint)((page - 1) * pageSize))
            .Build();

        using var connection = _dbConnection.CreateConnection();
        var result = await connection.QueryAsync<PurchaseOrderItem>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        return result.AsList();
    }

    public async Task<IEnumerable<PurchaseOrderItem>> GetByPurchaseOrderIdAsync(int purchaseOrderId, CancellationToken cancellationToken = default)
    {
        var (sql, parameters) = _createCommandBuilder()
            .Select(s => s.AllColumns<PurchaseOrderItem>())
            .From<PurchaseOrderItem>()
            .Where(w => w.Equals((PurchaseOrderItem poi) => poi.PurchaseOrderID, purchaseOrderId))
            .OrderBy("poi.PurchaseOrderItemID ASC")
            .Build();

        using var connection = _dbConnection.CreateConnection();
        var result = await connection.QueryAsync<PurchaseOrderItem>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        return result.AsList();
    }

    private static void ValidatePage(int page, int pageSize)
    {
        if (page < 1) throw new ArgumentOutOfRangeException(nameof(page));
        if (pageSize is < 1 or > 500) throw new ArgumentOutOfRangeException(nameof(pageSize));
    }
}