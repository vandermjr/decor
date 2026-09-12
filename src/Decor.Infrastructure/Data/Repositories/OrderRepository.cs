using Dapper;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Data;
using Decor.Core.Interfaces.Repositories;
using Decor.FluentSqlBuilder;

namespace Decor.Infrastructure.Data.Repositories;

public class OrderRepository(IDatabaseConnection dbConnection, Func<FluentCommandBuilder> createCommandBuilder) : IOrderRepository
{
    private readonly IDatabaseConnection _dbConnection = dbConnection;
    private readonly Func<FluentCommandBuilder> _createCommandBuilder = createCommandBuilder;

    public int Save(Order entity)
    {
        using var conn = _dbConnection.CreateConnection();
        if (entity.OrderID != 0)
        {
            var (sql, parameters) = _createCommandBuilder()
                .Update()
                .Table<Order>()
                .Set(entity)
                .Where(w => w.Equals((Order o) => o.OrderID, entity.OrderID))
                .Build();
            return conn.Execute(sql, parameters);
        }
        else
        {
            var (sql, parameters) = _createCommandBuilder()
                .Insert()
                .Into<Order>()
                .Values(entity)
                .ReturningGeneratedId()
                .Build();
            var generatedId = conn.QuerySingle<int>(sql, parameters);
            entity.OrderID = generatedId;
            return 1;
        }
    }

    public async Task<int> SaveAsync(Order entity, CancellationToken cancellationToken = default)
    {
        using var connection = _dbConnection.CreateConnection();
        if (entity.OrderID != 0)
        {
            var (sql, parameters) = _createCommandBuilder()
                .Update()
                .Table<Order>()
                .Set(entity)
                .Where(w => w.Equals((Order o) => o.OrderID, entity.OrderID))
                .Build();
            return await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        }
        else
        {
            var (sql, parameters) = _createCommandBuilder()
                .Insert()
                .Into<Order>()
                .Values(entity)
                .ReturningGeneratedId()
                .Build();
            var generatedId = await connection.QuerySingleAsync<int>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
            entity.OrderID = generatedId;
            return 1;
        }
    }

    public int Delete(int id)
    {
        var (sql, parameters) = _createCommandBuilder()
            .Delete<Order>()
            .Where(w => w.Equals((Order o) => o.OrderID, id))
            .Build();

        using var conn = _dbConnection.CreateConnection();
        return conn.Execute(sql, parameters);
    }

    public async Task<int> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var (sql, parameters) = _createCommandBuilder()
            .Delete<Order>()
            .Where(w => w.Equals((Order o) => o.OrderID, id))
            .Build();

