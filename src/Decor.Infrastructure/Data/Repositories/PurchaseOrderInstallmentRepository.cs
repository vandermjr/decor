using Dapper;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Data;
using Decor.Core.Interfaces.Repositories;
using Decor.FluentSqlBuilder;

namespace Decor.Infrastructure.Data.Repositories;

public class PurchaseOrderInstallmentRepository(IDatabaseConnection dbConnection, Func<FluentCommandBuilder> createCommandBuilder) : IPurchaseOrderInstallmentRepository
{
    private readonly IDatabaseConnection _dbConnection = dbConnection;
    private readonly Func<FluentCommandBuilder> _createCommandBuilder = createCommandBuilder;

    public int Save(PurchaseOrderInstallment installment)
    {
        using var connection = _dbConnection.CreateConnection();
        if (installment.InstallmentID != 0)
        {
            var (sql, parameters) = _createCommandBuilder().Update(u => u.Entity(installment))
                .Where(w => w.Equals<PurchaseOrderInstallment>(i => i.InstallmentID, installment.InstallmentID)).Build();
            return connection.Execute(sql, parameters);
        }

        var (insertSql, insertParameters) = _createCommandBuilder().Insert(i => i.Entity(installment)).ReturningGeneratedId().Build();
        installment.InstallmentID = connection.QuerySingle<int>(insertSql, insertParameters);
        return 1;
    }

    public async Task<int> SaveAsync(PurchaseOrderInstallment installment, CancellationToken cancellationToken = default)
    {
        using var connection = _dbConnection.CreateConnection();
        if (installment.InstallmentID != 0)
        {
            var (sql, parameters) = _createCommandBuilder().Update(u => u.Entity(installment))
                .Where(w => w.Equals<PurchaseOrderInstallment>(i => i.InstallmentID, installment.InstallmentID)).Build();
            return await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        }

        var (insertSql, insertParameters) = _createCommandBuilder().Insert(i => i.Entity(installment)).ReturningGeneratedId().Build();
        installment.InstallmentID = await connection.QuerySingleAsync<int>(new CommandDefinition(insertSql, insertParameters, cancellationToken: cancellationToken));
        return 1;
    }

    public int Delete(int id)
    {
        var (sql, parameters) = _createCommandBuilder().Delete<PurchaseOrderInstallment>()
            .Where(w => w.Equals<PurchaseOrderInstallment>(i => i.InstallmentID, id)).Build();
        using var connection = _dbConnection.CreateConnection();
        return connection.Execute(sql, parameters);
    }

    public async Task<int> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var (sql, parameters) = _createCommandBuilder().Delete<PurchaseOrderInstallment>()
            .Where(w => w.Equals<PurchaseOrderInstallment>(i => i.InstallmentID, id)).Build();
        using var connection = _dbConnection.CreateConnection();
        return await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }

    public IEnumerable<PurchaseOrderInstallment> SearchGetBy(string? arg = null)
    {
        var (sql, parameters) = _createCommandBuilder().Select(s => s.AllColumns<PurchaseOrderInstallment>())
            .From<PurchaseOrderInstallment>()
            .Where(w => w.WithDynamicSearchFilter<PurchaseOrderInstallment, PurchaseOrderInstallment>(arg, i => i.InstallmentID, i => i.PurchaseOrderID))
            .OrderBy("poi.InstallmentID ASC").Build();
        using var connection = _dbConnection.CreateConnection();
        return connection.Query<PurchaseOrderInstallment>(sql, parameters);
    }

    public async Task<IReadOnlyList<PurchaseOrderInstallment>> SearchGetByAsync(string? arg = null, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default)
    {
        ValidatePage(page, pageSize);
        var (sql, parameters) = _createCommandBuilder().Select(s => s.AllColumns<PurchaseOrderInstallment>())
            .From<PurchaseOrderInstallment>()
            .Where(w => w.WithDynamicSearchFilter<PurchaseOrderInstallment, PurchaseOrderInstallment>(arg, i => i.InstallmentID, i => i.PurchaseOrderID))
            .OrderBy("poi.InstallmentID ASC").Take((uint)pageSize).Skip((uint)((page - 1) * pageSize)).Build();
        using var connection = _dbConnection.CreateConnection();
        var result = await connection.QueryAsync<PurchaseOrderInstallment>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        return result.AsList();
    }

    public async Task<IReadOnlyList<PurchaseOrderInstallment>> GetByPurchaseOrderIdAsync(int purchaseOrderId, CancellationToken cancellationToken = default)
    {
        var (sql, parameters) = _createCommandBuilder().Select(s => s.AllColumns<PurchaseOrderInstallment>())
            .From<PurchaseOrderInstallment>()
            .Where(w => w.Equals<PurchaseOrderInstallment>(i => i.PurchaseOrderID, purchaseOrderId))
            .OrderBy("poi.InstallmentNumber ASC").Build();
        using var connection = _dbConnection.CreateConnection();
        var result = await connection.QueryAsync<PurchaseOrderInstallment>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        return result.AsList();
    }

    public async Task<PurchaseOrderInstallment?> GetByIdAsync(int installmentId, CancellationToken cancellationToken = default)
    {
        var (sql, parameters) = _createCommandBuilder().Select(s => s.AllColumns<PurchaseOrderInstallment>())
            .From<PurchaseOrderInstallment>()
            .Where(w => w.Equals<PurchaseOrderInstallment>(i => i.InstallmentID, installmentId)).Build();
        using var connection = _dbConnection.CreateConnection();
        return await connection.QueryFirstOrDefaultAsync<PurchaseOrderInstallment>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }

    private static void ValidatePage(int page, int pageSize)
    {
        if (page < 1) throw new ArgumentOutOfRangeException(nameof(page));
        if (pageSize is < 1 or > 500) throw new ArgumentOutOfRangeException(nameof(pageSize));
    }
}