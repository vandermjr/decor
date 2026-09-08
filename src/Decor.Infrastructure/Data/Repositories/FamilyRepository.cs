using Dapper;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Data;
using Decor.Core.Interfaces.Repositories;
using Decor.FluentSqlBuilder;

namespace Decor.Infrastructure.Data.Repositories;
public class FamilyRepository(IDatabaseConnection dbConnection, Func<FluentCommandBuilder> createCommandBuilder) : IFamilyRepository
{
    private readonly IDatabaseConnection _dbConnection = dbConnection;
    private readonly Func<FluentCommandBuilder> _createCommandBuilder = createCommandBuilder;

    public IEnumerable<Family> GetByClassId(int classId)
    {
        var (sql, parameters) = _createCommandBuilder()
            .Select(s => s.Columns<Family>(f => f.FamilyID, f => f.FamilyName))
            .From<Family>()
            .Where(w => w.Equals((Family f) => f.ClassID, classId))
            .OrderBy("f.FamilyName ASC")
            .Build();
        using var conn = _dbConnection.CreateConnection();
        return conn.Query<Family>(sql, parameters);
    }

    public async Task<IReadOnlyList<Family>> GetByClassIdAsync(int classId, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default)
    {
        ValidatePage(page, pageSize);
        var (sql, parameters) = _createCommandBuilder()
            .Select(s => s.Columns<Family>(f => f.FamilyID, f => f.FamilyName, f => f.ClassID))
            .From<Family>()
            .Where(w => w.Equals((Family f) => f.ClassID, classId))
            .OrderBy("f.FamilyName ASC")
            .Take((uint)pageSize)
            .Skip((uint)((page - 1) * pageSize))
            .Build();
        using var connection = _dbConnection.CreateConnection();
        var result = await connection.QueryAsync<Family>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        return result.AsList();
    }

    private static void ValidatePage(int page, int pageSize)
    {
        if (page < 1) throw new ArgumentOutOfRangeException(nameof(page));
        if (pageSize is < 1 or > 500) throw new ArgumentOutOfRangeException(nameof(pageSize));
    }
}