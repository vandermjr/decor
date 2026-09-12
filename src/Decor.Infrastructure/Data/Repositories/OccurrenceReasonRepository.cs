using Dapper;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Data;
using Decor.Core.Interfaces.Repositories;
using Decor.FluentSqlBuilder;

namespace Decor.Infrastructure.Data.Repositories;

public class OccurrenceReasonRepository(IDatabaseConnection dbConnection, Func<FluentCommandBuilder> createCommandBuilder) : IOccurrenceReasonRepository
{
    public int Save(OccurrenceReason entity)
    {
        using var connection = dbConnection.CreateConnection();
        if (entity.ReasonID != 0)
        {
            var (sql, parameters) = BuildUpdate(entity);
            return connection.Execute(sql, parameters);
        }

        var (insertSql, insertParameters) = BuildInsert(entity);
        entity.ReasonID = connection.QuerySingle<int>(insertSql, insertParameters);
        return 1;
    }

    public async Task<int> SaveAsync(OccurrenceReason entity, CancellationToken cancellationToken = default)
    {
        using var connection = dbConnection.CreateConnection();
        if (entity.ReasonID != 0)
        {
            var (sql, parameters) = BuildUpdate(entity);
            return await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        }

        var (insertSql, insertParameters) = BuildInsert(entity);
        entity.ReasonID = await connection.QuerySingleAsync<int>(new CommandDefinition(insertSql, insertParameters, cancellationToken: cancellationToken));
        return 1;
    }

    public int Delete(int id)
    {
        var (sql, parameters) = BuildDelete(id);
        using var connection = dbConnection.CreateConnection();
        return connection.Execute(sql, parameters);
    }

    public async Task<int> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var (sql, parameters) = BuildDelete(id);
        using var connection = dbConnection.CreateConnection();
        return await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }

    public IEnumerable<OccurrenceReason> SearchGetBy(string? arg = null)
    {
        var (sql, parameters) = BuildSearch(arg, null, null);
        using var connection = dbConnection.CreateConnection();
        return connection.Query<OccurrenceReason>(sql, parameters);
    }

    public async Task<IReadOnlyList<OccurrenceReason>> SearchGetByAsync(string? arg = null, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default)
    {
        ValidatePage(page, pageSize);
        var (sql, parameters) = BuildSearch(arg, (uint)pageSize, (uint)((page - 1) * pageSize));
        using var connection = dbConnection.CreateConnection();
        return (await connection.QueryAsync<OccurrenceReason>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken))).AsList();
    }

    public async Task<OccurrenceReason?> GetByIdAsync(int reasonId, CancellationToken cancellationToken = default)
    {
        var (sql, parameters) = createCommandBuilder()
            .Select(s => s.AllColumns<OccurrenceReason>())
            .From<OccurrenceReason>()
            .Where(w => w.Equals((OccurrenceReason r) => r.ReasonID, reasonId))
            .Build();
        using var connection = dbConnection.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<OccurrenceReason>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }

    public bool DescriptionExists(string description, int currentReasonId)
    {
        var (sql, parameters) = BuildDescriptionExists(description, currentReasonId);
        using var connection = dbConnection.CreateConnection();
        return connection.QuerySingle<int>(sql, parameters) > 0;
    }

    public async Task<bool> IsInUseAsync(int reasonId, CancellationToken cancellationToken = default)
    {
        var (sql, parameters) = createCommandBuilder()
            .Select(s => s.Count())
            .From<OrderOccurrence>()
            .Where(w => w.Equals((OrderOccurrence o) => o.ReasonID, reasonId))
            .Build();
        using var connection = dbConnection.CreateConnection();
        return await connection.QuerySingleAsync<int>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken)) > 0;
    }

    private (string Sql, object Parameters) BuildInsert(OccurrenceReason entity)
        => createCommandBuilder().Insert().Into<OccurrenceReason>().Values(entity).ReturningGeneratedId().Build();

    private (string Sql, object Parameters) BuildUpdate(OccurrenceReason entity)
        => createCommandBuilder().Update().Table<OccurrenceReason>().Set(entity).Where(w => w.Equals((OccurrenceReason r) => r.ReasonID, entity.ReasonID)).Build();

    private (string Sql, object Parameters) BuildDelete(int id)
        => createCommandBuilder().Delete<OccurrenceReason>().Where(w => w.Equals((OccurrenceReason r) => r.ReasonID, id)).Build();

    private (string Sql, object Parameters) BuildDescriptionExists(string description, int currentReasonId)
        => createCommandBuilder().Select(s => s.Count()).From<OccurrenceReason>().Where(w =>
        {
            w.Equals((OccurrenceReason r) => r.Description, description);
            w.NotEquals((OccurrenceReason r) => r.ReasonID, currentReasonId);
        }).Build();

    private (string Sql, object Parameters) BuildSearch(string? arg, uint? take, uint? skip)
    {
        var builder = createCommandBuilder()
            .Select(s => s.AllColumns<OccurrenceReason>())
            .From<OccurrenceReason>()
            .Where(w => w.WithDynamicSearchFilter<OccurrenceReason, OccurrenceReason>(arg, r => r.ReasonID, r => r.Description))
            .OrderBy("or1.Description ASC");
        if (take.HasValue) builder.Take(take.Value);
        if (skip.HasValue) builder.Skip(skip.Value);
        return builder.Build();
    }

    private static void ValidatePage(int page, int pageSize)
    {
        if (page < 1) throw new ArgumentOutOfRangeException(nameof(page));
        if (pageSize is < 1 or > 500) throw new ArgumentOutOfRangeException(nameof(pageSize));
    }
}