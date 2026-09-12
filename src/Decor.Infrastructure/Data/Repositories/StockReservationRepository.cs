using Dapper;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Data;
using Decor.Core.Interfaces.Repositories;
using Decor.FluentSqlBuilder;

namespace Decor.Infrastructure.Data.Repositories;

public class StockReservationRepository(IDatabaseConnection dbConnection, Func<FluentCommandBuilder> createCommandBuilder) : IStockReservationRepository
{
    private readonly IDatabaseConnection _dbConnection = dbConnection;
    private readonly Func<FluentCommandBuilder> _createCommandBuilder = createCommandBuilder;

    public int Save(StockReservation reservation)
    {
        using var conn = _dbConnection.CreateConnection();
        if (reservation.ReservationID != 0)
        {
            var (sql, parameters) = _createCommandBuilder()
                .Update()
                .Table<StockReservation>()
                .Set(reservation)
                .Where(w => w.Equals((StockReservation r) => r.ReservationID, reservation.ReservationID))
                .Build();
            return conn.Execute(sql, parameters);
        }
        else
        {
            var (sql, parameters) = _createCommandBuilder()
                .Insert()
                .Into<StockReservation>()
                .Values(reservation)
                .ReturningGeneratedId()
                .Build();
            var generatedId = conn.QuerySingle<int>(sql, parameters);
            reservation.ReservationID = generatedId;
            return 1;
        }
    }

    public async Task<int> SaveAsync(StockReservation reservation, CancellationToken cancellationToken = default)
    {
        using var connection = _dbConnection.CreateConnection();
        if (reservation.ReservationID != 0)
        {
            var (sql, parameters) = _createCommandBuilder()
                .Update()
                .Table<StockReservation>()
                .Set(reservation)
                .Where(w => w.Equals((StockReservation r) => r.ReservationID, reservation.ReservationID))
                .Build();
            return await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        }
        else
        {
            var (sql, parameters) = _createCommandBuilder()
                .Insert()
                .Into<StockReservation>()
                .Values(reservation)
                .ReturningGeneratedId()
                .Build();
            var generatedId = await connection.QuerySingleAsync<int>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
            reservation.ReservationID = generatedId;
            return 1;
        }
    }

    public int Delete(int reservationId)
    {
        var (sql, parameters) = _createCommandBuilder()
            .Delete<StockReservation>()
            .Where(w => w.Equals((StockReservation r) => r.ReservationID, reservationId))
            .Build();

        using var conn = _dbConnection.CreateConnection();
        return conn.Execute(sql, parameters);
    }

    public async Task<int> DeleteAsync(int reservationId, CancellationToken cancellationToken = default)
    {
        var (sql, parameters) = _createCommandBuilder()
            .Delete<StockReservation>()
            .Where(w => w.Equals((StockReservation r) => r.ReservationID, reservationId))
            .Build();

        using var connection = _dbConnection.CreateConnection();
        return await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }

    public IEnumerable<StockReservation> SearchGetBy(string? arg = null)
    {
        var (sql, parameters) = _createCommandBuilder()
            .Select(s => s.AllColumns<StockReservation>())
            .From<StockReservation>()
            .Where(w => w.WithDynamicSearchFilter<StockReservation, StockReservation>(arg, sr => sr.ReservationID, sr => sr.ReservationID))
            .OrderBy("sr.ReservationID DESC")
            .Build();

        using var conn = _dbConnection.CreateConnection();
        return conn.Query<StockReservation>(sql, parameters);
    }

    public async Task<IReadOnlyList<StockReservation>> SearchGetByAsync(string? arg = null, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default)
    {
        ValidatePage(page, pageSize);
        var (sql, parameters) = _createCommandBuilder()
            .Select(s => s.AllColumns<StockReservation>())
            .From<StockReservation>()
            .Where(w => w.WithDynamicSearchFilter<StockReservation, StockReservation>(arg, sr => sr.ReservationID, sr => sr.ReservationID))
            .OrderBy("sr.ReservationID DESC")
            .Take((uint)pageSize)
            .Skip((uint)((page - 1) * pageSize))
            .Build();

        using var connection = _dbConnection.CreateConnection();
        var result = await connection.QueryAsync<StockReservation>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        return result.AsList();
    }

