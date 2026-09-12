using Dapper;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Data;
using Decor.Core.Interfaces.Repositories;
using Decor.FluentSqlBuilder;

namespace Decor.Infrastructure.Data.Repositories;

public class OrderOccurrenceRepository(
    IDatabaseConnection dbConnection,
    Func<FluentCommandBuilder> createCommandBuilder,
    IOrderRepository orderRepository) : IOrderOccurrenceRepository
{
    public int Save(OrderOccurrence entity)
    {
        using var connection = dbConnection.CreateConnection();
        if (entity.OccurrenceID != 0)
        {
            var (sql, parameters) = BuildUpdate(entity);
            return connection.Execute(sql, parameters);
        }

        var (insertSql, insertParameters) = BuildInsert(entity);
        entity.OccurrenceID = connection.QuerySingle<int>(insertSql, insertParameters);
        return 1;
    }

    public async Task<int> SaveAsync(OrderOccurrence entity, CancellationToken cancellationToken = default)
    {
        using var connection = dbConnection.CreateConnection();
        if (entity.OccurrenceID != 0)
        {
            var (sql, parameters) = BuildUpdate(entity);
            return await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        }

        var (insertSql, insertParameters) = BuildInsert(entity);
        entity.OccurrenceID = await connection.QuerySingleAsync<int>(new CommandDefinition(insertSql, insertParameters, cancellationToken: cancellationToken));
        return 1;
    }

    public async Task<int> RegisterAsync(OrderOccurrence occurrence, Order order, CancellationToken cancellationToken = default)
    {
        using var connection = dbConnection.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();
        try
        {
            var orderRows = await orderRepository.SaveAsync(order, connection, transaction, cancellationToken);
            if (orderRows != 1)
                throw new InvalidOperationException("Não foi possível atualizar os prazos do pedido.");

            var (sql, parameters) = BuildInsert(occurrence);
            occurrence.OccurrenceID = await connection.QuerySingleAsync<int>(new CommandDefinition(sql, parameters, transaction, cancellationToken: cancellationToken));
            transaction.Commit();
            return 1;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public int Delete(int id)
    {
        var (sql, parameters) = BuildDelete(id);
        using var connection = dbConnection.CreateConnection();
        return connection.Execute(sql, parameters);
    }

    public async Task<int> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var (sql, parameters) = BuildDelete(id);
        using var connection = dbConnection.CreateConnection();
        return await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }

    public IEnumerable<OrderOccurrence> SearchGetBy(string? arg = null)
    {
        var (sql, parameters) = BuildSearch(arg, null, null);
        using var connection = dbConnection.CreateConnection();
        return connection.Query<OrderOccurrence>(sql, parameters);
    }

    public async Task<IReadOnlyList<OrderOccurrence>> SearchGetByAsync(string? arg = null, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default)
    {
        ValidatePage(page, pageSize);
        var (sql, parameters) = BuildSearch(arg, (uint)pageSize, (uint)((page - 1) * pageSize));
        using var connection = dbConnection.CreateConnection();
        return (await connection.QueryAsync<OrderOccurrence>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken))).AsList();
    }

    public async Task<IReadOnlyList<OrderOccurrence>> GetHistoryForOrderAsync(int orderId, CancellationToken cancellationToken = default)
    {
        var (sql, parameters) = createCommandBuilder()
            .Select(s => s.AllColumns<OrderOccurrence>())
            .From<OrderOccurrence>()
            .Where(w => w.Equals((OrderOccurrence o) => o.OrderID, orderId))
            .OrderBy("oo.RegisteredAt DESC")
            .Build();
        using var connection = dbConnection.CreateConnection();
        return (await connection.QueryAsync<OrderOccurrence>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken))).AsList();
    }

    private (string Sql, object Parameters) BuildInsert(OrderOccurrence entity)
        => createCommandBuilder().Insert().Into<OrderOccurrence>().Values(entity).ReturningGeneratedId().Build();

    private (string Sql, object Parameters) BuildUpdate(OrderOccurrence entity)
        => createCommandBuilder().Update().Table<OrderOccurrence>().Set(entity).Where(w => w.Equals((OrderOccurrence o) => o.OccurrenceID, entity.OccurrenceID)).Build();

    private (string Sql, object Parameters) BuildDelete(int id)
        => createCommandBuilder().Delete<OrderOccurrence>().Where(w => w.Equals((OrderOccurrence o) => o.OccurrenceID, id)).Build();

    private (string Sql, object Parameters) BuildSearch(string? arg, uint? take, uint? skip)
    {
        var builder = createCommandBuilder()
            .Select(s => s.AllColumns<OrderOccurrence>())
            .From<OrderOccurrence>()
            .Where(w => w.WithDynamicSearchFilter<OrderOccurrence, OrderOccurrence>(arg, o => o.OccurrenceID, o => o.Observation))
            .OrderBy("oo.RegisteredAt DESC");
        if (take.HasValue) builder.Take(take.Value);
        if (skip.HasValue) builder.Skip(skip.Value);
        return builder.Build();
    }

    private static void ValidatePage(int page, int pageSize)
    {
        if (page < 1) throw new ArgumentOutOfRangeException(nameof(page));
        if (pageSize is < 1 or > 500) throw new ArgumentOutOfRangeException(nameof(pageSize));
    }
}