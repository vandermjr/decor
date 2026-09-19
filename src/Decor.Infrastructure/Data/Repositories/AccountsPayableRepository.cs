using Dapper;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Data;
using Decor.Core.Interfaces.Repositories;
using Decor.FluentSqlBuilder;

namespace Decor.Infrastructure.Data.Repositories;

public class AccountsPayableRepository(IDatabaseConnection dbConnection, Func<FluentCommandBuilder> createCommandBuilder) : IAccountsPayableRepository
{
    public int Save(AccountsPayable entity)
    {
        using var connection = dbConnection.CreateConnection();
        var (sql, parameters) = entity.AccountsPayableID == 0
            ? createCommandBuilder().Insert(i => i.Entity(entity)).ReturningGeneratedId().Build()
            : createCommandBuilder().Update(u => u.Entity(entity)).Where(w => w.Equals<AccountsPayable>(a => a.AccountsPayableID, entity.AccountsPayableID)).Build();
        if (entity.AccountsPayableID == 0) entity.AccountsPayableID = connection.QuerySingle<int>(sql, parameters);
        return 1;
    }

    public async Task<int> SaveAsync(AccountsPayable entity, CancellationToken cancellationToken = default)
    {
        using var connection = dbConnection.CreateConnection();
        var (sql, parameters) = entity.AccountsPayableID == 0
            ? createCommandBuilder().Insert(i => i.Entity(entity)).ReturningGeneratedId().Build()
            : createCommandBuilder().Update(u => u.Entity(entity)).Where(w => w.Equals<AccountsPayable>(a => a.AccountsPayableID, entity.AccountsPayableID)).Build();
        if (entity.AccountsPayableID == 0)
        {
            entity.AccountsPayableID = await connection.QuerySingleAsync<int>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
            return 1;
        }
        return await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }

    public int Delete(int id) => throw new NotSupportedException("Contas a pagar não podem ser excluídas.");
    public Task<int> DeleteAsync(int id, CancellationToken cancellationToken = default) => throw new NotSupportedException("Contas a pagar não podem ser excluídas.");

    public IEnumerable<AccountsPayable> SearchGetBy(string? arg = null) => throw new NotSupportedException();
    public async Task<IReadOnlyList<AccountsPayable>> SearchGetByAsync(string? arg = null, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default)
    {
        if (page < 1) throw new ArgumentOutOfRangeException(nameof(page));
        if (pageSize is < 1 or > 500) throw new ArgumentOutOfRangeException(nameof(pageSize));
        var (sql, parameters) = createCommandBuilder().Select(s => s.AllColumns<AccountsPayable>())
            .From<AccountsPayable>()
            .Where(w => w.WithDynamicSearchFilter<AccountsPayable, AccountsPayable>(arg, a => a.AccountsPayableID, a => a.PayeeID))
            .OrderBy("ap.AccountsPayableID ASC").Take((uint)pageSize).Skip((uint)((page - 1) * pageSize)).Build();
        using var connection = dbConnection.CreateConnection();
        return (await connection.QueryAsync<AccountsPayable>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken))).AsList();
    }

    public async Task<AccountsPayable?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var (sql, parameters) = createCommandBuilder().Select(s => s.AllColumns<AccountsPayable>()).From<AccountsPayable>()
            .Where(w => w.Equals<AccountsPayable>(a => a.AccountsPayableID, id)).Build();
        using var connection = dbConnection.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<AccountsPayable>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }
}