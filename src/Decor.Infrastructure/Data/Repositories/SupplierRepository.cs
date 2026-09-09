using Dapper;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Data;
using Decor.Core.Interfaces.Repositories;
using Decor.FluentSqlBuilder;

namespace Decor.Infrastructure.Data.Repositories;
public class SupplierRepository(IDatabaseConnection dbConnection, Func<FluentCommandBuilder> createCommandBuilder) : ISupplierRepository
{
    private readonly IDatabaseConnection _dbConnection = dbConnection;
    private readonly Func<FluentCommandBuilder> _createCommandBuilder = createCommandBuilder;

    public int Save(Supplier supplier)
    {
        using var conn = _dbConnection.CreateConnection();

        var (sql, parameters) = supplier.SupplierID != 0
            ? _createCommandBuilder().Update().Table<Supplier>().Set(supplier).Where(w => w.Equals((Supplier s) => s.SupplierID, supplier.SupplierID)).Build()
            : _createCommandBuilder().Insert().Into<Supplier>().Values(supplier).Build();

        return conn.Execute(sql, parameters);
    }

    public async Task<int> SaveAsync(Supplier supplier, CancellationToken cancellationToken = default)
    {
        var (sql, parameters) = supplier.SupplierID != 0
            ? _createCommandBuilder().Update().Table<Supplier>().Set(supplier).Where(w => w.Equals((Supplier s) => s.SupplierID, supplier.SupplierID)).Build()
            : _createCommandBuilder().Insert().Into<Supplier>().Values(supplier).Build();

        using var connection = _dbConnection.CreateConnection();
        return await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }

    public int Delete(int supplierId)
    {
        var (sql, parameters) = _createCommandBuilder()
            .Delete<Supplier>()
            .Where(w => w.Equals((Supplier s) => s.SupplierID, supplierId))
            .Build();

        using var conn = _dbConnection.CreateConnection();
        return conn.Execute(sql, parameters);
    }

    public async Task<int> DeleteAsync(int supplierId, CancellationToken cancellationToken = default)
    {
        var (sql, parameters) = _createCommandBuilder()
            .Delete<Supplier>()
            .Where(w => w.Equals((Supplier s) => s.SupplierID, supplierId))
            .Build();

        using var connection = _dbConnection.CreateConnection();
        return await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }

    public IEnumerable<Supplier> SearchGetBy(string? arg = null)
    {
        var (sql, parameters) = _createCommandBuilder()
            .Select(s => s.AllColumns<Supplier>())
            .From<Supplier>()
            .Where(w => w.WithDynamicSearchFilter<Supplier, Supplier>(arg, s => s.SupplierID, s => s.CorporateName))
            .OrderBy("s.CorporateName ASC")
            .Build();

        using var conn = _dbConnection.CreateConnection();
        return conn.Query<Supplier>(sql, parameters);
    }

    public async Task<IReadOnlyList<Supplier>> SearchGetByAsync(string? arg = null, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default)
    {
        ValidatePage(page, pageSize);
        var (sql, parameters) = _createCommandBuilder()
            .Select(s => s.AllColumns<Supplier>())
            .From<Supplier>()
            .Where(w => w.WithDynamicSearchFilter<Supplier, Supplier>(arg, s => s.SupplierID, s => s.CorporateName))
            .OrderBy("s.CorporateName ASC")
            .Take((uint)pageSize)
            .Skip((uint)((page - 1) * pageSize))
            .Build();

        using var connection = _dbConnection.CreateConnection();
        var result = await connection.QueryAsync<Supplier>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        return result.AsList();
    }

    private static void ValidatePage(int page, int pageSize)
    {
        if (page < 1) throw new ArgumentOutOfRangeException(nameof(page));
        if (pageSize is < 1 or > 500) throw new ArgumentOutOfRangeException(nameof(pageSize));
    }
}
