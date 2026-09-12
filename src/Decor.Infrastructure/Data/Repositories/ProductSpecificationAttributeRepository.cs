using Dapper;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Data;
using Decor.Core.Interfaces.Repositories;
using Decor.FluentSqlBuilder;

namespace Decor.Infrastructure.Data.Repositories;
public class ProductSpecificationAttributeRepository(IDatabaseConnection dbConnection, Func<FluentCommandBuilder> createCommandBuilder) : IProductSpecificationAttributeRepository
{
    private readonly IDatabaseConnection _dbConnection = dbConnection;
    private readonly Func<FluentCommandBuilder> _createCommandBuilder = createCommandBuilder;

    public int Save(ProductSpecificationAttribute attribute)
    {
        using var conn = _dbConnection.CreateConnection();

        var (sql, parameters) = attribute.AttributeID != 0
            ? _createCommandBuilder().Update().Table<ProductSpecificationAttribute>().Set(attribute).Where(w => w.Equals((ProductSpecificationAttribute a) => a.AttributeID, attribute.AttributeID)).Build()
            : _createCommandBuilder().Insert().Into<ProductSpecificationAttribute>().Values(attribute).Build();

        return conn.Execute(sql, parameters);
    }

    public async Task<int> SaveAsync(ProductSpecificationAttribute attribute, CancellationToken cancellationToken = default)
    {
        var (sql, parameters) = attribute.AttributeID != 0
            ? _createCommandBuilder().Update().Table<ProductSpecificationAttribute>().Set(attribute).Where(w => w.Equals((ProductSpecificationAttribute a) => a.AttributeID, attribute.AttributeID)).Build()
            : _createCommandBuilder().Insert().Into<ProductSpecificationAttribute>().Values(attribute).Build();

        using var connection = _dbConnection.CreateConnection();
        return await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }

    public int Delete(int attributeId)
    {
        var (sql, parameters) = _createCommandBuilder()
            .Delete<ProductSpecificationAttribute>()
            .Where(w => w.Equals((ProductSpecificationAttribute a) => a.AttributeID, attributeId))
            .Build();

        using var conn = _dbConnection.CreateConnection();
        return conn.Execute(sql, parameters);
    }

    public async Task<int> DeleteAsync(int attributeId, CancellationToken cancellationToken = default)
    {
        var (sql, parameters) = _createCommandBuilder()
            .Delete<ProductSpecificationAttribute>()
            .Where(w => w.Equals((ProductSpecificationAttribute a) => a.AttributeID, attributeId))
            .Build();

        using var connection = _dbConnection.CreateConnection();
        return await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }

    public IEnumerable<ProductSpecificationAttribute> SearchGetBy(string? arg = null)
    {
        var (sql, parameters) = _createCommandBuilder()
            .Select(s => s.AllColumns<ProductSpecificationAttribute>())
            .From<ProductSpecificationAttribute>()
            .Where(w => w.WithDynamicSearchFilter<ProductSpecificationAttribute, ProductSpecificationAttribute>(arg, a => a.AttributeID, a => a.Name))
            .OrderBy("a.DisplayOrder ASC")
            .Build();

        using var conn = _dbConnection.CreateConnection();
        return conn.Query<ProductSpecificationAttribute>(sql, parameters);
    }

    public async Task<IReadOnlyList<ProductSpecificationAttribute>> SearchGetByAsync(string? arg = null, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default)
    {
        ValidatePage(page, pageSize);
        var (sql, parameters) = _createCommandBuilder()
            .Select(s => s.AllColumns<ProductSpecificationAttribute>())
            .From<ProductSpecificationAttribute>()
            .Where(w => w.WithDynamicSearchFilter<ProductSpecificationAttribute, ProductSpecificationAttribute>(arg, a => a.AttributeID, a => a.Name))
            .OrderBy("a.DisplayOrder ASC")
            .Take((uint)pageSize)
            .Skip((uint)((page - 1) * pageSize))
            .Build();

        using var connection = _dbConnection.CreateConnection();
        var result = await connection.QueryAsync<ProductSpecificationAttribute>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        return result.AsList();
    }

    private static void ValidatePage(int page, int pageSize)
    {
        if (page < 1) throw new ArgumentOutOfRangeException(nameof(page));
        if (pageSize is < 1 or > 500) throw new ArgumentOutOfRangeException(nameof(pageSize));
    }
}
