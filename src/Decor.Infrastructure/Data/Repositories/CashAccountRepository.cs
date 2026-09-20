using Dapper;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Data;
using Decor.Core.Interfaces.Repositories;
using Decor.FluentSqlBuilder;

namespace Decor.Infrastructure.Data.Repositories;

public sealed class CashAccountRepository(IDatabaseConnection databaseConnection, Func<FluentCommandBuilder> createCommandBuilder) : ICashAccountRepository
{
    private readonly IDatabaseConnection _databaseConnection = databaseConnection;
    private readonly Func<FluentCommandBuilder> _createCommandBuilder = createCommandBuilder;

    public async Task<int> SaveAsync(CashAccount account, CancellationToken cancellationToken = default)
    {
        var builder = _createCommandBuilder();
        var (sql, parameters) = account.CashAccountID == 0
            ? builder
                .Insert(i => i.Entity(account))
                .ReturningGeneratedId()
                .Build()
            : builder
                .Update(u => u.Entity(account))
                .Where(w => w.Equals<CashAccount>(a => a.CashAccountID, account.CashAccountID))
                .Build();
        using var connection = _databaseConnection.CreateConnection();
        if (account.CashAccountID == 0)
        {
            account.CashAccountID = await connection.QuerySingleAsync<int>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
            return 1;
        }
        return await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }

    public async Task<CashAccount?> GetByIdAsync(int cashAccountId, CancellationToken cancellationToken = default)
    {
        var (sql, parameters) = _createCommandBuilder()
            .Select(s => s.AllColumns<CashAccount>())
            .From<CashAccount>()
            .Where(w => w.Equals<CashAccount>(a => a.CashAccountID, cashAccountId))
            .Build();
        using var connection = _databaseConnection.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<CashAccount>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<CashAccount>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var (sql, parameters) = _createCommandBuilder()
            .Select(s => s.AllColumns<CashAccount>())
            .From<CashAccount>()
            .OrderBy(o => o.Ascending<CashAccount>(a => a.Name))
            .Build();
        using var connection = _databaseConnection.CreateConnection();
        return (await connection.QueryAsync<CashAccount>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken))).AsList();
    }

    public async Task<bool> NameExistsAsync(string name, int currentCashAccountId, CancellationToken cancellationToken = default)
    {
        var (sql, parameters) = _createCommandBuilder()
            .Select(s => s.Count())
            .From<CashAccount>()
            .Where(w => w.Equals<CashAccount>(a => a.Name, name).NotEquals<CashAccount>(a => a.CashAccountID, currentCashAccountId))
            .Build();
        using var connection = _databaseConnection.CreateConnection();
        return await connection.QuerySingleAsync<int>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken)) > 0;
    }

    public async Task<int> DeactivateAsync(int cashAccountId, CancellationToken cancellationToken = default)
    {
        var (sql, parameters) = _createCommandBuilder()
            .Update(u => u.Entity<CashAccount>(a => a.IsActive, false))
            .Where(w => w.Equals<CashAccount>(a => a.CashAccountID, cashAccountId))
            .Build();
        using var connection = _databaseConnection.CreateConnection();
        return await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }
}