using Dapper;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Data;
using Decor.Core.Interfaces.Repositories;
using Decor.FluentSqlBuilder;

namespace Decor.Infrastructure.Data.Repositories;

public sealed class UnitOfMeasureRepository(IDatabaseConnection dbConnection, Func<FluentCommandBuilder> createCommandBuilder) : IUnitOfMeasureRepository
{
    private readonly IDatabaseConnection _dbConnection = dbConnection;
    private readonly Func<FluentCommandBuilder> _createCommandBuilder = createCommandBuilder;

    public int Save(UnitOfMeasure entity)
    {
        using var connection = _dbConnection.CreateConnection();
        if (entity.UnitOfMeasureID != 0)
        {
            var (sql, parameters) = _createCommandBuilder()
                .Update(u => u.Entity(entity))
                .Where(w => w.Equals<UnitOfMeasure>(u => u.UnitOfMeasureID, entity.UnitOfMeasureID))
                .Build();
            return connection.Execute(sql, parameters);
        }

        var (insertSql, insertParameters) = _createCommandBuilder()
            .Insert(i => i.Entity(entity))
            .ReturningGeneratedId()
            .Build();

        entity.UnitOfMeasureID = connection.QuerySingle<int>(insertSql, insertParameters);
        return 1;
    }

    public async Task<int> SaveAsync(UnitOfMeasure entity, CancellationToken cancellationToken = default)
    {
        using var connection = _dbConnection.CreateConnection();
        if (entity.UnitOfMeasureID != 0)
        {
            var (sql, parameters) = _createCommandBuilder()
                .Update(u => u.Entity(entity))
                .Where(w => w.Equals<UnitOfMeasure>(u => u.UnitOfMeasureID, entity.UnitOfMeasureID))
                .Build();
            return await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        }

        var (insertSql, insertParameters) = _createCommandBuilder()
            .Insert(i => i.Entity(entity))
            .ReturningGeneratedId()
            .Build();

        entity.UnitOfMeasureID = await connection.QuerySingleAsync<int>(new CommandDefinition(insertSql, insertParameters, cancellationToken: cancellationToken));
        return 1;
    }

    public int Delete(int id)
    {
        var (sql, parameters) = _createCommandBuilder()
            .Delete<UnitOfMeasure>()
            .Where(w => w.Equals<UnitOfMeasure>(u => u.UnitOfMeasureID, id))
            .Build();

        using var connection = _dbConnection.CreateConnection();
        return connection.Execute(sql, parameters);
    }

    public async Task<int> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var (sql, parameters) = _createCommandBuilder()
            .Delete<UnitOfMeasure>()
            .Where(w => w.Equals<UnitOfMeasure>(u => u.UnitOfMeasureID, id))
            .Build();

