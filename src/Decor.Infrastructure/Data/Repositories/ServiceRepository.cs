using Dapper;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Data;
using Decor.Core.Interfaces.Repositories;
using Decor.FluentSqlBuilder;
using Decor.FluentSqlBuilder.Statements;

namespace Decor.Infrastructure.Data.Repositories;

public class ServiceRepository(IDatabaseConnection dbConnection, Func<FluentCommandBuilder> createCommandBuilder) : IServiceRepository
{
    public int Save(Service service)
    {
        var (sql, parameters) = BuildSave(service);
        using var connection = dbConnection.CreateConnection();
        return connection.Execute(sql, parameters);
    }

    public async Task<int> SaveAsync(Service service, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var (sql, parameters) = BuildSave(service);
        using var connection = dbConnection.CreateConnection();
        return await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }

    public int Delete(int id)
    {
        var (sql, parameters) = BuildDelete(id);
        using var connection = dbConnection.CreateConnection();
        return connection.Execute(sql, parameters);
    }

    public async Task<int> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var (sql, parameters) = BuildDelete(id);
        using var connection = dbConnection.CreateConnection();
        return await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }

    public IEnumerable<Service> SearchGetBy(string? arg = null)
    {
        var (sql, parameters) = BuildSearch(arg).Build();
        using var connection = dbConnection.CreateConnection();
        return connection.Query<Service>(sql, parameters);
    }

    public async Task<IReadOnlyList<Service>> SearchGetByAsync(string? arg = null, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (page < 1) throw new ArgumentOutOfRangeException(nameof(page));
        if (pageSize is < 1 or > 500) throw new ArgumentOutOfRangeException(nameof(pageSize));
        var offset = checked((uint)((long)(page - 1) * pageSize));
        var (sql, parameters) = BuildSearch(arg).Take((uint)pageSize).Skip(offset).Build();
        using var connection = dbConnection.CreateConnection();
        var result = await connection.QueryAsync<Service>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        return result.AsList();
    }

    public bool ServiceExists(int serviceId)
    {
        if (serviceId <= 0) return false;
        var (sql, parameters) = createCommandBuilder()
            .Select<Service>(selection => selection.Count())
            .Where(where => where.Equals<Service>(service => service.ServiceID, serviceId))
            .Build();
        using var connection = dbConnection.CreateConnection();
        return connection.QuerySingle<int>(sql, parameters) > 0;
    }

    private (string, object) BuildSave(Service service)
    {
        ArgumentNullException.ThrowIfNull(service);
        if (service.ServiceID < 0) throw new ArgumentOutOfRangeException(nameof(service));
        return service.ServiceID == 0
            ? createCommandBuilder().Insert(insert => insert.Entity(service)).Build()
            : createCommandBuilder().Update(update => update.Entity(service))
                .Where(where => where.Equals<Service>(entity => entity.ServiceID, service.ServiceID)).Build();
    }

    private (string, object) BuildDelete(int id)
    {
        if (id <= 0) throw new ArgumentOutOfRangeException(nameof(id));
        return createCommandBuilder().Delete<Service>()
            .Where(where => where.Equals<Service>(service => service.ServiceID, id)).Build();
    }

    private SelectBuilder<Service> BuildSearch(string? arg) => createCommandBuilder()
        .Select<Service>(selection => selection.AllColumns<Service>())
        .Where(where => where.WithDynamicSearchFilter<Service, Service>(arg, service => service.ServiceID, service => service.Description))
        .OrderBy(order => order.Ascending<Service>(service => service.Description).Ascending<Service>(service => service.ServiceID));
}