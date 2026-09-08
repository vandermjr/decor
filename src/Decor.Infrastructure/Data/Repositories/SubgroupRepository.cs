using Dapper;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Data;
using Decor.Core.Interfaces.Repositories;
using Decor.FluentSqlBuilder;

namespace Decor.Infrastructure.Data.Repositories;

public class SubgroupRepository(IDatabaseConnection dbConnection, Func<FluentCommandBuilder> createCommandBuilder) : ISubgroupRepository
{
    private readonly IDatabaseConnection _dbConnection = dbConnection;
    private readonly Func<FluentCommandBuilder> _createCommandBuilder = createCommandBuilder;

    public IEnumerable<Subgroup> GetByGroupId(int groupId)
    {
        var (sql, parameters) = _createCommandBuilder()
            .Select(s => s.Columns<Subgroup>(sg => sg.SubgroupID, sg => sg.SubgroupName))
            .From<Subgroup>()
            .Where(w => w.Equals((Subgroup sg) => sg.GroupID, groupId))
            .OrderBy("s.SubgroupName ASC")
            .Build();
        using var conn = _dbConnection.CreateConnection();
        return conn.Query<Subgroup>(sql, parameters);
    }

    public async Task<IReadOnlyList<Subgroup>> GetByGroupIdAsync(int groupId, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default)
    {
        ValidatePage(page, pageSize);
        var (sql, parameters) = _createCommandBuilder()
            .Select(s => s.Columns<Subgroup>(sg => sg.SubgroupID, sg => sg.SubgroupName, sg => sg.GroupID))
            .From<Subgroup>()
            .Where(w => w.Equals((Subgroup sg) => sg.GroupID, groupId))
            .OrderBy("s.SubgroupName ASC")
            .Take((uint)pageSize)
            .Skip((uint)((page - 1) * pageSize))
            .Build();
        using var connection = _dbConnection.CreateConnection();
        var result = await connection.QueryAsync<Subgroup>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        return result.AsList();
    }

    public int Save(Subgroup subgroup)
    {
        var (sql, parameters) = subgroup.SubgroupID != 0
            ? _createCommandBuilder().Update().Table<Subgroup>().Set(subgroup).Where(w => w.Equals((Subgroup sg) => sg.SubgroupID, subgroup.SubgroupID)).Build()
            : _createCommandBuilder().Insert().Into<Subgroup>().Values(subgroup).Build();
        using var conn = _dbConnection.CreateConnection();
        return conn.Execute(sql, parameters);
    }

    public int Delete(int id)
    {
        var (sql, parameters) = _createCommandBuilder()
            .Delete<Subgroup>()
            .Where(w => w.Equals((Subgroup sg) => sg.SubgroupID, id))
            .Build();
        using var conn = _dbConnection.CreateConnection();
        return conn.Execute(sql, parameters);
    }

    public IEnumerable<Subgroup> SearchGetBy(string? arg = null)
    {
        var (sql, parameters) = _createCommandBuilder()
            .Select(s => s.Columns<Subgroup>(sg => sg.SubgroupID, sg => sg.SubgroupName))
            .From<Subgroup>()
            .Where(w => w.WithDynamicSearchFilter<Subgroup, Subgroup>(arg, sg => sg.SubgroupID, sg => sg.SubgroupName))
            .OrderBy("s.SubgroupName ASC")
            .Build();
        using var conn = _dbConnection.CreateConnection();
        return conn.Query<Subgroup>(sql, parameters);
    }

    public async Task<int> SaveAsync(Subgroup subgroup, CancellationToken cancellationToken = default)
    {
        var (sql, parameters) = subgroup.SubgroupID != 0
            ? _createCommandBuilder().Update().Table<Subgroup>().Set(subgroup).Where(w => w.Equals((Subgroup sg) => sg.SubgroupID, subgroup.SubgroupID)).Build()
            : _createCommandBuilder().Insert().Into<Subgroup>().Values(subgroup).Build();
        return await ExecuteWriteAsync(sql, parameters, cancellationToken);
    }

    public async Task<int> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var (sql, parameters) = _createCommandBuilder()
            .Delete<Subgroup>()
            .Where(w => w.Equals((Subgroup sg) => sg.SubgroupID, id))
            .Build();
        return await ExecuteWriteAsync(sql, parameters, cancellationToken);
    }

    public async Task<IReadOnlyList<Subgroup>> SearchGetByAsync(string? arg = null, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default)
    {
        ValidatePage(page, pageSize);
        var (sql, parameters) = _createCommandBuilder()
            .Select(s => s.Columns<Subgroup>(sg => sg.SubgroupID, sg => sg.SubgroupName))
            .From<Subgroup>()
            .Where(w => w.WithDynamicSearchFilter<Subgroup, Subgroup>(arg, sg => sg.SubgroupID, sg => sg.SubgroupName))
            .OrderBy("s.SubgroupName ASC")
            .Take((uint)pageSize)
            .Skip((uint)((page - 1) * pageSize))
            .Build();
        using var connection = _dbConnection.CreateConnection();
        var result = await connection.QueryAsync<Subgroup>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        return result.AsList();
    }

    private async Task<int> ExecuteWriteAsync(string sql, object parameters, CancellationToken cancellationToken)
    {
        using var connection = _dbConnection.CreateConnection();
        return await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }

    private static void ValidatePage(int page, int pageSize)
    {
        if (page < 1) throw new ArgumentOutOfRangeException(nameof(page));
        if (pageSize is < 1 or > 500) throw new ArgumentOutOfRangeException(nameof(pageSize));
    }
}