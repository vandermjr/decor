using Dapper;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Data;
using Decor.Core.Interfaces.Repositories;
using Decor.FluentSqlBuilder;

namespace Decor.Infrastructure.Data.Repositories;

public class PartnerPriceTableRepository(IDatabaseConnection dbConnection, Func<FluentCommandBuilder> createCommandBuilder) : IPartnerPriceTableRepository
{
    private readonly IDatabaseConnection _dbConnection = dbConnection;
    private readonly Func<FluentCommandBuilder> _createCommandBuilder = createCommandBuilder;

    public int Save(PartnerPriceTable table)
    {
        using var connection = _dbConnection.CreateConnection();

        var (sql, parameters) = table.PriceTableID != 0
            ? _createCommandBuilder().Update(u => u.Entity(table)).Where(w => w.Equals<PartnerPriceTable>(p => p.PriceTableID, table.PriceTableID)).Build()
            : _createCommandBuilder().Insert(i => i.Entity(table)).Build();

        return connection.Execute(sql, parameters);
    }

    public async Task<int> SaveAsync(PartnerPriceTable table, CancellationToken cancellationToken = default)
    {
        var (sql, parameters) = table.PriceTableID != 0
            ? _createCommandBuilder().Update(u => u.Entity(table)).Where(w => w.Equals<PartnerPriceTable>(p => p.PriceTableID, table.PriceTableID)).Build()
            : _createCommandBuilder().Insert(i => i.Entity(table)).Build();

        using var connection = _dbConnection.CreateConnection();
        return await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }

    public int Delete(int priceTableId)
    {
        var (sql, parameters) = _createCommandBuilder()
            .Delete<PartnerPriceTable>()
            .Where(w => w.Equals<PartnerPriceTable>(p => p.PriceTableID, priceTableId))
            .Build();

        using var connection = _dbConnection.CreateConnection();
        return connection.Execute(sql, parameters);
    }

    public async Task<int> DeleteAsync(int priceTableId, CancellationToken cancellationToken = default)
    {
        var (sql, parameters) = _createCommandBuilder()
            .Delete<PartnerPriceTable>()
            .Where(w => w.Equals<PartnerPriceTable>(p => p.PriceTableID, priceTableId))
            .Build();

        using var connection = _dbConnection.CreateConnection();
        return await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }

    public IEnumerable<PartnerPriceTable> SearchGetBy(string? arg = null)
    {
        var (sql, parameters) = _createCommandBuilder()
            .Select(s => s.AllColumns<PartnerPriceTable>())
            .From<PartnerPriceTable>()
            .Where(w => w.WithDynamicSearchFilter<PartnerPriceTable, PartnerPriceTable>(arg, p => p.PriceTableID, p => p.PartnerID))
            .OrderBy("ppt.PriceTableID ASC")
            .Build();

        using var connection = _dbConnection.CreateConnection();
        return connection.Query<PartnerPriceTable>(sql, parameters);
    }

    public async Task<IReadOnlyList<PartnerPriceTable>> SearchGetByAsync(string? arg = null, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default)
    {
        ValidatePage(page, pageSize);
        var (sql, parameters) = _createCommandBuilder()
            .Select(s => s.AllColumns<PartnerPriceTable>())
            .From<PartnerPriceTable>()
            .Where(w => w.WithDynamicSearchFilter<PartnerPriceTable, PartnerPriceTable>(arg, p => p.PriceTableID, p => p.PartnerID))
            .OrderBy("ppt.PriceTableID ASC")
            .Take((uint)pageSize)
            .Skip((uint)((page - 1) * pageSize))
            .Build();

        using var connection = _dbConnection.CreateConnection();
        var result = await connection.QueryAsync<PartnerPriceTable>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        return result.AsList();
    }

    public async Task<PartnerPriceTable?> GetByIdAsync(int priceTableId, CancellationToken cancellationToken = default)
    {
        var (sql, parameters) = _createCommandBuilder()
            .Select(s => s.AllColumns<PartnerPriceTable>())
            .From<PartnerPriceTable>()
            .Where(w => w.Equals<PartnerPriceTable>(p => p.PriceTableID, priceTableId))
            .Build();

        using var connection = _dbConnection.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<PartnerPriceTable>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }

    public async Task<PartnerPriceTable?> GetActiveByPartnerAndGroupAsync(int partnerId, int groupId, CancellationToken cancellationToken = default)
    {
        var (sql, parameters) = _createCommandBuilder()
            .Select(s => s.AllColumns<PartnerPriceTable>())
            .From<PartnerPriceTable>()
            .Where(w => w
                .Equals<PartnerPriceTable>(p => p.PartnerID, partnerId)
                .Equals<PartnerPriceTable>(p => p.GroupID, groupId)
                .Equals<PartnerPriceTable>(p => p.IsActive, true))
            .Build();

        using var connection = _dbConnection.CreateConnection();
        return await connection.QueryFirstOrDefaultAsync<PartnerPriceTable>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }

    public async Task<IEnumerable<PartnerPriceTable>> GetByPartnerIdAsync(int partnerId, CancellationToken cancellationToken = default)
    {
        var (sql, parameters) = _createCommandBuilder()
            .Select(s => s.AllColumns<PartnerPriceTable>())
            .From<PartnerPriceTable>()
            .Where(w => w.Equals<PartnerPriceTable>(p => p.PartnerID, partnerId))
            .OrderBy("ppt.PriceTableID ASC")
            .Build();

        using var connection = _dbConnection.CreateConnection();
        var result = await connection.QueryAsync<PartnerPriceTable>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        return result.AsList();
    }

    public bool ActiveEntryExists(int partnerId, int groupId, int currentPriceTableId = 0)
    {
        var (sql, parameters) = _createCommandBuilder()
            .Select(s => s.Count())
            .From<PartnerPriceTable>()
            .Where(w =>
            {
                w.Equals<PartnerPriceTable>(p => p.PartnerID, partnerId);
                w.Equals<PartnerPriceTable>(p => p.GroupID, groupId);
                w.Equals<PartnerPriceTable>(p => p.IsActive, true);
                if (currentPriceTableId != 0)
                {
                    w.NotEquals<PartnerPriceTable>(p => p.PriceTableID, currentPriceTableId);
                }
            })
            .Build();

        using var connection = _dbConnection.CreateConnection();
        int count = connection.QuerySingle<int>(sql, parameters);
        return count > 0;
    }

    public async Task<bool> ActiveEntryExistsAsync(int partnerId, int groupId, int currentPriceTableId = 0, CancellationToken cancellationToken = default)
    {
        var (sql, parameters) = _createCommandBuilder()
            .Select(s => s.Count())
            .From<PartnerPriceTable>()
            .Where(w =>
            {
                w.Equals<PartnerPriceTable>(p => p.PartnerID, partnerId);
                w.Equals<PartnerPriceTable>(p => p.GroupID, groupId);
                w.Equals<PartnerPriceTable>(p => p.IsActive, true);
                if (currentPriceTableId != 0)
                {
                    w.NotEquals<PartnerPriceTable>(p => p.PriceTableID, currentPriceTableId);
                }
            })
            .Build();

        using var connection = _dbConnection.CreateConnection();
        int count = await connection.QuerySingleAsync<int>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        return count > 0;
    }

    private static void ValidatePage(int page, int pageSize)
    {
        if (page < 1) throw new ArgumentOutOfRangeException(nameof(page));
        if (pageSize is < 1 or > 500) throw new ArgumentOutOfRangeException(nameof(pageSize));
    }
}