        using var connection = _dbConnection.CreateConnection();
        return await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }

    public IEnumerable<Order> SearchGetBy(string? arg = null)
    {
        var (sql, parameters) = _createCommandBuilder()
            .Select(s => s.AllColumns<Order>())
            .From<Order>()
            .Where(w => w.WithDynamicSearchFilter<Order, Order>(arg, o => o.OrderID, o => o.OrderID))
            .OrderBy("o.OrderID ASC")
            .Build();

        using var conn = _dbConnection.CreateConnection();
        return conn.Query<Order>(sql, parameters);
    }

    public async Task<IReadOnlyList<Order>> SearchGetByAsync(string? arg = null, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default)
    {
        ValidatePage(page, pageSize);
        var (sql, parameters) = _createCommandBuilder()
            .Select(s => s.AllColumns<Order>())
            .From<Order>()
            .Where(w => w.WithDynamicSearchFilter<Order, Order>(arg, o => o.OrderID, o => o.OrderID))
            .OrderBy("o.OrderID ASC")
            .Take((uint)pageSize)
            .Skip((uint)((page - 1) * pageSize))
            .Build();

        using var connection = _dbConnection.CreateConnection();
        var result = await connection.QueryAsync<Order>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        return result.AsList();
    }

    public async Task<Order?> GetByIdAsync(int orderId, CancellationToken cancellationToken = default)
    {
        var (sql, parameters) = _createCommandBuilder()
            .Select(s => s.AllColumns<Order>())
            .From<Order>()
            .Where(w => w.Equals((Order o) => o.OrderID, orderId))
            .Build();

        using var connection = _dbConnection.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<Order>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }

    public async Task<Order?> GetCompleteOrderAsync(int orderId, CancellationToken cancellationToken = default)
    {
        var order = await GetByIdAsync(orderId, cancellationToken);
        if (order == null) return null;

        using var connection = _dbConnection.CreateConnection();

        var (itemSql, itemParams) = _createCommandBuilder()
            .Select(s => s.AllColumns<OrderItem>())
            .From<OrderItem>()
            .Where(w => w.Equals((OrderItem i) => i.OrderID, orderId))
            .OrderBy("oi.OrderItemID ASC")
            .Build();

        var items = (await connection.QueryAsync<OrderItem>(new CommandDefinition(itemSql, itemParams, cancellationToken: cancellationToken))).ToList();
        order.Items = items;

        foreach (var item in items)
        {
            var (valSql, valParams) = _createCommandBuilder()
                .Select(s => s.AllColumns<OrderItemSpecificationValue>())
                .From<OrderItemSpecificationValue>()
                .Where(w => w.Equals((OrderItemSpecificationValue v) => v.OrderItemID, item.OrderItemID))
                .OrderBy("oisv.ValueID ASC")
                .Build();

            var values = (await connection.QueryAsync<OrderItemSpecificationValue>(new CommandDefinition(valSql, valParams, cancellationToken: cancellationToken))).ToList();
            item.SpecificationValues = values;
        }

        return order;
    }

    public async Task<Order?> GetByQuoteSectionIdAsync(int quoteSectionId, CancellationToken cancellationToken = default)
    {
        var (sql, parameters) = _createCommandBuilder()
            .Select(s => s.AllColumns<Order>())
            .From<Order>()
            .Where(w => w.Equals((Order o) => o.QuoteSectionID, quoteSectionId))
            .Build();

        using var connection = _dbConnection.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<Order>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }

    public async Task<OrderItem?> GetOrderItemByIdAsync(int orderItemId, CancellationToken cancellationToken = default)
    {
        var (sql, parameters) = _createCommandBuilder()
            .Select(s => s.AllColumns<OrderItem>())
            .From<OrderItem>()
            .Where(w => w.Equals((OrderItem i) => i.OrderItemID, orderItemId))
            .Build();

        using var connection = _dbConnection.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<OrderItem>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }

    public async Task<int> SaveOrderItemAsync(OrderItem item, CancellationToken cancellationToken = default)
    {
        using var connection = _dbConnection.CreateConnection();
        if (item.OrderItemID != 0)
        {
            var (sql, parameters) = _createCommandBuilder()
                .Update()
                .Table<OrderItem>()
                .Set(item)
                .Where(w => w.Equals((OrderItem i) => i.OrderItemID, item.OrderItemID))
                .Build();
            return await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        }
        else
        {
            var (sql, parameters) = _createCommandBuilder()
                .Insert()
                .Into<OrderItem>()
                .Values(item)
                .ReturningGeneratedId()
                .Build();
            var generatedId = await connection.QuerySingleAsync<int>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
            item.OrderItemID = generatedId;
            return 1;
        }
    }

    public async Task<int> SaveSpecificationValueAsync(OrderItemSpecificationValue value, CancellationToken cancellationToken = default)
    {
        using var connection = _dbConnection.CreateConnection();
        if (value.ValueID != 0)
        {
            var (sql, parameters) = _createCommandBuilder()
                .Update()
                .Table<OrderItemSpecificationValue>()
                .Set(value)
                .Where(w => w.Equals((OrderItemSpecificationValue v) => v.ValueID, value.ValueID))
                .Build();
            return await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        }
        else
        {
            var (sql, parameters) = _createCommandBuilder()
                .Insert()
                .Into<OrderItemSpecificationValue>()
                .Values(value)
                .ReturningGeneratedId()
                .Build();
            var generatedId = await connection.QuerySingleAsync<int>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
            value.ValueID = generatedId;
            return 1;
        }
    }

    private static void ValidatePage(int page, int pageSize)
    {
        if (page < 1) throw new ArgumentOutOfRangeException(nameof(page), "O número da página deve ser maior ou igual a 1.");
        if (pageSize < 1) throw new ArgumentOutOfRangeException(nameof(pageSize), "O tamanho da página deve ser maior ou igual a 1.");
    }
}
