using Dapper;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Data;
using Decor.Core.Interfaces.Repositories;
using Decor.Infrastructure.Data.Utils;
using Decor.FluentSqlBuilder;
using Decor.FluentSqlBuilder.Statements;

namespace Decor.Infrastructure.Data.Repositories;

public class ProductRepository(IDatabaseConnection dbConnection, Func<IQueryContext> queryContextFactory, Func<FluentCommandBuilder> createCommandBuilder) : IProductRepository
{
    private readonly IDatabaseConnection _dbConnection = dbConnection;
    private readonly Func<IQueryContext> _queryContextFactory = queryContextFactory;
    private readonly Func<FluentCommandBuilder> _createCommandBuilder = createCommandBuilder;
    public int Save(Product product)
    {
        using var conn = _dbConnection.CreateConnection();
        int result = default;

        if (product.ProductID > 0)
        {
            var get = conn.GetType();
            var (sql, parameters) = _createCommandBuilder()
                .Update(u => u.Entity(product))
                .Where(w => w.Equals<Product>(p => p.ProductID, product.ProductID))
                .Build();

            result = conn.Execute(sql, parameters);
        }
        else
        {
            var (sql, parameters) = _createCommandBuilder()
                .Insert(i => i.Entity(product))
                .Build();

            result = conn.Execute(sql: sql, param: parameters);
        }
        return result;
    }

    public async Task<int> SaveAsync(Product product, CancellationToken cancellationToken = default)
    {
        var query = _createCommandBuilder();
        var (sql, parameters) = product.ProductID > 0
            ? query
                .Update(u => u.Entity(product))
                .Where(w => w.Equals<Product>(p => p.ProductID, product.ProductID))
                .Build()
            : query
                .Insert(i => i.Entity(product))
                .Build();

        using var connection = _dbConnection.CreateConnection();
        return await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }

    public int Delete(int id)
    {
        throw new NotSupportedException("Produtos não podem ser excluídos. Altere o status IsActive para desativá-los.");
    }