    public async Task<StockReservation?> GetByIdAsync(int reservationId, CancellationToken cancellationToken = default)
    {
        var (sql, parameters) = _createCommandBuilder()
            .Select(s => s.AllColumns<StockReservation>())
            .From<StockReservation>()
            .Where(w => w.Equals((StockReservation sr) => sr.ReservationID, reservationId))
            .Build();

        using var connection = _dbConnection.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<StockReservation>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }

    public async Task<IEnumerable<StockReservation>> GetByOrderItemIdAsync(int orderItemId, CancellationToken cancellationToken = default)
    {
        var (sql, parameters) = _createCommandBuilder()
            .Select(s => s.AllColumns<StockReservation>())
            .From<StockReservation>()
            .Where(w => w.Equals((StockReservation sr) => sr.OrderItemID, orderItemId))
            .OrderBy("sr.ReservationID DESC")
            .Build();

        using var connection = _dbConnection.CreateConnection();
        var result = await connection.QueryAsync<StockReservation>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        return result.AsList();
    }

    public async Task<IReadOnlyList<StockReservation>> GetByOrderIdAsync(int orderId, CancellationToken cancellationToken = default)
    {
        var (sql, parameters) = _createCommandBuilder()
            .Select(s => s.AllColumns<StockReservation>())
            .From<StockReservation>()
            .Join(j => j.Inner<StockReservation, OrderItem>((sr, oi) => sr.OrderItemID == oi.OrderItemID))
            .Where(w => w.Equals((OrderItem oi) => oi.OrderID, orderId))
            .OrderBy("sr.ReservationID ASC")
            .Build();

        using var connection = _dbConnection.CreateConnection();
        var result = await connection.QueryAsync<StockReservation>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        return result.AsList();
    }

    public async Task<IReadOnlyList<StockReservation>> GetActiveByOrderIdAsync(int orderId, CancellationToken cancellationToken = default)
    {
        var (sql, parameters) = _createCommandBuilder()
            .Select(s => s.AllColumns<StockReservation>())
            .From<StockReservation>()
            .Join(j => j.Inner<StockReservation, OrderItem>((sr, oi) => sr.OrderItemID == oi.OrderItemID))
            .Where(w =>
            {
                w.Equals((OrderItem oi) => oi.OrderID, orderId);
                w.Equals((StockReservation sr) => sr.Status, StockReservationStatus.Active);
            })
            .OrderBy("sr.ReservationID ASC")
            .Build();

        using var connection = _dbConnection.CreateConnection();
        var result = await connection.QueryAsync<StockReservation>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        return result.AsList();
    }

    public async Task<StockReservation?> GetActiveByOrderItemIdAsync(int orderItemId, CancellationToken cancellationToken = default)
    {
        var (sql, parameters) = _createCommandBuilder()
            .Select(s => s.AllColumns<StockReservation>())
            .From<StockReservation>()
            .Where(w =>
            {
                w.Equals((StockReservation sr) => sr.OrderItemID, orderItemId);
                w.Equals((StockReservation sr) => sr.Status, StockReservationStatus.Active);
            })
            .Build();

        using var connection = _dbConnection.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<StockReservation>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }

    public async Task<IEnumerable<StockReservation>> GetActiveByProductAndLocationAsync(int productId, int stockLocationId, CancellationToken cancellationToken = default)
    {
        var (sql, parameters) = _createCommandBuilder()
            .Select(s => s.AllColumns<StockReservation>())
            .From<StockReservation>()
            .Where(w =>
            {
                w.Equals((StockReservation sr) => sr.ProductID, productId);
                w.Equals((StockReservation sr) => sr.StockLocationID, stockLocationId);
                w.Equals((StockReservation sr) => sr.Status, StockReservationStatus.Active);
            })
            .OrderBy("sr.ReservationID ASC")
            .Build();

        using var connection = _dbConnection.CreateConnection();
        var result = await connection.QueryAsync<StockReservation>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        return result.AsList();
    }

    public async Task<decimal> GetTotalActiveReservedQuantityAsync(int productId, int stockLocationId, CancellationToken cancellationToken = default)
    {
        var active = await GetActiveByProductAndLocationAsync(productId, stockLocationId, cancellationToken);
        return active.Sum(r => r.Quantity);
    }

    private static void ValidatePage(int page, int pageSize)
    {
        if (page < 1) throw new ArgumentOutOfRangeException(nameof(page));
        if (pageSize is < 1 or > 500) throw new ArgumentOutOfRangeException(nameof(pageSize));
    }
}
