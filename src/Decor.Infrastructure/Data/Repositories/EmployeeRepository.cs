using Dapper;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Data;
using Decor.Core.Interfaces.Repositories;
using Decor.FluentSqlBuilder;

namespace Decor.Infrastructure.Data.Repositories;
public class EmployeeRepository(IDatabaseConnection dbConnection, Func<FluentCommandBuilder> createCommandBuilder) : IEmployeeRepository
{
    private readonly IDatabaseConnection _dbConnection = dbConnection;
    private readonly Func<FluentCommandBuilder> _createCommandBuilder = createCommandBuilder;

    public int Save(Employee employee)
    {
        using var conn = _dbConnection.CreateConnection();

        var (sql, parameters) = employee.EmployeeID != 0
            ? _createCommandBuilder().Update().Table<Employee>().Set(employee).Where(w => w.Equals((Employee e) => e.EmployeeID, employee.EmployeeID)).Build()
            : _createCommandBuilder().Insert().Into<Employee>().Values(employee).Build();

        return conn.Execute(sql, parameters);
    }

    public async Task<int> SaveAsync(Employee employee, CancellationToken cancellationToken = default)
    {
        var (sql, parameters) = employee.EmployeeID != 0
            ? _createCommandBuilder().Update().Table<Employee>().Set(employee).Where(w => w.Equals((Employee e) => e.EmployeeID, employee.EmployeeID)).Build()
            : _createCommandBuilder().Insert().Into<Employee>().Values(employee).Build();

        using var connection = _dbConnection.CreateConnection();
        return await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }

    public int Delete(int employeeId)
    {
        var (sql, parameters) = _createCommandBuilder()
            .Delete<Employee>()
            .Where(w => w.Equals((Employee e) => e.EmployeeID, employeeId))
            .Build();

        using var conn = _dbConnection.CreateConnection();
        return conn.Execute(sql, parameters);
    }

    public async Task<int> DeleteAsync(int employeeId, CancellationToken cancellationToken = default)
    {
        var (sql, parameters) = _createCommandBuilder()
            .Delete<Employee>()
            .Where(w => w.Equals((Employee e) => e.EmployeeID, employeeId))
            .Build();

        using var connection = _dbConnection.CreateConnection();
        return await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }

    public IEnumerable<Employee> SearchGetBy(string? arg = null)
    {
        var (sql, parameters) = _createCommandBuilder()
            .Select(s => s.AllColumns<Employee>())
            .From<Employee>()
            .Where(w => w.WithDynamicSearchFilter<Employee, Employee>(arg, e => e.EmployeeID, e => e.Name))
            .OrderBy("e.Name ASC")
            .Build();

        using var conn = _dbConnection.CreateConnection();
        return conn.Query<Employee>(sql, parameters);
    }

    public async Task<IReadOnlyList<Employee>> SearchGetByAsync(string? arg = null, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default)
    {
        ValidatePage(page, pageSize);
        var (sql, parameters) = _createCommandBuilder()
            .Select(s => s.AllColumns<Employee>())
            .From<Employee>()
            .Where(w => w.WithDynamicSearchFilter<Employee, Employee>(arg, e => e.EmployeeID, e => e.Name))
            .OrderBy("e.Name ASC")
            .Take((uint)pageSize)
            .Skip((uint)((page - 1) * pageSize))
            .Build();

        using var connection = _dbConnection.CreateConnection();
        var result = await connection.QueryAsync<Employee>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        return result.AsList();
    }

    private static void ValidatePage(int page, int pageSize)
    {
        if (page < 1) throw new ArgumentOutOfRangeException(nameof(page));
        if (pageSize is < 1 or > 500) throw new ArgumentOutOfRangeException(nameof(pageSize));
    }
}
