using System.Data;
using Dapper;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Data;
using Decor.Core.Interfaces.Repositories;
using Decor.FluentSqlBuilder;

namespace Decor.Infrastructure.Data.Repositories;
public sealed class StockMovementRepository(IDatabaseConnection databaseConnection, Func<FluentCommandBuilder> createCommandBuilder) : IStockMovementRepository, ITransactionalStockMovementRepository
{
    public async Task<int> RegisterMovementAsync(StockMovement movement, CancellationToken cancellationToken = default)
    {
        using var connection = databaseConnection.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();
        try
        {
            var id = await InsertMovementAsync(connection, transaction, movement, cancellationToken);
            await ApplyMovementToBalanceAsync(connection, transaction, movement, cancellationToken);

            transaction.Commit();
            return id;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task<int> RegisterMovementAsync(StockMovement movement, IDbConnection connection, IDbTransaction transaction, CancellationToken cancellationToken = default)
    {
        var id = await InsertMovementAsync(connection, transaction, movement, cancellationToken);
        await ApplyMovementToBalanceAsync(connection, transaction, movement, cancellationToken);
        return id;
    }

    public async Task<Guid> RegisterTransferAsync(StockMovement outboundMovement, StockMovement inboundMovement, CancellationToken cancellationToken = default)
    {
        var transferId = Guid.NewGuid();
        outboundMovement.TransferID = transferId;
        inboundMovement.TransferID = transferId;

        using var connection = databaseConnection.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();
        try
        {
            await InsertMovementAsync(connection, transaction, outboundMovement, cancellationToken);
            await ApplyMovementToBalanceAsync(connection, transaction, outboundMovement, cancellationToken);

            await InsertMovementAsync(connection, transaction, inboundMovement, cancellationToken);
            await ApplyMovementToBalanceAsync(connection, transaction, inboundMovement, cancellationToken);

            transaction.Commit();
            return transferId;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task<StockMovement?> GetByIdAsync(int stockMovementId, CancellationToken cancellationToken = default)
    {
        var (sql, parameters) = createCommandBuilder()
            .Select(s => s.AllColumns<StockMovement>())
            .From<StockMovement>()
            .Where(w => w.Equals<StockMovement>(sm => sm.StockMovementID, stockMovementId))
            .Build();

        using var connection = databaseConnection.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<StockMovement>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }

    public async Task<IEnumerable<StockMovement>> GetByProductAsync(int productId, CancellationToken cancellationToken = default)
    {
        var (sql, parameters) = createCommandBuilder()
            .Select(s => s.AllColumns<StockMovement>())
            .From<StockMovement>()
            .Where(w => w.Equals<StockMovement>(sm => sm.ProductID, productId))
            .OrderBy("sm.MovementDate DESC, sm.StockMovementID DESC")
            .Build();

        using var connection = databaseConnection.CreateConnection();
        var result = await connection.QueryAsync<StockMovement>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        return result.AsList();
    }

    public async Task<IEnumerable<StockMovement>> GetByTransferIdAsync(Guid transferId, CancellationToken cancellationToken = default)
    {
        var (sql, parameters) = createCommandBuilder()
            .Select(s => s.AllColumns<StockMovement>())
            .From<StockMovement>()
            .Where(w => w.Equals<StockMovement>(sm => sm.TransferID, transferId))
            .OrderBy("sm.StockMovementID ASC")
            .Build();

        using var connection = databaseConnection.CreateConnection();
        var result = await connection.QueryAsync<StockMovement>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        return result.AsList();
    }

    public async Task<int> UpdateReviewAsync(Guid transferId, StockMovementReviewStatus reviewStatus, int reviewedByEmployeeId, DateTime reviewedAt, CancellationToken cancellationToken = default)
    {
        var (sql, parameters) = createCommandBuilder()
            .Update(u => u.Entity<StockMovement>(sm => sm.ReviewStatus, reviewStatus)
                .Entity<StockMovement>(sm => sm.ReviewedByEmployeeID, reviewedByEmployeeId)
                .Entity<StockMovement>(sm => sm.ReviewedAt, reviewedAt))
            .Where(w => w.Equals<StockMovement>(sm => sm.TransferID, transferId))
            .Build();

        using var connection = databaseConnection.CreateConnection();
        return await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }

    private async Task<int> InsertMovementAsync(IDbConnection connection, IDbTransaction transaction, StockMovement movement, CancellationToken cancellationToken)
    {
        var (sql, parameters) = createCommandBuilder()
            .Insert(i => i.Entity(movement))
            .ReturningGeneratedId()
            .Build();

        return await connection.QuerySingleAsync<int>(new CommandDefinition(sql, parameters, transaction, cancellationToken: cancellationToken));
    }

    private async Task ApplyMovementToBalanceAsync(IDbConnection connection, IDbTransaction transaction, StockMovement movement, CancellationToken cancellationToken)
    {
        var (lockSql, lockParameters) = createCommandBuilder()
            .Select(s => s.AllColumns<StockBalance>())
            .From<StockBalance>()
            .Where(w =>
            {
                w.Equals<StockBalance>(sb => sb.ProductID, movement.ProductID);
                w.Equals<StockBalance>(sb => sb.StockLocationID, movement.StockLocationID);
            })
            .ForUpdate()
            .Build();
        var balance = await connection.QuerySingleOrDefaultAsync<StockBalance>(new CommandDefinition(lockSql, lockParameters, transaction, cancellationToken: cancellationToken));

        if (balance is null)
        {
            await InsertBalanceAsync(connection, transaction, movement, cancellationToken);
            return;
        }

        await UpdateBalanceAsync(connection, transaction, balance.StockBalanceID, balance.Quantity + movement.Quantity, movement.MovementDate, cancellationToken);
    }

    private async Task InsertBalanceAsync(IDbConnection connection, IDbTransaction transaction, StockMovement movement, CancellationToken cancellationToken)
    {
        var balance = new StockBalance
        {
            ProductID = movement.ProductID,
            StockLocationID = movement.StockLocationID,
            Quantity = movement.Quantity,
            UpdatedAt = movement.MovementDate
        };

        var (sql, parameters) = createCommandBuilder()
            .Insert(i => i.Entity(balance))
            .Build();

        await connection.ExecuteAsync(new CommandDefinition(sql, parameters, transaction, cancellationToken: cancellationToken));
    }

    private async Task UpdateBalanceAsync(IDbConnection connection, IDbTransaction transaction, int stockBalanceId, decimal quantity, DateTime updatedAt, CancellationToken cancellationToken)
    {
        var (sql, parameters) = createCommandBuilder()
            .Update(u => u.Entity<StockBalance>(sb => sb.Quantity, quantity)
                .Entity<StockBalance>(sb => sb.UpdatedAt, updatedAt))
            .Where(w => w.Equals<StockBalance>(sb => sb.StockBalanceID, stockBalanceId))
            .Build();

        await connection.ExecuteAsync(new CommandDefinition(sql, parameters, transaction, cancellationToken: cancellationToken));
    }
}