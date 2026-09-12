using Dapper;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Data;
using Decor.Core.Interfaces.Repositories;
using Decor.FluentSqlBuilder;

namespace Decor.Infrastructure.Data.Repositories;

public class OrderInstallmentRepository(IDatabaseConnection dbConnection, Func<FluentCommandBuilder> createCommandBuilder) : IOrderInstallmentRepository
{
    private readonly IDatabaseConnection _dbConnection = dbConnection;
    private readonly Func<FluentCommandBuilder> _createCommandBuilder = createCommandBuilder;

    public int Save(OrderInstallment installment)
    {
        using var conn = _dbConnection.CreateConnection();

        var (sql, parameters) = installment.InstallmentID != 0
            ? _createCommandBuilder().Update().Table<OrderInstallment>().Set(installment).Where(w => w.Equals((OrderInstallment i) => i.InstallmentID, installment.InstallmentID)).Build()
            : _createCommandBuilder().Insert().Into<OrderInstallment>().Values(installment).Build();

        return conn.Execute(sql, parameters);
    }

    public async Task<int> SaveAsync(OrderInstallment installment, CancellationToken cancellationToken = default)
    {
        var (sql, parameters) = installment.InstallmentID != 0
            ? _createCommandBuilder().Update().Table<OrderInstallment>().Set(installment).Where(w => w.Equals((OrderInstallment i) => i.InstallmentID, installment.InstallmentID)).Build()
            : _createCommandBuilder().Insert().Into<OrderInstallment>().Values(installment).Build();

        using var connection = _dbConnection.CreateConnection();
        return await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }

    public int Delete(int id)
    {
        var (sql, parameters) = _createCommandBuilder()
            .Delete<OrderInstallment>()
            .Where(w => w.Equals((OrderInstallment i) => i.InstallmentID, id))
            .Build();

        using var conn = _dbConnection.CreateConnection();
        return conn.Execute(sql, parameters);
    }

    public async Task<int> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var (sql, parameters) = _createCommandBuilder()
            .Delete<OrderInstallment>()
            .Where(w => w.Equals((OrderInstallment i) => i.InstallmentID, id))
            .Build();

        using var connection = _dbConnection.CreateConnection();
        return await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }

    public IEnumerable<OrderInstallment> SearchGetBy(string? arg = null)
    {
        var (sql, parameters) = _createCommandBuilder()
            .Select(s => s.AllColumns<OrderInstallment>())
            .From<OrderInstallment>()
            .Where(w => w.WithDynamicSearchFilter<OrderInstallment, OrderInstallment>(arg, i => i.InstallmentID, i => i.OrderID))
            .OrderBy("i.InstallmentID ASC")
            .Build();

        using var conn = _dbConnection.CreateConnection();
        return conn.Query<OrderInstallment>(sql, parameters);
    }

    public async Task<IReadOnlyList<OrderInstallment>> SearchGetByAsync(string? arg = null, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default)
    {
        ValidatePage(page, pageSize);
        var (sql, parameters) = _createCommandBuilder()
            .Select(s => s.AllColumns<OrderInstallment>())
            .From<OrderInstallment>()
            .Where(w => w.WithDynamicSearchFilter<OrderInstallment, OrderInstallment>(arg, i => i.InstallmentID, i => i.OrderID))
            .OrderBy("i.InstallmentID ASC")
            .Take((uint)pageSize)
            .Skip((uint)((page - 1) * pageSize))
            .Build();

        using var connection = _dbConnection.CreateConnection();
        var result = await connection.QueryAsync<OrderInstallment>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        return result.AsList();
    }

    public async Task<IReadOnlyList<OrderInstallment>> GetByOrderIdAsync(int orderId, CancellationToken cancellationToken = default)
    {
        var (sql, parameters) = _createCommandBuilder()
            .Select(s => s.AllColumns<OrderInstallment>())
            .From<OrderInstallment>()
            .Where(w => w.Equals((OrderInstallment i) => i.OrderID, orderId))
            .OrderBy("i.InstallmentNumber ASC")
            .Build();

        using var connection = _dbConnection.CreateConnection();
        var result = await connection.QueryAsync<OrderInstallment>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        return result.AsList();
    }

    public async Task<OrderInstallment?> GetByIdAsync(int installmentId, CancellationToken cancellationToken = default)
    {
        var (sql, parameters) = _createCommandBuilder()
            .Select(s => s.AllColumns<OrderInstallment>())
            .From<OrderInstallment>()
            .Where(w => w.Equals((OrderInstallment i) => i.InstallmentID, installmentId))
            .Build();

        using var connection = _dbConnection.CreateConnection();
        return await connection.QueryFirstOrDefaultAsync<OrderInstallment>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }

    private static void ValidatePage(int page, int pageSize)
    {
        if (page < 1) throw new ArgumentOutOfRangeException(nameof(page));
        if (pageSize is < 1 or > 500) throw new ArgumentOutOfRangeException(nameof(pageSize));
    }
}
