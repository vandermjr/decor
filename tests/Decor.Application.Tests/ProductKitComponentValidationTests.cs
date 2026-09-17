using Decor.Application.Services;
using Decor.Application.Validation;
using Decor.Core.Common;
using Decor.Core.DTOs;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Repositories;
using Decor.Core.Interfaces.Services;
using Decor.Core.Validation;
using System.ComponentModel.DataAnnotations;

namespace Decor.Application.Tests;

public sealed class ProductKitComponentValidationTests
{
    [Fact]
    public void ProductKitComponentDTOValidator_RejectsSelfReferenceAndInvalidQuantity()
    {
        var errors = new ProductKitComponentDTOValidator().Validate(CreateDto(kitProductId: 5, componentProductId: 5, quantity: 0)).ToArray();

        errors.Should().Contain(error => error.Contains("si mesmo"));
        errors.Should().Contain(error => error.Contains("Quantidade"));
    }

    [Fact]
    public async Task ProductKitComponentService_KitNotGood_ThrowsValidationException()
    {
        var productRepository = new FixedProductRepository { KitIsGood = false };
        var repoValidator = new ProductKitComponentRepositoryValidator(new FakeKitComponentRepository(), productRepository);
        var repository = new FakeKitComponentRepository();
        var service = new ProductKitComponentService(repository, new ProductKitComponentDTOValidator(), repoValidator, new FixedAuthorizationService(DecorPermissions.ProductKitComponentsCreate));

        var action = () => service.SaveProductKitComponentAsync(CreateDto(kitProductId: 1, componentProductId: 2));

        await action.Should().ThrowAsync<ValidationException>();
        repository.SaveCalls.Should().Be(0);
    }

    [Fact]
    public async Task ProductKitComponentService_ComponentNotGood_ThrowsValidationException()
    {
        var productRepository = new FixedProductRepository { ComponentIsGood = false };
        var repository = new FakeKitComponentRepository();
        var repoValidator = new ProductKitComponentRepositoryValidator(repository, productRepository);
        var service = new ProductKitComponentService(repository, new ProductKitComponentDTOValidator(), repoValidator, new FixedAuthorizationService(DecorPermissions.ProductKitComponentsCreate));

        var action = () => service.SaveProductKitComponentAsync(CreateDto(kitProductId: 1, componentProductId: 2));

        await action.Should().ThrowAsync<ValidationException>();
        repository.SaveCalls.Should().Be(0);
    }

    [Fact]
    public async Task ProductKitComponentService_IndirectCycle_ThrowsValidationException()
    {
        var productRepository = new FixedProductRepository();
        var repository = new FakeKitComponentRepository { RelationExistsResult = true };
        var repoValidator = new ProductKitComponentRepositoryValidator(repository, productRepository);
        var service = new ProductKitComponentService(repository, new ProductKitComponentDTOValidator(), repoValidator, new FixedAuthorizationService(DecorPermissions.ProductKitComponentsCreate));

        var action = () => service.SaveProductKitComponentAsync(CreateDto(kitProductId: 1, componentProductId: 2));

        await action.Should().ThrowAsync<ValidationException>();
        repository.SaveCalls.Should().Be(0);
    }

    [Fact]
    public async Task ProductKitComponentService_ValidComponent_Saves()
    {
        var productRepository = new FixedProductRepository();
        var repository = new FakeKitComponentRepository();
        var repoValidator = new ProductKitComponentRepositoryValidator(repository, productRepository);
        var service = new ProductKitComponentService(repository, new ProductKitComponentDTOValidator(), repoValidator, new FixedAuthorizationService(DecorPermissions.ProductKitComponentsCreate));

        await service.SaveProductKitComponentAsync(CreateDto(kitProductId: 1, componentProductId: 2));

        repository.SaveCalls.Should().Be(1);
    }

