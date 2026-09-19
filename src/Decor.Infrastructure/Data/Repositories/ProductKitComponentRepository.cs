using Dapper;
using Decor.Core.DTOs;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Data;
using Decor.Core.Interfaces.Repositories;
using Decor.FluentSqlBuilder;

namespace Decor.Infrastructure.Data.Repositories;

public class ProductKitComponentRepository(IDatabaseConnection dbConnection, Func<FluentCommandBuilder> createCommandBuilder) : IProductKitComponentRepository
{
    private readonly IDatabaseConnection _dbConnection = dbConnection;
    private readonly Func<FluentCommandBuilder> _createCommandBuilder = createCommandBuilder;

    public int Save(ProductKitComponent component)
    {
        using var connection = _dbConnection.CreateConnection();

        var (sql, parameters) = component.ComponentID != 0
            ? _createCommandBuilder()
                .Update(u => u.Entity(component))
                .Where(w => w.Equals<ProductKitComponent>(c => c.ComponentID, component.ComponentID))
                .Build()
            : _createCommandBuilder()
                .Insert(i => i.Entity(component))
                .Build();

        return connection.Execute(sql, parameters);
    }

    public async Task<int> SaveAsync(ProductKitComponent component, CancellationToken cancellationToken = default)
    {
        var (sql, parameters) = component.ComponentID != 0
            ? _createCommandBuilder()
                .Update(u => u.Entity(component))
                .Where(w => w.Equals<ProductKitComponent>(c => c.ComponentID, component.ComponentID))
                .Build()
            : _createCommandBuilder()
                .Insert(i => i.Entity(component))
                .Build();

        using var connection = _dbConnection.CreateConnection();
        return await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }

    public int Delete(int componentId)
    {
        var (sql, parameters) = _createCommandBuilder()
            .Delete<ProductKitComponent>()
            .Where(w => w.Equals<ProductKitComponent>(c => c.ComponentID, componentId))
            .Build();

        using var connection = _dbConnection.CreateConnection();
        return connection.Execute(sql, parameters);
    }

    public async Task<int> DeleteAsync(int componentId, CancellationToken cancellationToken = default)
    {
        var (sql, parameters) = _createCommandBuilder()
            .Delete<ProductKitComponent>()
            .Where(w => w.Equals<ProductKitComponent>(c => c.ComponentID, componentId))
            .Build();

        using var connection = _dbConnection.CreateConnection();
        return await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }

    public IEnumerable<ProductKitComponent> SearchGetBy(string? arg = null)
    {
        var (sql, parameters) = _createCommandBuilder()
            .Select(s => s.AllColumns<ProductKitComponent>())
            .From<ProductKitComponent>()
            .Where(w => w.WithDynamicSearchFilter<ProductKitComponent, ProductKitComponent>(arg, c => c.ComponentID, c => c.KitProductID))
            .OrderBy("pkc.ComponentID ASC")
            .Build();

        using var connection = _dbConnection.CreateConnection();
        return connection.Query<ProductKitComponent>(sql, parameters);
    }

    public async Task<IReadOnlyList<ProductKitComponent>> SearchGetByAsync(string? arg = null, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default)
    {
        ValidatePage(page, pageSize);
        var (sql, parameters) = _createCommandBuilder()
            .Select(s => s.AllColumns<ProductKitComponent>())
            .From<ProductKitComponent>()
            .Where(w => w.WithDynamicSearchFilter<ProductKitComponent, ProductKitComponent>(arg, c => c.ComponentID, c => c.KitProductID))
            .OrderBy("pkc.ComponentID ASC")
            .Take((uint)pageSize)
            .Skip((uint)((page - 1) * pageSize))
            .Build();

        using var connection = _dbConnection.CreateConnection();
        var result = await connection.QueryAsync<ProductKitComponent>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        return result.AsList();
    }

    public async Task<IEnumerable<ProductKitComponent>> GetByKitProductIdAsync(int kitProductId, CancellationToken cancellationToken = default)
    {
        var (sql, parameters) = _createCommandBuilder()
            .Select(s => s.AllColumns<ProductKitComponent>())
            .From<ProductKitComponent>()
            .Where(w => w.Equals<ProductKitComponent>(c => c.KitProductID, kitProductId))
            .OrderBy("pkc.DisplayOrder ASC")
            .Build();

        using var connection = _dbConnection.CreateConnection();
        var result = await connection.QueryAsync<ProductKitComponent>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        return result.AsList();
    }

    public async Task<IEnumerable<KitComponentPricingDTO>> GetPricingByKitProductIdAsync(int kitProductId, CancellationToken cancellationToken = default)
    {
        var (sql, parameters) = _createCommandBuilder()
            .Select(s =>
            {
                s.WithColumns<ProductKitComponent>(c => c.Quantity);
                s.WithColumns<Product>(p => p.SalePrice);
            })
            .From<ProductKitComponent>()
            .Join(j => j.Inner<ProductKitComponent, Product>((c, p) => c.ComponentProductID == p.ProductID))
            .Where(w => w.Equals<ProductKitComponent>(c => c.KitProductID, kitProductId))
            .Build();

        using var connection = _dbConnection.CreateConnection();
        var result = await connection.QueryAsync<KitComponentPricingDTO>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        return result.AsList();
    }

    public bool RelationExists(int kitProductId, int componentProductId)
    {
        var (sql, parameters) = _createCommandBuilder()
            .Select(s => s.Count())
            .From<ProductKitComponent>()
            .Where(w =>
            {
                w.Equals<ProductKitComponent>(c => c.KitProductID, kitProductId);
                w.Equals<ProductKitComponent>(c => c.ComponentProductID, componentProductId);
            })
            .Build();

        using var connection = _dbConnection.CreateConnection();
        int count = connection.QuerySingle<int>(sql, parameters);
        return count > 0;
    }

    private static void ValidatePage(int page, int pageSize)
    {
        if (page < 1) throw new ArgumentOutOfRangeException(nameof(page));
        if (pageSize is < 1 or > 500) throw new ArgumentOutOfRangeException(nameof(pageSize));
    }
}
