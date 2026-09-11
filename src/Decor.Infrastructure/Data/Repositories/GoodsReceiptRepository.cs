using Dapper;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Data;
using Decor.Core.Interfaces.Repositories;
using Decor.FluentSqlBuilder;

namespace Decor.Infrastructure.Data.Repositories;

public sealed class GoodsReceiptRepository(IDatabaseConnection databaseConnection, Func<FluentCommandBuilder> createCommandBuilder) : IGoodsReceiptRepository
{
    public async Task<int> InsertAsync(GoodsReceipt goodsReceipt, CancellationToken cancellationToken = default)
    {
        var (sql, parameters) = createCommandBuilder()
            .Insert().Into<GoodsReceipt>()
            .Values(goodsReceipt)
            .ReturningGeneratedId()
            .Build();

        using var connection = databaseConnection.CreateConnection();
        return await connection.QuerySingleAsync<int>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }

    public async Task<GoodsReceipt?> GetByIdAsync(int goodsReceiptId, CancellationToken cancellationToken = default)
    {
        var (sql, parameters) = createCommandBuilder()
            .Select(s => s.AllColumns<GoodsReceipt>())
            .From<GoodsReceipt>()
            .Where(w => w.Equals((GoodsReceipt gr) => gr.GoodsReceiptID, goodsReceiptId))
            .Build();

        using var connection = databaseConnection.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<GoodsReceipt>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }

    public async Task<IEnumerable<GoodsReceipt>> GetByPurchaseOrderItemIdAsync(int purchaseOrderItemId, CancellationToken cancellationToken = default)
    {
        var (sql, parameters) = createCommandBuilder()
            .Select(s => s.AllColumns<GoodsReceipt>())
            .From<GoodsReceipt>()
            .Where(w => w.Equals((GoodsReceipt gr) => gr.PurchaseOrderItemID, purchaseOrderItemId))
            .OrderBy("gr.ReceiptDate ASC, gr.GoodsReceiptID ASC")
            .Build();

        using var connection = databaseConnection.CreateConnection();
        var result = await connection.QueryAsync<GoodsReceipt>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        return result.AsList();
    }
}