using Dapper;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Data;
using Decor.Core.Interfaces.Repositories;
using Decor.Infrastructure.Data.Utils;
using Decor.FluentSqlBuilder;

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
                .Update().Table<Product>()
                .Set(product)
                .Where(w => w.Equals((Product p) => p.ProductID, product.ProductID))
                .Build();

            result = conn.Execute(sql, parameters);
        }
        else
        {
            var (sql, parameters) = _createCommandBuilder()
                .Insert().Into<Product>()
                .Values(product)
                .Build();

            result = conn.Execute(sql: sql, param: parameters);
        }
        return result;
    }

    public async Task<int> SaveAsync(Product product, CancellationToken cancellationToken = default)
    {
        var query = _createCommandBuilder();
        var (sql, parameters) = product.ProductID > 0
            ? query.Update().Table<Product>().Set(product).Where(w => w.Equals((Product p) => p.ProductID, product.ProductID)).Build()
            : query.Insert().Into<Product>().Values(product).Build();

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
                s.Except<Product>(p => p.BrandID);
                s.Columns<Brand>(b => b.BrandID, b => b.BrandName);
                s.Columns<Class>(cl => cl.ClassID, cl => cl.ClassName);
                s.Columns<Family>(fa => fa.FamilyID, fa => fa.FamilyName);
                s.Columns<Group>(gr => gr.GroupID, gr => gr.GroupName);
                s.Columns<Subgroup>(sg => sg.SubgroupID, sg => sg.SubgroupName);
            })
            .From<Product>()
            .Join(j =>
            {
                j.Inner<Product, Brand>((p, b) => p.BrandID == b.BrandID);
                j.Inner<Product, Subgroup>((p, sg) => p.SubgroupID == sg.SubgroupID);
                j.Inner<Subgroup, Group>((sg, gr) => sg.GroupID == gr.GroupID);
                j.Inner<Group, Family>((gr, fa) => gr.FamilyID == fa.FamilyID);
                j.Inner<Family, Class>((fa, cl) => fa.ClassID == cl.ClassID);
            })
            .Where(w =>
            {
                w.Equals((Product p) => p.IsActive, true);
                var filter = w.WithDynamicFullTextSearch<Product, Product>(arg, p => p.ProductID, p => p.Description);
                queryContext.IsSingleIdSearch = filter.IsIdSearch;
                filter.OrderByRelevanceDescending();
            })            
            .Take(10000)
            .Build();

        using var conn = _dbConnection.CreateConnection();
        var result = conn.Query<Product, Brand, Class, Family, Group, Subgroup, Product>(
            sql,
            map: (product, brand, @class, family, group, subgroup) =>
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
                return product;
            },
            param: parameters,
            splitOn: "BrandID,ClassID,FamilyID,GroupID,SubgroupID");

        return result.QuerySingleOrMany(queryContext);
    }

    public async Task<IReadOnlyList<Product>> SearchGetByAsync(string? arg = null, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default)
    {
        ValidatePage(page, pageSize);
        var queryContext = _queryContextFactory();
        var (sql, parameters) = _createCommandBuilder()
            .Select(s =>
            {
                s.Except<Product>(p => p.BrandID);
                s.Columns<Brand>(b => b.BrandID, b => b.BrandName);
                s.Columns<Class>(cl => cl.ClassID, cl => cl.ClassName);
                s.Columns<Family>(fa => fa.FamilyID, fa => fa.FamilyName);
                s.Columns<Group>(gr => gr.GroupID, gr => gr.GroupName);
                s.Columns<Subgroup>(sg => sg.SubgroupID, sg => sg.SubgroupName);
            })
            .From<Product>()
            .Join(j =>
            {
                j.Inner<Product, Brand>((p, b) => p.BrandID == b.BrandID);
                j.Inner<Product, Subgroup>((p, sg) => p.SubgroupID == sg.SubgroupID);
                j.Inner<Subgroup, Group>((sg, gr) => sg.GroupID == gr.GroupID);
                j.Inner<Group, Family>((gr, fa) => gr.FamilyID == fa.FamilyID);
                j.Inner<Family, Class>((fa, cl) => fa.ClassID == cl.ClassID);
            })
            .Where(w =>
            {
                w.Equals((Product p) => p.IsActive, true);
                var filter = w.WithDynamicFullTextSearch<Product, Product>(arg, p => p.ProductID, p => p.Description);
                queryContext.IsSingleIdSearch = filter.IsIdSearch;
                filter.OrderByRelevanceDescending();
            })
            .Take((uint)pageSize)
            .Skip((uint)((page - 1) * pageSize))
            .Build();

        using var connection = _dbConnection.CreateConnection();
        var result = await connection.QueryAsync<Product, Brand, Class, Family, Group, Subgroup, Product>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken),
            map: (product, brand, @class, family, group, subgroup) =>
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
                return product;
            }, splitOn: "BrandID,ClassID,FamilyID,GroupID,SubgroupID");
        var products = result.ToList();
        return queryContext.IsSingleIdSearch ? products.Take(1).ToArray() : products;
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
            .Where(w => w.Equals((Brand b) => b.BrandID, marcaID))            
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
            .Where(w => w.Equals((Subgroup sg) => sg.SubgroupID, subgroupID))            
            .Build();

        using var conn = _dbConnection.CreateConnection();
        int count = conn.QuerySingle<int>(sql, parameters);
        return count > 0;
    }
}
