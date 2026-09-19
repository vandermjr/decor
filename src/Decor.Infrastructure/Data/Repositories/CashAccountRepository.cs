using Dapper;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Data;
using Decor.Core.Interfaces.Repositories;
using Decor.FluentSqlBuilder;

namespace Decor.Infrastructure.Data.Repositories;

public sealed class CashAccountRepository(IDatabaseConnection databaseConnection, Func<FluentCommandBuilder> createCommandBuilder) : ICashAccountRepository
{
    public async Task<int> SaveAsync(CashAccount account, CancellationToken cancellationToken = default)
    {
        var builder = createCommandBuilder();
        var (sql, parameters) = account.CashAccountID == 0
            ? builder.Insert().Into<CashAccount>().Values(account).ReturningGeneratedId().Build()
            : builder.Update().Table<CashAccount>().Set(account).Where(w => w.Equals((CashAccount a) => a.CashAccountID, account.CashAccountID)).Build();
        using var connection = databaseConnection.CreateConnection();
        if (account.CashAccountID == 0)
        {
            account.CashAccountID = await connection.QuerySingleAsync<int>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
            return 1;
        }
        return await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }

    public async Task<CashAccount?> GetByIdAsync(int cashAccountId, CancellationToken cancellationToken = default)
    {
        var (sql, parameters) = createCommandBuilder().Select(s => s.AllColumns<CashAccount>()).From<CashAccount>()
            .Where(w => w.Equals<CashAccount>(a => a.CashAccountID, cashAccountId)).Build();
        using var connection = databaseConnection.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<CashAccount>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<CashAccount>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var (sql, parameters) = createCommandBuilder().Select(s => s.AllColumns<CashAccount>()).From<CashAccount>().OrderBy("ca.Name ASC").Build();
        using var connection = databaseConnection.CreateConnection();
        return (await connection.QueryAsync<CashAccount>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken))).AsList();
    }

    public async Task<bool> NameExistsAsync(string name, int currentCashAccountId, CancellationToken cancellationToken = default)
    {
        var (sql, parameters) = createCommandBuilder().Select(s => s.Count()).From<CashAccount>().Where(w =>
        {
            w.Equals<CashAccount>(a => a.Name, name);
            w.NotEquals<CashAccount>(a => a.CashAccountID, currentCashAccountId);
        }).Build();
        using var connection = databaseConnection.CreateConnection();
        return await connection.QuerySingleAsync<int>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken)) > 0;
    }

    public async Task<int> DeactivateAsync(int cashAccountId, CancellationToken cancellationToken = default)
    {
        var (sql, parameters) = createCommandBuilder().Update().Table<CashAccount>().Set((CashAccount a) => a.IsActive, false)
            .Where(w => w.Equals((CashAccount a) => a.CashAccountID, cashAccountId)).Build();
        using var connection = databaseConnection.CreateConnection();
        return await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }
}