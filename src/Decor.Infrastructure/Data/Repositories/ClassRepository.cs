using Dapper;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Data;
using Decor.Core.Interfaces.Repositories;
using Decor.FluentSqlBuilder;

namespace Decor.Infrastructure.Data.Repositories;

public class ClassRepository(IDatabaseConnection dbConnection, Func<FluentCommandBuilder> createCommandBuilder) : IClassRepository
{
    private readonly IDatabaseConnection _dbConnection = dbConnection;
    private readonly Func<FluentCommandBuilder> _createCommandBuilder = createCommandBuilder;

    public IEnumerable<Class> GetAll()
    {
        var (sql, parameters) = _createCommandBuilder()
            .Select(s => s.Columns<Class>(cl => cl.ClassID, cl => cl.ClassName))
            .From<Class>()
            .OrderBy("c.ClassName ASC")
            .Build();
        using var conn = _dbConnection.CreateConnection();
        return conn.Query<Class>(sql, parameters);
    }

    public async Task<IReadOnlyList<Class>> GetAllAsync(int page = 1, int pageSize = 100, CancellationToken cancellationToken = default)
    {
        ValidatePage(page, pageSize);
        var (sql, parameters) = _createCommandBuilder()
            .Select(s => s.Columns<Class>(cl => cl.ClassID, cl => cl.ClassName))
            .From<Class>()
            .OrderBy("c.ClassName ASC")
            .Take((uint)pageSize)
            .Skip((uint)((page - 1) * pageSize))
            .Build();
        using var connection = _dbConnection.CreateConnection();
        var result = await connection.QueryAsync<Class>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        return result.AsList();
    }

    public int Save(Class @class)
    {
        var (sql, parameters) = @class.ClassID != 0
            ? _createCommandBuilder().Update().Table<Class>().Set(@class).Where(w => w.Equals((Class cl) => cl.ClassID, @class.ClassID)).Build()
            : _createCommandBuilder().Insert().Into<Class>().Values(@class).Build();
        using var conn = _dbConnection.CreateConnection();
        return conn.Execute(sql, parameters);
    }

    public int Delete(int id)
    {
        var (sql, parameters) = _createCommandBuilder()
            .Delete<Class>()
            .Where(w => w.Equals((Class cl) => cl.ClassID, id))
            .Build();
        using var conn = _dbConnection.CreateConnection();
        return conn.Execute(sql, parameters);
    }

    public IEnumerable<Class> SearchGetBy(string? arg = null)
    {
        var (sql, parameters) = _createCommandBuilder()
            .Select(s => s.Columns<Class>(cl => cl.ClassID, cl => cl.ClassName))
            .From<Class>()
            .Where(w => w.WithDynamicSearchFilter<Class, Class>(arg, cl => cl.ClassID, cl => cl.ClassName))
            .OrderBy("c.ClassName ASC")
            .Build();
        using var conn = _dbConnection.CreateConnection();
        return conn.Query<Class>(sql, parameters);
    }

    public async Task<int> SaveAsync(Class @class, CancellationToken cancellationToken = default)
    {
        var (sql, parameters) = @class.ClassID != 0
            ? _createCommandBuilder().Update().Table<Class>().Set(@class).Where(w => w.Equals((Class cl) => cl.ClassID, @class.ClassID)).Build()
            : _createCommandBuilder().Insert().Into<Class>().Values(@class).Build();
        return await ExecuteWriteAsync(sql, parameters, cancellationToken);
    }

    public async Task<int> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var (sql, parameters) = _createCommandBuilder()
            .Delete<Class>()
            .Where(w => w.Equals((Class cl) => cl.ClassID, id))
            .Build();
        return await ExecuteWriteAsync(sql, parameters, cancellationToken);
    }

    public async Task<IReadOnlyList<Class>> SearchGetByAsync(string? arg = null, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default)
    {
        ValidatePage(page, pageSize);
        var (sql, parameters) = _createCommandBuilder()
            .Select(s => s.Columns<Class>(cl => cl.ClassID, cl => cl.ClassName))
            .From<Class>()
            .Where(w => w.WithDynamicSearchFilter<Class, Class>(arg, cl => cl.ClassID, cl => cl.ClassName))
            .OrderBy("c.ClassName ASC")
            .Take((uint)pageSize)
            .Skip((uint)((page - 1) * pageSize))
            .Build();
        using var connection = _dbConnection.CreateConnection();
        var result = await connection.QueryAsync<Class>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
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