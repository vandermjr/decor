using Dapper;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Data;
using Decor.Core.Interfaces.Repositories;
using Decor.FluentSqlBuilder;

namespace Decor.Infrastructure.Data.Repositories;

public class GroupRepository(IDatabaseConnection dbConnection, Func<FluentCommandBuilder> createCommandBuilder) : IGroupRepository
{
    private readonly IDatabaseConnection _dbConnection = dbConnection;
    private readonly Func<FluentCommandBuilder> _createCommandBuilder = createCommandBuilder;

    public IEnumerable<Group> GetByFamilyId(int familyId)
    {
        var (sql, parameters) = _createCommandBuilder()
            .Select(s => s.Columns<Group>(g => g.GroupID, g => g.GroupName))
            .From<Group>()
            .Where(w => w.Equals((Group g) => g.FamilyID, familyId))
            .OrderBy("g.GroupName ASC")
            .Build();
        using var conn = _dbConnection.CreateConnection();
        return conn.Query<Group>(sql, parameters);
    }

    public async Task<IReadOnlyList<Group>> GetByFamilyIdAsync(int familyId, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default)
    {
        ValidatePage(page, pageSize);
        var (sql, parameters) = _createCommandBuilder()
            .Select(s => s.Columns<Group>(g => g.GroupID, g => g.GroupName, g => g.FamilyID))
            .From<Group>()
            .Where(w => w.Equals((Group g) => g.FamilyID, familyId))
            .OrderBy("g.GroupName ASC")
            .Take((uint)pageSize)
            .Skip((uint)((page - 1) * pageSize))
            .Build();
        using var connection = _dbConnection.CreateConnection();
        var result = await connection.QueryAsync<Group>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        return result.AsList();
    }

    public int Save(Group group)
    {
        var (sql, parameters) = group.GroupID != 0
            ? _createCommandBuilder().Update().Table<Group>().Set(group).Where(w => w.Equals((Group g) => g.GroupID, group.GroupID)).Build()
            : _createCommandBuilder().Insert().Into<Group>().Values(group).Build();
        using var conn = _dbConnection.CreateConnection();
        return conn.Execute(sql, parameters);
    }

    public int Delete(int id)
    {
        var (sql, parameters) = _createCommandBuilder()
            .Delete<Group>()
            .Where(w => w.Equals((Group g) => g.GroupID, id))
            .Build();
        using var conn = _dbConnection.CreateConnection();
        return conn.Execute(sql, parameters);
    }

    public IEnumerable<Group> SearchGetBy(string? arg = null)
    {
        var (sql, parameters) = _createCommandBuilder()
            .Select(s => s.Columns<Group>(g => g.GroupID, g => g.GroupName))
            .From<Group>()
            .Where(w => w.WithDynamicSearchFilter<Group, Group>(arg, g => g.GroupID, g => g.GroupName))
            .OrderBy("g.GroupName ASC")
            .Build();
        using var conn = _dbConnection.CreateConnection();
        return conn.Query<Group>(sql, parameters);
    }

    public async Task<int> SaveAsync(Group group, CancellationToken cancellationToken = default)
    {
        var (sql, parameters) = group.GroupID != 0
            ? _createCommandBuilder().Update().Table<Group>().Set(group).Where(w => w.Equals((Group g) => g.GroupID, group.GroupID)).Build()
            : _createCommandBuilder().Insert().Into<Group>().Values(group).Build();
        return await ExecuteWriteAsync(sql, parameters, cancellationToken);
    }

    public async Task<int> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var (sql, parameters) = _createCommandBuilder()
            .Delete<Group>()
            .Where(w => w.Equals((Group g) => g.GroupID, id))
            .Build();
        return await ExecuteWriteAsync(sql, parameters, cancellationToken);
    }

    public async Task<IReadOnlyList<Group>> SearchGetByAsync(string? arg = null, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default)
    {
        ValidatePage(page, pageSize);
        var (sql, parameters) = _createCommandBuilder()
            .Select(s => s.Columns<Group>(g => g.GroupID, g => g.GroupName))
            .From<Group>()
            .Where(w => w.WithDynamicSearchFilter<Group, Group>(arg, g => g.GroupID, g => g.GroupName))
            .OrderBy("g.GroupName ASC")
            .Take((uint)pageSize)
            .Skip((uint)((page - 1) * pageSize))
            .Build();
        using var connection = _dbConnection.CreateConnection();
        var result = await connection.QueryAsync<Group>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
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