using Dapper;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Data;
using Decor.Core.Interfaces.Repositories;
using Decor.FluentSqlBuilder;

namespace Decor.Infrastructure.Data.Repositories;
public class PartnerRepository(IDatabaseConnection dbConnection, Func<FluentCommandBuilder> createCommandBuilder) : IPartnerRepository
{
    private readonly IDatabaseConnection _dbConnection = dbConnection;
    private readonly Func<FluentCommandBuilder> _createCommandBuilder = createCommandBuilder;

    public int Save(Partner partner)
    {
        using var conn = _dbConnection.CreateConnection();

        var (sql, parameters) = partner.PartnerID != 0
            ? _createCommandBuilder().Update().Table<Partner>().Set(partner).Where(w => w.Equals((Partner p) => p.PartnerID, partner.PartnerID)).Build()
            : _createCommandBuilder().Insert().Into<Partner>().Values(partner).Build();

        return conn.Execute(sql, parameters);
    }

    public async Task<int> SaveAsync(Partner partner, CancellationToken cancellationToken = default)
    {
        var (sql, parameters) = partner.PartnerID != 0
            ? _createCommandBuilder().Update().Table<Partner>().Set(partner).Where(w => w.Equals((Partner p) => p.PartnerID, partner.PartnerID)).Build()
            : _createCommandBuilder().Insert().Into<Partner>().Values(partner).Build();

        using var connection = _dbConnection.CreateConnection();
        return await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }

    public int Delete(int partnerId)
    {
        var (sql, parameters) = _createCommandBuilder()
            .Delete<Partner>()
            .Where(w => w.Equals((Partner p) => p.PartnerID, partnerId))
            .Build();

        using var conn = _dbConnection.CreateConnection();
        return conn.Execute(sql, parameters);
    }

    public async Task<int> DeleteAsync(int partnerId, CancellationToken cancellationToken = default)
    {
        var (sql, parameters) = _createCommandBuilder()
            .Delete<Partner>()
            .Where(w => w.Equals((Partner p) => p.PartnerID, partnerId))
            .Build();

        using var connection = _dbConnection.CreateConnection();
        return await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }

    public IEnumerable<Partner> SearchGetBy(string? arg = null)
    {
        var (sql, parameters) = _createCommandBuilder()
            .Select(s => s.AllColumns<Partner>())
            .From<Partner>()
            .Where(w => w.WithDynamicSearchFilter<Partner, Partner>(arg, p => p.PartnerID, p => p.Name))
            .OrderBy("p.Name ASC")
            .Build();

        using var conn = _dbConnection.CreateConnection();
        return conn.Query<Partner>(sql, parameters);
    }

    public async Task<IReadOnlyList<Partner>> SearchGetByAsync(string? arg = null, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default)
    {
        ValidatePage(page, pageSize);
        var (sql, parameters) = _createCommandBuilder()
            .Select(s => s.AllColumns<Partner>())
            .From<Partner>()
            .Where(w => w.WithDynamicSearchFilter<Partner, Partner>(arg, p => p.PartnerID, p => p.Name))
            .OrderBy("p.Name ASC")
            .Take((uint)pageSize)
            .Skip((uint)((page - 1) * pageSize))
            .Build();

        using var connection = _dbConnection.CreateConnection();
        var result = await connection.QueryAsync<Partner>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        return result.AsList();
    }

    private static void ValidatePage(int page, int pageSize)
    {
        if (page < 1) throw new ArgumentOutOfRangeException(nameof(page));
        if (pageSize is < 1 or > 500) throw new ArgumentOutOfRangeException(nameof(pageSize));
    }
}
