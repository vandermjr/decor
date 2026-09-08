using Dapper;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Data;
using Decor.Core.Interfaces.Repositories;
using Decor.FluentSqlBuilder;

namespace Decor.Infrastructure.Data.Repositories;
public class BrandsRepository(IDatabaseConnection dbConnection, Func<FluentCommandBuilder> createCommandBuilder) : IBrandRepository
{
    private readonly IDatabaseConnection _dbConnection = dbConnection;
    private readonly Func<FluentCommandBuilder> _createCommandBuilder = createCommandBuilder;

    public int Save(Brand brand)
    {
        using var conn = _dbConnection.CreateConnection();

        var (sql, parameters) = brand.BrandID != 0
            ? _createCommandBuilder().Update().Table<Brand>().Set(brand).Where(w => w.Equals((Brand b) => b.BrandID, brand.BrandID)).Build()
            : _createCommandBuilder().Insert().Into<Brand>().Values(brand).Build();

        return conn.Execute(sql, parameters);
    }

    public async Task<int> SaveAsync(Brand brand, CancellationToken cancellationToken = default)
    {
        var (sql, parameters) = brand.BrandID != 0
            ? _createCommandBuilder().Update().Table<Brand>().Set(brand).Where(w => w.Equals((Brand b) => b.BrandID, brand.BrandID)).Build()
            : _createCommandBuilder().Insert().Into<Brand>().Values(brand).Build();

        using var connection = _dbConnection.CreateConnection();
        return await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }

    public int Delete(int brandid)
    {
        var (sql, parameters) = _createCommandBuilder()
            .Delete<Brand>()
            .Where(w => w.Equals((Brand b) => b.BrandID, brandid))
            .Build();

        using var conn = _dbConnection.CreateConnection();
        return conn.Execute(sql, parameters);
    }

    public async Task<int> DeleteAsync(int brandId, CancellationToken cancellationToken = default)
    {
        var (sql, parameters) = _createCommandBuilder()
            .Delete<Brand>()
            .Where(w => w.Equals((Brand b) => b.BrandID, brandId))
            .Build();

        using var connection = _dbConnection.CreateConnection();
        return await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }

    public IEnumerable<Brand> SearchGetBy(string? arg = null)
    {
        var (sql, parameters) = _createCommandBuilder()
            .Select(s => s.Columns<Brand>(b => b.BrandID, b => b.BrandName))
            .From<Brand>()
            .Where(w => w.WithDynamicSearchFilter<Brand, Brand>(arg, b => b.BrandID, b => b.BrandName))
            .OrderBy("b.BrandName ASC")
            .Build();

        using var conn = _dbConnection.CreateConnection();
        return conn.Query<Brand>(sql, parameters);
    }

    public async Task<IReadOnlyList<Brand>> SearchGetByAsync(string? arg = null, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default)
    {
        ValidatePage(page, pageSize);
        var (sql, parameters) = _createCommandBuilder()
            .Select(s => s.Columns<Brand>(b => b.BrandID, b => b.BrandName))
            .From<Brand>()
            .Where(w => w.WithDynamicSearchFilter<Brand, Brand>(arg, b => b.BrandID, b => b.BrandName))
            .OrderBy("b.BrandName ASC")
            .Take((uint)pageSize)
            .Skip((uint)((page - 1) * pageSize))
            .Build();

        using var connection = _dbConnection.CreateConnection();
        var result = await connection.QueryAsync<Brand>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        return result.AsList();
    }

    private static void ValidatePage(int page, int pageSize)
    {
        if (page < 1) throw new ArgumentOutOfRangeException(nameof(page));
        if (pageSize is < 1 or > 500) throw new ArgumentOutOfRangeException(nameof(pageSize));
    }

    public bool NameExists(string? brandName, int currentBrandID)
    {
        var (sql, parameters) = _createCommandBuilder()
            .Select(s => s.Count())
            .From<Brand>()
            .Where(w =>
            {
                w.Equals((Brand b) => b.BrandName, brandName);
                w.NotEquals((Brand b) => b.BrandID, currentBrandID);
            })
            .Build();

        using var conn = _dbConnection.CreateConnection();
        int count = conn.QuerySingle<int>(sql, parameters);
        return count > 0;
    }
}