    public Task<int> DeleteAsync(int id, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Produtos não podem ser excluídos. Altere o status IsActive para desativá-los.");

    public IEnumerable<Product> SearchGetBy(string? arg = null)
    {
        var queryContext = _queryContextFactory();

        var (sql, parameters) = _createCommandBuilder()
            .Select(s =>
            {
                s.ExceptColumns<Product>(p => p.BrandID);
                s.WithColumns<Brand>(b => b.BrandID, b => b.BrandName);
                s.WithColumns<Class>(cl => cl.ClassID, cl => cl.ClassName);
                s.WithColumns<Family>(fa => fa.FamilyID, fa => fa.FamilyName);
                s.WithColumns<Group>(gr => gr.GroupID, gr => gr.GroupName);
                s.WithColumns<Subgroup>(sg => sg.SubgroupID, sg => sg.SubgroupName);
                s.WithColumns<UnitOfMeasure>(u => u.UnitOfMeasureID, u => u.Code, u => u.Description);
            })
            .From<Product>()
            .Join(j =>
            {
                j.Inner<Product, Brand>((p, b) => p.BrandID == b.BrandID);
                j.Left<Product, Subgroup>((p, sg) => p.SubgroupID == sg.SubgroupID);
                j.Left<Product, UnitOfMeasure>((p, u) => p.StockUnitID == u.UnitOfMeasureID);
                j.Left<Subgroup, Group>((sg, gr) => sg.GroupID == gr.GroupID);
                j.Left<Group, Family>((gr, fa) => gr.FamilyID == fa.FamilyID);
                j.Left<Family, Class>((fa, cl) => fa.ClassID == cl.ClassID);
            })
            .Where(w =>
            {
                w.Equals<Product>(p => p.IsActive, true);
                var filter = w.WithDynamicFullTextSearch<Product, Product>(arg, p => p.ProductID, p => p.Description);
                queryContext.IsSingleIdSearch = filter.IsIdSearch;
                filter.OrderByRelevanceDescending();
            })
            .Take(10000)
            .Build();

        using var conn = _dbConnection.CreateConnection();
        var result = conn.Query<Product, Brand, Class, Family, Group, Subgroup, UnitOfMeasure, Product>(
            sql,
            map: (product, brand, @class, family, group, subgroup, stockUnit) =>
            {
                if (brand != null) { product.Brand = brand; product.BrandID = brand.BrandID; }
                if (subgroup != null)
                {
                    family.Class = @class;
                    group.Family = family;
                    subgroup.Group = group;
                    product.Subgroup = subgroup;
                    product.SubgroupID = subgroup.SubgroupID;
                }
                if (stockUnit != null)
                {
                    product.StockUnit = stockUnit;
                    product.StockUnitID = stockUnit.UnitOfMeasureID;
                }
                return product;
            },
            param: parameters,
            splitOn: "BrandID,ClassID,FamilyID,GroupID,SubgroupID,UnitOfMeasureID");

        return result.QuerySingleOrMany(queryContext);
    }

    public async Task<IReadOnlyList<Product>> SearchGetByAsync(string? arg = null, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default)
    {
        ValidatePage(page, pageSize);
        var queryContext = _queryContextFactory();
        var (sql, parameters) = _createCommandBuilder()
            .Select(s =>
            {
                s.ExceptColumns<Product>(p => p.BrandID);
                s.WithColumns<Brand>(b => b.BrandID, b => b.BrandName);
                s.WithColumns<Class>(cl => cl.ClassID, cl => cl.ClassName);
                s.WithColumns<Family>(fa => fa.FamilyID, fa => fa.FamilyName);
                s.WithColumns<Group>(gr => gr.GroupID, gr => gr.GroupName);
                s.WithColumns<Subgroup>(sg => sg.SubgroupID, sg => sg.SubgroupName);
                s.WithColumns<UnitOfMeasure>(u => u.UnitOfMeasureID, u => u.Code, u => u.Description);
            })
            .From<Product>()
            .Join(j =>
            {
                j.Inner<Product, Brand>((p, b) => p.BrandID == b.BrandID);
                j.Left<Product, Subgroup>((p, sg) => p.SubgroupID == sg.SubgroupID);
                j.Left<Product, UnitOfMeasure>((p, u) => p.StockUnitID == u.UnitOfMeasureID);
                j.Left<Subgroup, Group>((sg, gr) => sg.GroupID == gr.GroupID);
                j.Left<Group, Family>((gr, fa) => gr.FamilyID == fa.FamilyID);
                j.Left<Family, Class>((fa, cl) => fa.ClassID == cl.ClassID);
            })
            .Where(w =>
            {
                w.Equals<Product>(p => p.IsActive, true);
                var filter = w.WithDynamicFullTextSearch<Product, Product>(arg, p => p.ProductID, p => p.Description);
                queryContext.IsSingleIdSearch = filter.IsIdSearch;
                filter.OrderByRelevanceDescending();
            })
            .Take((uint)pageSize)
            .Skip((uint)((page - 1) * pageSize))
            .Build();

        using var connection = _dbConnection.CreateConnection();
        var result = await connection.QueryAsync<Product, Brand, Class, Family, Group, Subgroup, UnitOfMeasure, Product>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken),
            map: (product, brand, @class, family, group, subgroup, stockUnit) =>
            {
                if (brand != null) { product.Brand = brand; product.BrandID = brand.BrandID; }
                if (subgroup != null)
                {
                    family.Class = @class;
                    group.Family = family;
                    subgroup.Group = group;
                    product.Subgroup = subgroup;
                    product.SubgroupID = subgroup.SubgroupID;
                }
                if (stockUnit != null)
                {
                    product.StockUnit = stockUnit;
                    product.StockUnitID = stockUnit.UnitOfMeasureID;
                }
                return product;
            }, splitOn: "BrandID,ClassID,FamilyID,GroupID,SubgroupID,UnitOfMeasureID");
        var products = result.ToList();
        return queryContext.IsSingleIdSearch ? products.Take(1).ToArray() : products;
    }

    public IEnumerable<Product> SearchGetBy(string? arg, Filters<Product> filters)
    {
        ArgumentNullException.ThrowIfNull(filters);
        var queryContext = _queryContextFactory();
        var (sql, parameters) = BuildSearchQuery(arg, queryContext, filters)
            .Take(10000)
            .Build();

        using var conn = _dbConnection.CreateConnection();
        var result = conn.Query<Product, Brand, Class, Family, Group, Subgroup, UnitOfMeasure, Product>(
            sql,
            map: MapProduct,
            param: parameters,
            splitOn: "BrandID,ClassID,FamilyID,GroupID,SubgroupID,UnitOfMeasureID");

        return result.QuerySingleOrMany(queryContext);
    }

    public async Task<IReadOnlyList<Product>> SearchGetByAsync(
        string? arg,
        Filters<Product> filters,
        int page = 1,
        int pageSize = 100,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filters);
        ValidatePage(page, pageSize);
        var queryContext = _queryContextFactory();
        var (sql, parameters) = BuildSearchQuery(arg, queryContext, filters)
            .Take((uint)pageSize)
            .Skip((uint)((page - 1) * pageSize))
            .Build();

        using var connection = _dbConnection.CreateConnection();
        var result = await connection.QueryAsync<Product, Brand, Class, Family, Group, Subgroup, UnitOfMeasure, Product>(
            new CommandDefinition(sql, parameters, cancellationToken: cancellationToken),
            map: MapProduct,
            splitOn: "BrandID,ClassID,FamilyID,GroupID,SubgroupID,UnitOfMeasureID");
        var products = result.ToList();
        return queryContext.IsSingleIdSearch ? products.Take(1).ToArray() : products;
    }

    private SelectBuilder BuildSearchQuery(string? arg, IQueryContext queryContext, Filters<Product> filters)
    {
        var query = _createCommandBuilder()
            .Select(s =>
            {
                s.ExceptColumns<Product>(p => p.BrandID);
                s.WithColumns<Brand>(b => b.BrandID, b => b.BrandName);
                s.WithColumns<Class>(cl => cl.ClassID, cl => cl.ClassName);
                s.WithColumns<Family>(fa => fa.FamilyID, fa => fa.FamilyName);
                s.WithColumns<Group>(gr => gr.GroupID, gr => gr.GroupName);
                s.WithColumns<Subgroup>(sg => sg.SubgroupID, sg => sg.SubgroupName);
                s.WithColumns<UnitOfMeasure>(u => u.UnitOfMeasureID, u => u.Code, u => u.Description);
            })
            .From<Product>()
            .Join(j =>
            {
                j.Inner<Product, Brand>((p, b) => p.BrandID == b.BrandID);
                j.Left<Product, Subgroup>((p, sg) => p.SubgroupID == sg.SubgroupID);
                j.Left<Product, UnitOfMeasure>((p, u) => p.StockUnitID == u.UnitOfMeasureID);
                j.Left<Subgroup, Group>((sg, gr) => sg.GroupID == gr.GroupID);
                j.Left<Group, Family>((gr, fa) => gr.FamilyID == fa.FamilyID);
                j.Left<Family, Class>((fa, cl) => fa.ClassID == cl.ClassID);
            })
            .Where(w =>
            {
                w.Equals<Product>(p => p.IsActive, true);
                var search = w.WithDynamicFullTextSearch<Product, Product>(arg, p => p.ProductID, p => p.Description);
                queryContext.IsSingleIdSearch = search.IsIdSearch;
                search.OrderByRelevanceDescending();
            });

        query.OrderBy(o => o.Apply(filters));
        return query;
    }

    private static Product MapProduct(Product product, Brand brand, Class @class, Family family, Group group, Subgroup subgroup, UnitOfMeasure stockUnit)
    {
        if (brand != null) { product.Brand = brand; product.BrandID = brand.BrandID; }
        if (subgroup != null)
        {
            family.Class = @class;
            group.Family = family;
            subgroup.Group = group;
            product.Subgroup = subgroup;
            product.SubgroupID = subgroup.SubgroupID;
        }
        if (stockUnit != null)
        {
            product.StockUnit = stockUnit;
            product.StockUnitID = stockUnit.UnitOfMeasureID;
        }
        return product;
    }

    private static void ValidatePage(int page, int pageSize)
    {
        if (page < 1) throw new ArgumentOutOfRangeException(nameof(page));
        if (pageSize < 1) throw new ArgumentOutOfRangeException(nameof(pageSize));
    }

    public bool BrandExists(int marcaID)
    {
        var (sql, parameters) = _createCommandBuilder()
            .Select(s => s.Count())
            .From<Brand>()
            .Where(w => w.Equals<Brand>(b => b.BrandID, marcaID))
            .Build();

        using var conn = _dbConnection.CreateConnection();
        int count = conn.QuerySingle<int>(sql, parameters);
        return count > 0;
    }

    public bool SubgroupExists(int subgroupID)
    {
        var (sql, parameters) = _createCommandBuilder()
            .Select(s => s.Count())
            .From<Subgroup>()
            .Where(w => w.Equals<Subgroup>(sg => sg.SubgroupID, subgroupID))
            .Build();

        using var conn = _dbConnection.CreateConnection();
        int count = conn.QuerySingle<int>(sql, parameters);
        return count > 0;
    }

    public bool UnitOfMeasureExists(int unitOfMeasureId)
    {
        var (sql, parameters) = _createCommandBuilder()
            .Select(s => s.Count())
            .From<UnitOfMeasure>()
            .Where(w => w.Equals<UnitOfMeasure>(u => u.UnitOfMeasureID, unitOfMeasureId))
            .Build();

        using var conn = _dbConnection.CreateConnection();
        int count = conn.QuerySingle<int>(sql, parameters);
        return count > 0;
    }

    public bool ServiceProductExists(int productId)
    {
        var (sql, parameters) = _createCommandBuilder()
            .Select(s => s.Count())
            .From<Product>()
            .Where(w =>
            {
                w.Equals<Product>(p => p.ProductID, productId);
                w.Equals<Product>(p => p.ProductType, ProductType.Service);
            })
            .Build();

        using var conn = _dbConnection.CreateConnection();
        int count = conn.QuerySingle<int>(sql, parameters);
        return count > 0;
    }

    public bool GoodProductExists(int productId)
    {
        var (sql, parameters) = _createCommandBuilder()
            .Select(s => s.Count())
            .From<Product>()
            .Where(w =>
            {
                w.Equals<Product>(p => p.ProductID, productId);
                w.Equals<Product>(p => p.ProductType, ProductType.Good);
            })
            .Build();

        using var conn = _dbConnection.CreateConnection();
        int count = conn.QuerySingle<int>(sql, parameters);
        return count > 0;
    }
}
