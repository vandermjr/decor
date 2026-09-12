using System.ComponentModel.DataAnnotations;
using Decor.Application.Services;
using Decor.Core.Common;
using Decor.Core.DTOs;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Repositories;
using Decor.Core.Interfaces.Services;
using Decor.Core.Validation;

namespace Decor.Application.Tests;

public sealed class QuoteAggregateValidationTests
{
    [Fact]
    public async Task QuoteService_TransitionToSent_RequiresAllItemsWithUnitPrice()
    {
        var repository = new TrackingQuoteRepository();
        var service = new QuoteService(
            repository,
            new QuoteDTOValidator(),
            new ValidQuoteRepositoryValidator(),
            new FixedAuthorizationService(DecorPermissions.QuotesEdit, DecorPermissions.QuotesSend),
            new FixedProductSpecificationAttributeRepository(),
            new FixedProductRepository());

        var quote = CreateQuote();
        var section = CreateSection(status: QuoteSectionStatus.AwaitingQuotation);
        section.Items.Add(CreateItem(unitPrice: null));
        quote.Sections.Add(section);

        repository.SetQuoteForTest(quote);

        var action = () => service.UpdateSectionStatusAsync(quote.QuoteID, section.QuoteSectionID, QuoteSectionStatus.Sent, CancellationToken.None);

        await action.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public void QuoteItemSpecificationValueValidation_RequiresAttributeCategoryMatchAndTypeCompatibility()
    {
        var attribute = new ProductSpecificationAttribute
        {
            AttributeID = 9,
            ProductCategoryID = 4,
            Name = "Largura",
            DataType = ProductSpecificationDataType.Number,
            EnumOptions = null
        };

        var product = new Product { ProductID = 2, SubgroupID = 4, BrandID = 1 };
        var item = new QuoteItem { QuoteItemID = 7, ProductID = 2, Quantity = 1m, UnitPrice = 10m };

        var validationErrors = QuoteService.ValidateQuoteItemSpecificationValue(item, attribute, product, "ABC").ToArray();

        validationErrors.Should().ContainSingle();
        validationErrors[0].Should().Contain("numérico");
    }

    private static Quote CreateQuote() => new()
    {
        QuoteID = 1,
        CustomerID = 10,
        CreatedByEmployeeID = 5,
        SourceType = QuoteSourceType.DirectCapture,
        CreatedAt = DateTime.UtcNow,
        Sections = []
    };

    private static QuoteSection CreateSection(QuoteSectionStatus status) => new()
    {
        QuoteSectionID = 1,
        QuoteID = 1,
        SectionType = QuoteSectionType.Catalog,
        Status = status,
        CreatedAt = DateTime.UtcNow,
        Items = []
    };

    private static QuoteItem CreateItem(decimal? unitPrice) => new()
    {
        QuoteItemID = 1,
        QuoteSectionID = 1,
        ProductID = 2,
        Quantity = 1m,
        UnitPrice = unitPrice,
        HasInstallationService = false,
        SpecificationValues = []
    };

    private sealed class FixedAuthorizationService(params string[] permissions) : IAuthorizationService
    {
        private readonly HashSet<string> _permissions = permissions.ToHashSet(StringComparer.OrdinalIgnoreCase);
        public bool HasPermission(string permissionCode) => _permissions.Contains(permissionCode);
        public bool CanView(string resource) => HasPermission($"{resource}.View");
        public bool CanCreate(string resource) => HasPermission($"{resource}.Create");
        public bool CanEdit(string resource) => HasPermission($"{resource}.Edit");
        public bool CanDelete(string resource) => HasPermission($"{resource}.Delete");
    }

    private sealed class ValidQuoteRepositoryValidator : IRepositoryValidator<Quote>
    {
        public IEnumerable<string> Validate(Quote entity) => [];
    }

    private sealed class TrackingQuoteRepository : IQuoteRepository
    {
        private Quote? _quote;

        public void SetQuoteForTest(Quote quote) => _quote = quote;

        public int Save(Quote entity) => 1;
        public int Delete(int id) => 1;
        public IEnumerable<Quote> SearchGetBy(string? arg = null) => [];
        public Task<int> SaveAsync(Quote entity, CancellationToken cancellationToken = default) => Task.FromResult(1);
        public Task<int> DeleteAsync(int id, CancellationToken cancellationToken = default) => Task.FromResult(1);
        public Task<IReadOnlyList<Quote>> SearchGetByAsync(string? arg = null, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Quote>>([]);
        public Task<Quote?> GetByIdAsync(int quoteId, CancellationToken cancellationToken = default) => Task.FromResult<Quote?>(_quote);
        public Task<Quote?> GetCompleteQuoteAsync(int quoteId, CancellationToken cancellationToken = default) => Task.FromResult<Quote?>(_quote);
        public Task<QuoteSection?> GetSectionByItemIdAsync(int quoteItemId, CancellationToken cancellationToken = default) => Task.FromResult(_quote?.Sections.FirstOrDefault(s => s.Items.Any(i => i.QuoteItemID == quoteItemId)));
        public Task<QuoteSection?> GetSectionByIdAsync(int quoteSectionId, CancellationToken cancellationToken = default) => Task.FromResult(_quote?.Sections.FirstOrDefault(s => s.QuoteSectionID == quoteSectionId));
        public Task<QuoteItem?> GetItemByIdAsync(int quoteItemId, CancellationToken cancellationToken = default) => Task.FromResult(_quote?.Sections.SelectMany(s => s.Items).FirstOrDefault(i => i.QuoteItemID == quoteItemId));
        public Task<int> SaveSectionAsync(QuoteSection section, CancellationToken cancellationToken = default) => Task.FromResult(1);
        public Task<int> SaveItemAsync(QuoteItem item, CancellationToken cancellationToken = default) => Task.FromResult(1);
        public Task<int> SaveSpecificationValueAsync(QuoteItemSpecificationValue value, CancellationToken cancellationToken = default) => Task.FromResult(1);
    }

    private sealed class FixedProductSpecificationAttributeRepository : IProductSpecificationAttributeRepository
    {
        public int Save(ProductSpecificationAttribute entity) => 1;
        public int Delete(int id) => 1;
        public IEnumerable<ProductSpecificationAttribute> SearchGetBy(string? arg = null) => [];
        public Task<int> SaveAsync(ProductSpecificationAttribute entity, CancellationToken cancellationToken = default) => Task.FromResult(1);
        public Task<int> DeleteAsync(int id, CancellationToken cancellationToken = default) => Task.FromResult(1);
        public Task<IReadOnlyList<ProductSpecificationAttribute>> SearchGetByAsync(string? arg = null, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ProductSpecificationAttribute>>([]);
    }

    private sealed class FixedProductRepository : IProductRepository
    {
        public int Save(Product entity) => 1;
        public int Delete(int id) => 1;
        public IEnumerable<Product> SearchGetBy(string? arg = null) => [];
        public Task<int> SaveAsync(Product entity, CancellationToken cancellationToken = default) => Task.FromResult(1);
        public Task<int> DeleteAsync(int id, CancellationToken cancellationToken = default) => Task.FromResult(1);
        public Task<IReadOnlyList<Product>> SearchGetByAsync(string? arg = null, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Product>>([]);
        public bool BrandExists(int marcaID) => true;
        public bool SubgroupExists(int subgroupID) => true;
    }
}
