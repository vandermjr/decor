using Decor.Application.Services;
using Decor.Core.Common;
using Decor.Core.DTOs;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Repositories;
using Decor.Core.Interfaces.Services;
using Decor.Core.Validation;

namespace Decor.Application.Tests;

public sealed class CatalogSecurityAndValidationTests
{
    [Fact]
    public async Task ProductService_SaveWithoutPermission_DoesNotPersist()
    {
        var repository = new TrackingProductRepository();
        var service = new ProductService(repository, new ProductDTOValidator(), new ValidProductRepositoryValidator(), new FixedAuthorizationService());

        var action = () => service.SaveProductAsync(CreateProduct());

        await action.Should().ThrowAsync<UnauthorizedAccessException>();
        repository.SaveCalls.Should().Be(0);
    }

    [Fact]
    public async Task ProductService_SaveWhenDatabaseAffectsNoRows_Throws()
    {
        var repository = new TrackingProductRepository { SaveResult = 0 };
        var service = new ProductService(repository, new ProductDTOValidator(), new ValidProductRepositoryValidator(), new FixedAuthorizationService(DecorPermissions.ProductsCreate));

        var action = () => service.SaveProductAsync(CreateProduct());

        await action.Should().ThrowAsync<InvalidOperationException>();
        repository.SaveCalls.Should().Be(1);
    }

    [Fact]
    public void ProductDTOValidator_RequiresForeignKeys()
    {
        var errors = new ProductDTOValidator().Validate(CreateProduct(brandId: null, subgroupId: null)).ToArray();

        errors.Should().Contain(error => error.Contains("Marca"));
        errors.Should().Contain(error => error.Contains("Subgrupo"));
    }

    [Fact]
    public async Task BrandService_DeleteWithoutPermission_DoesNotPersist()
    {
        var repository = new TrackingBrandRepository();
        var service = new BrandService(repository, new BrandDTOValidator(), new ValidBrandRepositoryValidator(), new FixedAuthorizationService());

        var action = () => service.DeleteBrandAsync(1);

        await action.Should().ThrowAsync<UnauthorizedAccessException>();
        repository.DeleteCalls.Should().Be(0);
    }

    private static ProductDTO CreateProduct(int? brandId = 1, int? subgroupId = 1) => new(
        0, "12345678", true, "Produto válido", 1, brandId, "Marca", subgroupId, "Subgrupo", 1, "Grupo", 1, "Família", 1, "Classe", null, null, null, null, 0);

    private sealed class FixedAuthorizationService(params string[] permissions) : IAuthorizationService
    {
        private readonly HashSet<string> _permissions = permissions.ToHashSet(StringComparer.OrdinalIgnoreCase);
        public bool HasPermission(string permissionCode) => _permissions.Contains(permissionCode);
        public bool CanView(string resource) => HasPermission($"{resource}.View");
        public bool CanCreate(string resource) => HasPermission($"{resource}.Create");
        public bool CanEdit(string resource) => HasPermission($"{resource}.Edit");
        public bool CanDelete(string resource) => HasPermission($"{resource}.Delete");
    }

    private sealed class ValidProductRepositoryValidator : IRepositoryValidator<Product>
    {
        public IEnumerable<string> Validate(Product entity) => [];
    }

    private sealed class ValidBrandRepositoryValidator : IRepositoryValidator<Brand>
    {
        public IEnumerable<string> Validate(Brand entity) => [];
    }

    private sealed class TrackingProductRepository : IProductRepository
    {
        public int SaveResult { get; init; } = 1;
        public int SaveCalls { get; private set; }
        public int Save(Product entity) => SaveResult;
        public int Delete(int id) => throw new NotSupportedException();
        public IEnumerable<Product> SearchGetBy(string? arg = null) => [];
        public Task<int> SaveAsync(Product entity, CancellationToken cancellationToken = default) { SaveCalls++; return Task.FromResult(SaveResult); }
        public Task<int> DeleteAsync(int id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Product>> SearchGetByAsync(string? arg = null, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Product>>([]);
        public bool BrandExists(int marcaId) => true;
        public bool SubgroupExists(int subgroupId) => true;
    }

    private sealed class TrackingBrandRepository : IBrandRepository
    {
        public int DeleteCalls { get; private set; }
        public int Save(Brand entity) => 1;
        public int Delete(int id) { DeleteCalls++; return 1; }
        public IEnumerable<Brand> SearchGetBy(string? arg = null) => [];
        public Task<int> SaveAsync(Brand entity, CancellationToken cancellationToken = default) => Task.FromResult(1);
        public Task<int> DeleteAsync(int id, CancellationToken cancellationToken = default) { DeleteCalls++; return Task.FromResult(1); }
        public Task<IReadOnlyList<Brand>> SearchGetByAsync(string? arg = null, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Brand>>([]);
    }
}