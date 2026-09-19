using Dapper;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Data;
using Decor.Core.Interfaces.Repositories;
using Decor.FluentSqlBuilder;

namespace Decor.Infrastructure.Data.Repositories;

public class ServiceExecutionRecordRepository(IDatabaseConnection dbConnection, Func<FluentCommandBuilder> createCommandBuilder) : IServiceExecutionRecordRepository
{
    private readonly IDatabaseConnection _dbConnection = dbConnection;
    private readonly Func<FluentCommandBuilder> _createCommandBuilder = createCommandBuilder;

    public int Save(ServiceExecutionRecord entity)
    {
        using var connection = _dbConnection.CreateConnection();
        var (sql, parameters) = entity.ExecutionID == 0
            ? _createCommandBuilder().Insert(i => i.Entity(entity)).ReturningGeneratedId().Build()
            : _createCommandBuilder().Update(u => u.Entity(entity)).Where(w => w.Equals<ServiceExecutionRecord>(r => r.ExecutionID, entity.ExecutionID)).Build();
        if (entity.ExecutionID == 0)
        {
            entity.ExecutionID = connection.QuerySingle<int>(sql, parameters);
            return 1;
        }
        return connection.Execute(sql, parameters);
    }

    public async Task<int> SaveAsync(ServiceExecutionRecord entity, CancellationToken cancellationToken = default)
    {
        using var connection = _dbConnection.CreateConnection();
        var (sql, parameters) = entity.ExecutionID == 0
            ? _createCommandBuilder().Insert(i => i.Entity(entity)).ReturningGeneratedId().Build()
            : _createCommandBuilder().Update(u => u.Entity(entity)).Where(w => w.Equals<ServiceExecutionRecord>(r => r.ExecutionID, entity.ExecutionID)).Build();
        if (entity.ExecutionID == 0)
        {
            entity.ExecutionID = await connection.QuerySingleAsync<int>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
            return 1;
        }
        return await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }

    public int Delete(int id) => throw new NotSupportedException("Registros de execução não podem ser excluídos.");
    public Task<int> DeleteAsync(int id, CancellationToken cancellationToken = default) => throw new NotSupportedException("Registros de execução não podem ser excluídos.");
    public IEnumerable<ServiceExecutionRecord> SearchGetBy(string? arg = null) => throw new NotSupportedException();
    public Task<IReadOnlyList<ServiceExecutionRecord>> SearchGetByAsync(string? arg = null, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public async Task<ServiceExecutionRecord?> GetByIdAsync(int executionId, CancellationToken cancellationToken = default)
    {
        var (sql, parameters) = _createCommandBuilder().Select(s => s.AllColumns<ServiceExecutionRecord>())
            .From<ServiceExecutionRecord>()
            .Where(w => w.Equals<ServiceExecutionRecord>(r => r.ExecutionID, executionId)).Build();
        using var connection = _dbConnection.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<ServiceExecutionRecord>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }

    public async Task<ServiceExecutionRecord?> GetByAppointmentIdAsync(int appointmentId, CancellationToken cancellationToken = default)
    {
        var (sql, parameters) = _createCommandBuilder()
            .Select(s => s.AllColumns<ServiceExecutionRecord>())
            .From<ServiceExecutionRecord>()
            .Where(w => w.Equals<ServiceExecutionRecord>(r => r.AppointmentID, appointmentId))
            .Build();
        using var connection = _dbConnection.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<ServiceExecutionRecord>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }
}
