using System.Data;
using Dapper;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Data;
using Decor.Core.Interfaces.Repositories;
using Decor.FluentSqlBuilder;

namespace Decor.Infrastructure.Data.Repositories;

public sealed class CashTransactionRepository(IDatabaseConnection databaseConnection, Func<FluentCommandBuilder> createCommandBuilder) : ICashTransactionRepository
{
    public async Task<int> RegisterAsync(CashTransaction transaction, CancellationToken cancellationToken = default)
    {
        using var connection = databaseConnection.CreateConnection();
        var (sql, parameters) = createCommandBuilder().Insert().Into<CashTransaction>().Values(transaction).ReturningGeneratedId().Build();
        transaction.CashTransactionID = await connection.QuerySingleAsync<int>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        return transaction.CashTransactionID;
    }

    public async Task<Guid> RegisterTransferAsync(CashTransaction expense, CashTransaction income, CancellationToken cancellationToken = default)
    {
        var transferId = Guid.NewGuid();
        expense.TransferID = transferId;
        income.TransferID = transferId;
        using var connection = databaseConnection.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();
        try
        {
            await InsertAsync(connection, transaction, expense, cancellationToken);
            await InsertAsync(connection, transaction, income, cancellationToken);
            transaction.Commit();
            return transferId;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task<CashTransaction?> GetByIdAsync(int cashTransactionId, CancellationToken cancellationToken = default)
    {
        var (sql, parameters) = createCommandBuilder().Select(s => s.AllColumns<CashTransaction>()).From<CashTransaction>()
            .Where(w => w.Equals((CashTransaction t) => t.CashTransactionID, cashTransactionId)).Build();
        using var connection = databaseConnection.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<CashTransaction>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<CashTransaction>> GetByCashAccountAsync(int cashAccountId, CancellationToken cancellationToken = default)
    {
        var (sql, parameters) = createCommandBuilder().Select(s => s.AllColumns<CashTransaction>()).From<CashTransaction>()
            .Where(w => w.Equals((CashTransaction t) => t.CashAccountID, cashAccountId)).OrderBy("ct.TransactionDate ASC, ct.CashTransactionID ASC").Build();
        using var connection = databaseConnection.CreateConnection();
        return (await connection.QueryAsync<CashTransaction>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken))).AsList();
    }

    public async Task<decimal> GetBalanceAsync(int cashAccountId, CancellationToken cancellationToken = default)
    {
        var balanceExpression =
            $"CASE WHEN ct.TransactionType = {(int)CashTransactionType.Income} THEN ct.Amount " +
            $"WHEN ct.TransactionType = {(int)CashTransactionType.Expense} THEN -ct.Amount ELSE 0 END";

        var (sql, parameters) = createCommandBuilder()
            .Select(s => s.Sum(balanceExpression, "Balance"))
            .From<CashTransaction>()
            .Where(w => w.Equals((CashTransaction t) => t.CashAccountID, cashAccountId))
            .Build();

        using var connection = databaseConnection.CreateConnection();
        var balance = await connection.QuerySingleOrDefaultAsync<decimal?>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        return balance ?? 0m;
    }

    private async Task InsertAsync(IDbConnection connection, IDbTransaction transaction, CashTransaction entity, CancellationToken cancellationToken)
    {
        var (sql, parameters) = createCommandBuilder().Insert().Into<CashTransaction>().Values(entity).ReturningGeneratedId().Build();
        entity.CashTransactionID = await connection.QuerySingleAsync<int>(new CommandDefinition(sql, parameters, transaction, cancellationToken: cancellationToken));
    }
}