        using var connection = _dbConnection.CreateConnection();
        return await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }

    public IEnumerable<UnitOfMeasure> SearchGetBy(string? arg = null)
    {
        var (sql, parameters) = _createCommandBuilder()
            .Select(s => s.AllColumns<UnitOfMeasure>())
            .From<UnitOfMeasure>()
            .Where(w => w.WithDynamicSearchFilter<UnitOfMeasure, UnitOfMeasure>(arg, u => u.UnitOfMeasureID, u => u.Code))
            .OrderBy("u.Code ASC")
            .Build();

        using var connection = _dbConnection.CreateConnection();
        return connection.Query<UnitOfMeasure>(sql, parameters);
    }

    public async Task<IReadOnlyList<UnitOfMeasure>> SearchGetByAsync(string? arg = null, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default)
    {
        ValidatePage(page, pageSize);
        var (sql, parameters) = _createCommandBuilder()
            .Select(s => s.AllColumns<UnitOfMeasure>())
            .From<UnitOfMeasure>()
            .Where(w => w.WithDynamicSearchFilter<UnitOfMeasure, UnitOfMeasure>(arg, u => u.UnitOfMeasureID, u => u.Code))
            .OrderBy("u.Code ASC")
            .Take((uint)pageSize)
            .Skip((uint)((page - 1) * pageSize))
            .Build();

        using var connection = _dbConnection.CreateConnection();
        var result = await connection.QueryAsync<UnitOfMeasure>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        return result.AsList();
    }

    public async Task<UnitOfMeasure?> GetByIdAsync(int unitOfMeasureId, CancellationToken cancellationToken = default)
    {
        var (sql, parameters) = _createCommandBuilder()
            .Select(s => s.AllColumns<UnitOfMeasure>())
            .From<UnitOfMeasure>()
            .Where(w => w.Equals<UnitOfMeasure>(u => u.UnitOfMeasureID, unitOfMeasureId))
            .Build();

        using var connection = _dbConnection.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<UnitOfMeasure>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }

    public bool CodeExists(string code, int currentUnitOfMeasureId)
    {
        var (sql, parameters) = _createCommandBuilder()
            .Select(s => s.Count())
            .From<UnitOfMeasure>()
            .Where(w =>
            {
                w.Equals<UnitOfMeasure>(u => u.Code, code);
                w.NotEquals<UnitOfMeasure>(u => u.UnitOfMeasureID, currentUnitOfMeasureId);
            })
            .Build();

        using var connection = _dbConnection.CreateConnection();
        return connection.QuerySingle<int>(sql, parameters) > 0;
    }

    public async Task<bool> CodeExistsAsync(string code, int currentUnitOfMeasureId, CancellationToken cancellationToken = default)
    {
        var (sql, parameters) = _createCommandBuilder()
            .Select(s => s.Count())
            .From<UnitOfMeasure>()
            .Where(w =>
            {
                w.Equals<UnitOfMeasure>(u => u.Code, code);
                w.NotEquals<UnitOfMeasure>(u => u.UnitOfMeasureID, currentUnitOfMeasureId);
            })
            .Build();

        using var connection = _dbConnection.CreateConnection();
        return await connection.QuerySingleAsync<int>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken)) > 0;
    }

    public int Deactivate(int unitOfMeasureId)
    {
        var (sql, parameters) = _createCommandBuilder()
            .Update(u => u.Entity<UnitOfMeasure>(uom => uom.IsActive, false))
            .Where(w => w.Equals<UnitOfMeasure>(u => u.UnitOfMeasureID, unitOfMeasureId))
            .Build();

        using var connection = _dbConnection.CreateConnection();
        return connection.Execute(sql, parameters);
    }

    public async Task<int> DeactivateAsync(int unitOfMeasureId, CancellationToken cancellationToken = default)
    {
        var (sql, parameters) = _createCommandBuilder()
            .Update(u => u.Entity<UnitOfMeasure>(uom => uom.IsActive, false))
            .Where(w => w.Equals<UnitOfMeasure>(u => u.UnitOfMeasureID, unitOfMeasureId))
            .Build();

        using var connection = _dbConnection.CreateConnection();
        return await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }

    public int Activate(int unitOfMeasureId)
    {
        var (sql, parameters) = _createCommandBuilder()
            .Update(u => u.Entity<UnitOfMeasure>(uom => uom.IsActive, true))
            .Where(w => w.Equals<UnitOfMeasure>(u => u.UnitOfMeasureID, unitOfMeasureId))
            .Build();

        using var connection = _dbConnection.CreateConnection();
        return connection.Execute(sql, parameters);
    }

    public async Task<int> ActivateAsync(int unitOfMeasureId, CancellationToken cancellationToken = default)
    {
        var (sql, parameters) = _createCommandBuilder()
            .Update(u => u.Entity<UnitOfMeasure>(uom => uom.IsActive, true))
            .Where(w => w.Equals<UnitOfMeasure>(u => u.UnitOfMeasureID, unitOfMeasureId))
            .Build();

        using var connection = _dbConnection.CreateConnection();
        return await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }

    private static void ValidatePage(int page, int pageSize)
    {
        if (page < 1) throw new ArgumentOutOfRangeException(nameof(page));
        if (pageSize is < 1 or > 500) throw new ArgumentOutOfRangeException(nameof(pageSize));
    }
}
