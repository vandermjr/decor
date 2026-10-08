using Dapper;
using Decor.Core.Common;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Data;
using Decor.Core.Interfaces.Repositories;
using Decor.FluentSqlBuilder;

namespace Decor.Infrastructure.Data.Repositories;

public class ProductRepository(IDatabaseConnection dbConnection, Func<IQueryContext> queryContextFactory, Func<FluentCommandBuilder> createCommandBuilder) : IProductRepository
{
    private readonly IDatabaseConnection _dbConnection = dbConnection;
    private readonly Func<IQueryContext> _queryContextFactory = queryContextFactory;
    private readonly Func<FluentCommandBuilder> _createCommandBuilder = createCommandBuilder;
    public int Save(Product product)
    {
        ValidateProduct(product);
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
        ValidateProduct(product);
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
        var (sql, parameters) = BuildSearchQuery(arg, null, null);
        using var connection = _dbConnection.CreateConnection();
        return connection.Query<Product, Brand, Class, Family, Group, Subgroup, UnitOfMeasure, Product>(
            sql, MapProduct, parameters, splitOn: "BrandID,ClassID,FamilyID,GroupID,SubgroupID,UnitOfMeasureID").ToList();
    }

    public async Task<IReadOnlyList<Product>> SearchGetByAsync(string? arg = null, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default)
    {
        ValidatePage(page, pageSize);
        var (sql, parameters) = BuildSearchQuery(arg, page, pageSize);
        using var connection = _dbConnection.CreateConnection();
        var result = await connection.QueryAsync<Product, Brand, Class, Family, Group, Subgroup, UnitOfMeasure, Product>(
            new CommandDefinition(sql, parameters, cancellationToken: cancellationToken), MapProduct,
            splitOn: "BrandID,ClassID,FamilyID,GroupID,SubgroupID,UnitOfMeasureID");
        return result.AsList();
    }

    private (string Sql, Dictionary<string, object?> Parameters) BuildSearchQuery(string? arg, int? page, int? pageSize)
    {
        var search = ProductSearchQuery.Parse(arg);
        _queryContextFactory().IsSingleIdSearch = false;
        var builder = _createCommandBuilder()
            .Select<Product>(s => s
                .ExceptColumns<Product>(p => p.BrandID)
                .WithColumns<Brand>(b => b.BrandID, b => b.BrandName)
                .WithColumns<Class>(cl => cl.ClassID, cl => cl.ClassName)
                .WithColumns<Family>(fa => fa.FamilyID, fa => fa.FamilyName)
                .WithColumns<Group>(gr => gr.GroupID, gr => gr.GroupName)
                .WithColumns<Subgroup>(sg => sg.SubgroupID, sg => sg.SubgroupName)
                .WithColumns<UnitOfMeasure>(u => u.UnitOfMeasureID, u => u.Code, u => u.Description, u => u.AllowsFraction))
            .Join(j => j
                .Left<Product, Brand>((p, b) => p.BrandID == b.BrandID)
                .Left<Product, Subgroup>((p, sg) => p.SubgroupID == sg.SubgroupID)
                .Left<Product, UnitOfMeasure>((p, u) => p.StockUnitID == u.UnitOfMeasureID)
                .Left<Subgroup, Group>((sg, gr) => sg.GroupID == gr.GroupID)
                .Left<Group, Family>((gr, fa) => gr.FamilyID == fa.FamilyID)
                .Left<Family, Class>((fa, cl) => fa.ClassID == cl.ClassID))
            .Where(w =>
            {
                w.Equals<Product>(p => p.IsActive, true);
                w.Equals<Product>(p => p.ProductType, search.CatalogProductType);
                if (search.Type.HasValue && search.Type != search.CatalogProductType)
                    w.Equals<Product>(p => p.ProductType, search.Type.Value);
                if (search.StockGreaterThan.HasValue)
                    w.GreaterThan<Product>(p => p.StockQuantity, search.StockGreaterThan.Value);
                if (search.StockLessThan.HasValue)
                    w.LessThan<Product>(p => p.StockQuantity, search.StockLessThan.Value);
                if (search.StockEquals.HasValue)
                    w.Equals<Product>(p => p.StockQuantity, search.StockEquals.Value);
                if (search.IsProductIdSearch)
                    w.Equals<Product>(p => p.ProductID, search.ProductID.GetValueOrDefault());
                foreach (var token in search.IsProductIdSearch ? Array.Empty<string>() : search.Tokens)
                {
                    w.Group(group =>
                    {
                        group.Contains<Product>(p => p.Description, token)
                            .Or().Contains<Brand>(b => b.BrandName, token)
                            .Or().Contains<Product>(p => p.Barcode, token)
                            .Or().Contains<Product>(p => p.ManufacturerRef, token);
                        if (int.TryParse(token, out var productId))
                            group.Or().Equals<Product>(p => p.ProductID, productId);
                    });
                }
            })
            .OrderBy(order => order.Ascending<Product>(p => p.ProductID));
        if (page.HasValue && pageSize.HasValue)
            builder.Take((uint)pageSize.Value).Skip((uint)((page.Value - 1) * pageSize.Value));
        return builder.Build();
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
            .Select<Brand>(s => s.Count())
            .Where(w => w.Equals<Brand>(b => b.BrandID, marcaID))
            .Build();

        using var conn = _dbConnection.CreateConnection();
        int count = conn.QuerySingle<int>(sql, parameters);
        return count > 0;
    }

    public bool SubgroupExists(int subgroupID)
    {
        var (sql, parameters) = _createCommandBuilder()
            .Select<Subgroup>(s => s.Count())
            .Where(w => w.Equals<Subgroup>(sg => sg.SubgroupID, subgroupID))
            .Build();

        using var conn = _dbConnection.CreateConnection();
        int count = conn.QuerySingle<int>(sql, parameters);
        return count > 0;
    }

    public bool UnitOfMeasureExists(int unitOfMeasureId)
    {
        var (sql, parameters) = _createCommandBuilder()
            .Select<UnitOfMeasure>(s => s.Count())
            .Where(w => w.Equals<UnitOfMeasure>(u => u.UnitOfMeasureID, unitOfMeasureId))
            .Build();

        using var conn = _dbConnection.CreateConnection();
        int count = conn.QuerySingle<int>(sql, parameters);
        return count > 0;
    }

    public bool ServiceProductExists(int productId)
    {
        using var conn = _dbConnection.CreateConnection();
        return conn.QuerySingle<int>("SELECT COUNT(*) FROM services WHERE ServiceID = @productId", new { productId }) > 0;
    }

    private static void ValidateProduct(Product product)
    {
        if (product.ProductType != ProductType.Good || product.EmployeeCommissionValue is not null)
            throw new System.ComponentModel.DataAnnotations.ValidationException("O catálogo de produtos aceita apenas bens sem comissão de serviço.");
    }

    public bool GoodProductExists(int productId)
    {
        var (sql, parameters) = _createCommandBuilder()
            .Select<Product>(s => s.Count())
            .Where(w => w
                .Equals<Product>(p => p.ProductID, productId)
                .Equals<Product>(p => p.ProductType, ProductType.Good))
            .Build();

        using var conn = _dbConnection.CreateConnection();
        int count = conn.QuerySingle<int>(sql, parameters);
        return count > 0;
    }
}
