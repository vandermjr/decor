using Dapper;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Data;
using Decor.Core.Interfaces.Repositories;
using Decor.FluentSqlBuilder;

namespace Decor.Infrastructure.Data.Repositories;

public class PurchaseOrderRepository(IDatabaseConnection dbConnection, Func<FluentCommandBuilder> createCommandBuilder) : IPurchaseOrderRepository
{
    private readonly IDatabaseConnection _dbConnection = dbConnection;
    private readonly Func<FluentCommandBuilder> _createCommandBuilder = createCommandBuilder;

    public int Save(PurchaseOrder purchaseOrder)
    {
        using var connection = _dbConnection.CreateConnection();

        var (sql, parameters) = purchaseOrder.PurchaseOrderID != 0
            ? _createCommandBuilder().Update().Table<PurchaseOrder>().Set(purchaseOrder).Where(w => w.Equals((PurchaseOrder po) => po.PurchaseOrderID, purchaseOrder.PurchaseOrderID)).Build()
            : _createCommandBuilder().Insert().Into<PurchaseOrder>().Values(purchaseOrder).Build();

        return connection.Execute(sql, parameters);
    }

    public async Task<int> SaveAsync(PurchaseOrder purchaseOrder, CancellationToken cancellationToken = default)
    {
        var (sql, parameters) = purchaseOrder.PurchaseOrderID != 0
            ? _createCommandBuilder().Update().Table<PurchaseOrder>().Set(purchaseOrder).Where(w => w.Equals((PurchaseOrder po) => po.PurchaseOrderID, purchaseOrder.PurchaseOrderID)).Build()
            : _createCommandBuilder().Insert().Into<PurchaseOrder>().Values(purchaseOrder).Build();

        using var connection = _dbConnection.CreateConnection();
        return await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }

    public int Delete(int purchaseOrderId)
    {
        var (sql, parameters) = _createCommandBuilder()
            .Delete<PurchaseOrder>()
            .Where(w => w.Equals((PurchaseOrder po) => po.PurchaseOrderID, purchaseOrderId))
            .Build();

        using var connection = _dbConnection.CreateConnection();
        return connection.Execute(sql, parameters);
    }

    public async Task<int> DeleteAsync(int purchaseOrderId, CancellationToken cancellationToken = default)
    {
        var (sql, parameters) = _createCommandBuilder()
            .Delete<PurchaseOrder>()
            .Where(w => w.Equals((PurchaseOrder po) => po.PurchaseOrderID, purchaseOrderId))
            .Build();

        using var connection = _dbConnection.CreateConnection();
        return await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }

    public IEnumerable<PurchaseOrder> SearchGetBy(string? arg = null)
    {
        var (sql, parameters) = _createCommandBuilder()
            .Select(s => s.AllColumns<PurchaseOrder>())
            .From<PurchaseOrder>()
            .Where(w => w.WithDynamicSearchFilter<PurchaseOrder, PurchaseOrder>(arg, po => po.PurchaseOrderID, po => po.SupplierID))
            .OrderBy("po.OrderDate DESC")
            .Build();

        using var connection = _dbConnection.CreateConnection();
        return connection.Query<PurchaseOrder>(sql, parameters);
    }

    public async Task<IReadOnlyList<PurchaseOrder>> SearchGetByAsync(string? arg = null, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default)
    {
        ValidatePage(page, pageSize);
        var (sql, parameters) = _createCommandBuilder()
            .Select(s => s.AllColumns<PurchaseOrder>())
            .From<PurchaseOrder>()
            .Where(w => w.WithDynamicSearchFilter<PurchaseOrder, PurchaseOrder>(arg, po => po.PurchaseOrderID, po => po.SupplierID))
            .OrderBy("po.OrderDate DESC")
            .Take((uint)pageSize)
            .Skip((uint)((page - 1) * pageSize))
            .Build();

        using var connection = _dbConnection.CreateConnection();
        var result = await connection.QueryAsync<PurchaseOrder>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        return result.AsList();
    }

    private static void ValidatePage(int page, int pageSize)
    {
        if (page < 1) throw new ArgumentOutOfRangeException(nameof(page));
        if (pageSize is < 1 or > 500) throw new ArgumentOutOfRangeException(nameof(pageSize));
    }
}