    [Fact]
    public async Task GetSuggestedKitPriceAsync_AnyComponentWithoutSalePrice_ReturnsNull()
    {
        var repository = new FakeKitComponentRepository
        {
            PricingItems = [new KitComponentPricingDTO(2m, 100m), new KitComponentPricingDTO(1m, null)]
        };
        var repoValidator = new ProductKitComponentRepositoryValidator(repository, new FixedProductRepository());
        var service = new ProductKitComponentService(repository, new ProductKitComponentDTOValidator(), repoValidator, new FixedAuthorizationService(DecorPermissions.ProductKitComponentsView));

        var result = await service.GetSuggestedKitPriceAsync(1);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetSuggestedKitPriceAsync_AllComponentsPriced_ReturnsSum()
    {
        var repository = new FakeKitComponentRepository
        {
            PricingItems = [new KitComponentPricingDTO(2m, 100m), new KitComponentPricingDTO(1.5m, 50m)]
        };
        var repoValidator = new ProductKitComponentRepositoryValidator(repository, new FixedProductRepository());
        var service = new ProductKitComponentService(repository, new ProductKitComponentDTOValidator(), repoValidator, new FixedAuthorizationService(DecorPermissions.ProductKitComponentsView));

        var result = await service.GetSuggestedKitPriceAsync(1);

        result.Should().Be(2m * 100m + 1.5m * 50m);
    }

    private static ProductKitComponentDTO CreateDto(int kitProductId, int componentProductId, decimal quantity = 1) =>
        new(0, kitProductId, componentProductId, quantity, true, 0);

    private sealed class FixedAuthorizationService(params string[] permissions) : IAuthorizationService
    {
        private readonly HashSet<string> _permissions = permissions.ToHashSet(StringComparer.OrdinalIgnoreCase);
        public bool HasPermission(string permissionCode) => _permissions.Contains(permissionCode);
        public bool CanView(string resource) => HasPermission($"{resource}.View");
        public bool CanCreate(string resource) => HasPermission($"{resource}.Create");
        public bool CanEdit(string resource) => HasPermission($"{resource}.Edit");
        public bool CanDelete(string resource) => HasPermission($"{resource}.Delete");
    }

    private sealed class FixedProductRepository : IProductRepository
    {
        public bool KitIsGood { get; set; } = true;
        public bool ComponentIsGood { get; set; } = true;
        public int Save(Product entity) => 1;
        public int Delete(int id) => throw new NotSupportedException();
        public IEnumerable<Product> SearchGetBy(string? arg = null) => [];
        public Task<int> SaveAsync(Product entity, CancellationToken cancellationToken = default) => Task.FromResult(1);
        public Task<int> DeleteAsync(int id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Product>> SearchGetByAsync(string? arg = null, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Product>>([]);
        public bool BrandExists(int marcaId) => true;
        public bool SubgroupExists(int subgroupId) => true;
        public bool UnitOfMeasureExists(int unitOfMeasureId) => true;
        public bool ServiceProductExists(int productId) => true;
        public bool GoodProductExists(int productId) => productId == 1 ? KitIsGood : ComponentIsGood;
    }

    private sealed class FakeKitComponentRepository : IProductKitComponentRepository
    {
        public int SaveCalls { get; private set; }
        public bool RelationExistsResult { get; set; }
        public IReadOnlyList<KitComponentPricingDTO> PricingItems { get; set; } = [];
        public int Save(ProductKitComponent entity) => 1;
        public int Delete(int id) => throw new NotSupportedException();
        public IEnumerable<ProductKitComponent> SearchGetBy(string? arg = null) => [];
        public Task<int> SaveAsync(ProductKitComponent entity, CancellationToken cancellationToken = default) { SaveCalls++; return Task.FromResult(1); }
        public Task<int> DeleteAsync(int id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<ProductKitComponent>> SearchGetByAsync(string? arg = null, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ProductKitComponent>>([]);
        public Task<IEnumerable<ProductKitComponent>> GetByKitProductIdAsync(int kitProductId, CancellationToken cancellationToken = default) => Task.FromResult<IEnumerable<ProductKitComponent>>([]);
        public Task<IEnumerable<KitComponentPricingDTO>> GetPricingByKitProductIdAsync(int kitProductId, CancellationToken cancellationToken = default) => Task.FromResult<IEnumerable<KitComponentPricingDTO>>(PricingItems);
        public bool RelationExists(int kitProductId, int componentProductId) => RelationExistsResult;
    }
}
