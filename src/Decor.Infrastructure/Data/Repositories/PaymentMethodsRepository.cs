using Dapper;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Data;
using Decor.Core.Interfaces.Repositories;
using Decor.FluentSqlBuilder;

namespace Decor.Infrastructure.Data.Repositories;

public class PaymentMethodsRepository(IDatabaseConnection dbConnection, Func<FluentCommandBuilder> createCommandBuilder) : IPaymentMethodRepository
{
    private readonly IDatabaseConnection _dbConnection = dbConnection;
    private readonly Func<FluentCommandBuilder> _createCommandBuilder = createCommandBuilder;

    public int Save(PaymentMethod paymentMethod)
    {
        using var conn = _dbConnection.CreateConnection();
        if (paymentMethod.PaymentMethodID != 0)
        {
            var (sql, parameters) = _createCommandBuilder()
                .Update()
                .Table<PaymentMethod>()
                .Set(paymentMethod)
                .Where(w => w.Equals((PaymentMethod p) => p.PaymentMethodID, paymentMethod.PaymentMethodID))
                .Build();
            return conn.Execute(sql, parameters);
        }
        else
        {
            var (sql, parameters) = _createCommandBuilder()
                .Insert()
                .Into<PaymentMethod>()
                .Values(paymentMethod)
                .ReturningGeneratedId()
                .Build();
            var generatedId = conn.QuerySingle<int>(sql, parameters);
            paymentMethod.PaymentMethodID = generatedId;
            return 1;
        }
    }

    public async Task<int> SaveAsync(PaymentMethod paymentMethod, CancellationToken cancellationToken = default)
    {
        using var connection = _dbConnection.CreateConnection();
        if (paymentMethod.PaymentMethodID != 0)
        {
            var (sql, parameters) = _createCommandBuilder()
                .Update()
                .Table<PaymentMethod>()
                .Set(paymentMethod)
                .Where(w => w.Equals((PaymentMethod p) => p.PaymentMethodID, paymentMethod.PaymentMethodID))
                .Build();
            return await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        }
        else
        {
            var (sql, parameters) = _createCommandBuilder()
                .Insert()
                .Into<PaymentMethod>()
                .Values(paymentMethod)
                .ReturningGeneratedId()
                .Build();
            var generatedId = await connection.QuerySingleAsync<int>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
            paymentMethod.PaymentMethodID = generatedId;
            return 1;
        }
    }

    public int Delete(int id)
    {
        var (sql, parameters) = _createCommandBuilder()
            .Delete<PaymentMethod>()
            .Where(w => w.Equals((PaymentMethod p) => p.PaymentMethodID, id))
            .Build();

        using var conn = _dbConnection.CreateConnection();
        return conn.Execute(sql, parameters);
    }

    public async Task<int> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var (sql, parameters) = _createCommandBuilder()
            .Delete<PaymentMethod>()
            .Where(w => w.Equals((PaymentMethod p) => p.PaymentMethodID, id))
            .Build();

        using var connection = _dbConnection.CreateConnection();
        return await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }

    public IEnumerable<PaymentMethod> SearchGetBy(string? arg = null)
    {
        var (sql, parameters) = _createCommandBuilder()
            .Select(s => s.AllColumns<PaymentMethod>())
            .From<PaymentMethod>()
            .Where(w => w.WithDynamicSearchFilter<PaymentMethod, PaymentMethod>(arg, p => p.PaymentMethodID, p => p.Name))
            .OrderBy("pm.Name ASC")
            .Build();

        using var conn = _dbConnection.CreateConnection();
        return conn.Query<PaymentMethod>(sql, parameters);
    }

    public async Task<IReadOnlyList<PaymentMethod>> SearchGetByAsync(string? arg = null, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default)
    {
        ValidatePage(page, pageSize);
        var (sql, parameters) = _createCommandBuilder()
            .Select(s => s.AllColumns<PaymentMethod>())
            .From<PaymentMethod>()
            .Where(w => w.WithDynamicSearchFilter<PaymentMethod, PaymentMethod>(arg, p => p.PaymentMethodID, p => p.Name))
            .OrderBy("pm.Name ASC")
            .Take((uint)pageSize)
            .Skip((uint)((page - 1) * pageSize))
            .Build();

        using var connection = _dbConnection.CreateConnection();
        var result = await connection.QueryAsync<PaymentMethod>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        return result.AsList();
    }

    public bool IsInUse(int paymentMethodId)
    {
        var (sql, parameters) = _createCommandBuilder()
            .Select(s => s.Count())
            .From<OrderInstallment>()
            .Where(w => w.Equals((OrderInstallment i) => i.PaymentMethodID, paymentMethodId))
            .Build();

        using var conn = _dbConnection.CreateConnection();
        return conn.QuerySingle<int>(sql, parameters) > 0;
    }

    public async Task<bool> IsInUseAsync(int paymentMethodId, CancellationToken cancellationToken = default)
    {
        var (sql, parameters) = _createCommandBuilder()
            .Select(s => s.Count())
            .From<OrderInstallment>()
            .Where(w => w.Equals((OrderInstallment i) => i.PaymentMethodID, paymentMethodId))
            .Build();

        using var connection = _dbConnection.CreateConnection();
        var count = await connection.QuerySingleAsync<int>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        return count > 0;
    }

    public bool NameExists(string name, int currentPaymentMethodId)
    {
        var (sql, parameters) = _createCommandBuilder()
            .Select(s => s.Count())
            .From<PaymentMethod>()
            .Where(w =>
            {
                w.Equals((PaymentMethod p) => p.Name, name);
                w.NotEquals((PaymentMethod p) => p.PaymentMethodID, currentPaymentMethodId);
            })
            .Build();

        using var conn = _dbConnection.CreateConnection();
        return conn.QuerySingle<int>(sql, parameters) > 0;
    }

    public async Task<bool> NameExistsAsync(string name, int currentPaymentMethodId, CancellationToken cancellationToken = default)
    {
        var (sql, parameters) = _createCommandBuilder()
            .Select(s => s.Count())
            .From<PaymentMethod>()
            .Where(w =>
            {
                w.Equals((PaymentMethod p) => p.Name, name);
                w.NotEquals((PaymentMethod p) => p.PaymentMethodID, currentPaymentMethodId);
            })
            .Build();

        using var connection = _dbConnection.CreateConnection();
        var count = await connection.QuerySingleAsync<int>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        return count > 0;
    }

    private static void ValidatePage(int page, int pageSize)
    {
        if (page < 1) throw new ArgumentOutOfRangeException(nameof(page));
        if (pageSize is < 1 or > 500) throw new ArgumentOutOfRangeException(nameof(pageSize));
    }
}
