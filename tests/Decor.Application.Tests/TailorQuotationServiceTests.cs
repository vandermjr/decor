using System.ComponentModel.DataAnnotations;
using Decor.Application.Services;
using Decor.Application.Validation;
using Decor.Core.Common;
using Decor.Core.DTOs;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Repositories;
using Decor.Core.Interfaces.Services;
using Decor.Core.Validation;

namespace Decor.Application.Tests;

public class TailorQuotationServiceTests
{
    private readonly InMemoryTailorQuotationRepository _tailorRepo = new();
    private readonly InMemoryQuoteRepository _quoteRepo = new();
    private readonly FixedAuthService _authService = new(
        DecorPermissions.TailorQuotationsView,
        DecorPermissions.TailorQuotationsCreate,
        DecorPermissions.TailorQuotationsRespond,
        DecorPermissions.TailorQuotationsClose,
        DecorPermissions.QuotesSend,
        DecorPermissions.QuotesEdit);

    private readonly InMemoryPartnerPriceTableRepository _partnerPriceRepo = new();
    private readonly InMemoryProductRepository _productRepo = new();
    private readonly InMemoryAttributeRepository _attributeRepo = new();

    private TailorQuotationService CreateTailorService() => new(
        _tailorRepo,
        _quoteRepo,
        _partnerPriceRepo,
        _productRepo,
        _attributeRepo,
        new TailorQuotationRequestDTOValidator(),
        new TailorQuotationRevisionDTOValidator(),
        new TailorQuotationRepositoryValidator(_tailorRepo),
        _authService);

    private QuoteService CreateQuoteService() => new(
        _quoteRepo,
        new QuoteDTOValidator(),
        new DummyQuoteRepoValidator(),
        _authService,
        new DummyAttributeRepo(),
        new DummyProductRepo(),
        _tailorRepo);

    [Fact]
    public async Task CreateRequest_ShouldFail_WhenSectionIsNotCustom()
    {
        var service = CreateTailorService();

        // SectionType = Catalog
        _quoteRepo.SetupItemAndSection(quoteItemId: 10, sectionType: QuoteSectionType.Catalog);

        var dto = new TailorQuotationRequestDTO(0, QuoteItemID: 10, PartnerID: 1, RequestedByEmployeeID: 1, DateTime.UtcNow, null, (int)TailorQuotationRequestStatus.Open);

        var act = () => service.CreateRequestAsync(dto);
        await Assert.ThrowsAsync<ValidationException>(act);
    }

    [Fact]
    public async Task CreateRequest_ShouldFail_WhenExistingRequestIsNotClosed()
    {
        var service = CreateTailorService();
        _quoteRepo.SetupItemAndSection(quoteItemId: 10, sectionType: QuoteSectionType.Custom);

        _tailorRepo.Requests.Add(new TailorQuotationRequest
        {
            RequestID = 1,
            QuoteItemID = 10,
            PartnerID = 1,
            RequestedByEmployeeID = 1,
            RequestedAt = DateTime.UtcNow,
            Status = TailorQuotationRequestStatus.Open
        });

        var dto = new TailorQuotationRequestDTO(0, QuoteItemID: 10, PartnerID: 1, RequestedByEmployeeID: 1, DateTime.UtcNow, null, (int)TailorQuotationRequestStatus.Open);

        var act = () => service.CreateRequestAsync(dto);
        await Assert.ThrowsAsync<ValidationException>(act);
    }

    [Fact]
    public async Task CreateRequest_ShouldSucceed_WhenCustomSectionAndNoOpenRequests()
    {
        var service = CreateTailorService();
        _quoteRepo.SetupItemAndSection(quoteItemId: 10, sectionType: QuoteSectionType.Custom);

        var dto = new TailorQuotationRequestDTO(0, QuoteItemID: 10, PartnerID: 1, RequestedByEmployeeID: 1, DateTime.UtcNow, null, (int)TailorQuotationRequestStatus.Open);

        await service.CreateRequestAsync(dto);

        Assert.Single(_tailorRepo.Requests);
        Assert.Equal(TailorQuotationRequestStatus.Open, _tailorRepo.Requests.First().Status);
    }

    [Fact]
    public async Task AddRevision_ShouldCalculateRevisionNumber_And_UpdateStatusAndUnitPrice()
    {
        var service = CreateTailorService();
        _quoteRepo.SetupItemAndSection(quoteItemId: 10, sectionType: QuoteSectionType.Custom);

        var req = new TailorQuotationRequest
        {
            RequestID = 1,
            QuoteItemID = 10,
            PartnerID = 1,
            RequestedByEmployeeID = 1,
            RequestedAt = DateTime.UtcNow,
            Status = TailorQuotationRequestStatus.Open
        };
        _tailorRepo.Requests.Add(req);

        var revDto = new TailorQuotationRevisionDTO(0, RequestID: 1, RevisionNumber: 0, Price: 150.00m, ChangeReason: "Ajuste inicial", DateTime.UtcNow, RegisteredByEmployeeID: 2);

        await service.AddRevisionAsync(revDto);

        Assert.Single(_tailorRepo.Revisions);
        var rev = _tailorRepo.Revisions.First();
        Assert.Equal(1, rev.RevisionNumber);
        Assert.Equal(150.00m, rev.Price);
        Assert.Equal(TailorQuotationRequestStatus.Answered, req.Status);

        var item = await _quoteRepo.GetItemByIdAsync(10);
        Assert.NotNull(item);
        Assert.Equal(150.00m, item.UnitPrice);
    }

    [Fact]
    public async Task CloseRequest_ShouldFail_WithoutRevisions()
    {
        var service = CreateTailorService();
        var req = new TailorQuotationRequest
        {
            RequestID = 1,
            QuoteItemID = 10,
            PartnerID = 1,
            RequestedByEmployeeID = 1,
            RequestedAt = DateTime.UtcNow,
            Status = TailorQuotationRequestStatus.Open
        };
        _tailorRepo.Requests.Add(req);

        var act = () => service.CloseRequestAsync(1);
        await Assert.ThrowsAsync<ValidationException>(act);
    }

    [Fact]
    public async Task CloseRequest_ShouldSucceed_WithRevisions()
    {
        var service = CreateTailorService();
        var req = new TailorQuotationRequest
        {
            RequestID = 1,
            QuoteItemID = 10,
            PartnerID = 1,
            RequestedByEmployeeID = 1,
            RequestedAt = DateTime.UtcNow,
            Status = TailorQuotationRequestStatus.Answered
        };
        _tailorRepo.Requests.Add(req);
        _tailorRepo.Revisions.Add(new TailorQuotationRevision
        {
            RevisionID = 1,
            RequestID = 1,
            RevisionNumber = 1,
            Price = 100m,
            RespondedAt = DateTime.UtcNow,
            RegisteredByEmployeeID = 2
        });

        await service.CloseRequestAsync(1);

        Assert.Equal(TailorQuotationRequestStatus.Closed, req.Status);
    }

    [Fact]
    public async Task GetSuggestedFabricationPriceAsync_ShouldReturnAreaTimesPrice_WhenWidthAndHeightConfigured()
    {
        var service = CreateTailorService();

        var product = new Product
        {
            ProductID = 42,
            ProductType = ProductType.Good,
            SubgroupID = 7,
            IsActive = true,
            Description = "Tecido"
        };
        product.Subgroup = new Subgroup { SubgroupID = 7, GroupID = 9, SubgroupName = "Voil" };
        _productRepo.Products.Add(product);

        _quoteRepo.Items.Add(new QuoteItem { QuoteItemID = 10, ProductID = 42, Quantity = 1m, UnitPrice = 100m, QuoteSectionID = 100 });
        _quoteRepo.Sections.Add(new QuoteSection { QuoteSectionID = 100, QuoteID = 1, SectionType = QuoteSectionType.Custom });

        _attributeRepo.Attributes.AddRange([
            new ProductSpecificationAttribute { AttributeID = 1, ProductCategoryID = 7, Name = "Largura", DataType = ProductSpecificationDataType.Number, MeasurementRole = MeasurementRole.Width },
            new ProductSpecificationAttribute { AttributeID = 2, ProductCategoryID = 7, Name = "Altura", DataType = ProductSpecificationDataType.Number, MeasurementRole = MeasurementRole.Height }
        ]);

        _quoteRepo.SpecificationValues.AddRange([
            new QuoteItemSpecificationValue { ValueID = 1, QuoteItemID = 10, AttributeID = 1, Value = "2.5" },
            new QuoteItemSpecificationValue { ValueID = 2, QuoteItemID = 10, AttributeID = 2, Value = "3" }
        ]);

        _partnerPriceRepo.Tables.Add(new PartnerPriceTable { PriceTableID = 1, PartnerID = 5, GroupID = 9, PricePerSquareMeter = 40m, IsActive = true });

        var result = await service.GetSuggestedFabricationPriceAsync(10, 5);

        Assert.NotNull(result);
        Assert.Equal(300m, result);
    }

    [Fact]
    public async Task GetSuggestedFabricationPriceAsync_ShouldReturnNull_WhenMissingWidthHeightConfiguration()
    {
        var service = CreateTailorService();

        var product = new Product
        {
            ProductID = 42,
            ProductType = ProductType.Good,
            SubgroupID = 7,
            IsActive = true,
            Description = "Tecido"
        };
        product.Subgroup = new Subgroup { SubgroupID = 7, GroupID = 9, SubgroupName = "Voil" };
        _productRepo.Products.Add(product);

        _quoteRepo.Items.Add(new QuoteItem { QuoteItemID = 10, ProductID = 42, Quantity = 1m, UnitPrice = 100m, QuoteSectionID = 100 });
        _quoteRepo.Sections.Add(new QuoteSection { QuoteSectionID = 100, QuoteID = 1, SectionType = QuoteSectionType.Custom });

        _attributeRepo.Attributes.Add(new ProductSpecificationAttribute { AttributeID = 1, ProductCategoryID = 7, Name = "Largura", DataType = ProductSpecificationDataType.Number, MeasurementRole = MeasurementRole.Width });
        _quoteRepo.SpecificationValues.Add(new QuoteItemSpecificationValue { ValueID = 1, QuoteItemID = 10, AttributeID = 1, Value = "2.5" });

        _partnerPriceRepo.Tables.Add(new PartnerPriceTable { PriceTableID = 1, PartnerID = 5, GroupID = 9, PricePerSquareMeter = 40m, IsActive = true });

        var result = await service.GetSuggestedFabricationPriceAsync(10, 5);

        Assert.Null(result);
    }

    [Fact]
    public async Task QuoteService_TransitionToSent_ShouldFail_IfTailorQuotationNotClosed()
    {
        var quoteService = CreateQuoteService();

        var quote = new Quote { QuoteID = 1, CustomerID = 1, CreatedByEmployeeID = 1, CreatedAt = DateTime.UtcNow };
        var section = new QuoteSection { QuoteSectionID = 100, QuoteID = 1, SectionType = QuoteSectionType.Custom, Status = QuoteSectionStatus.AwaitingQuotation };
        var item = new QuoteItem { QuoteItemID = 10, QuoteSectionID = 100, ProductID = 1, Quantity = 1, UnitPrice = 100m };
        section.Items.Add(item);
        quote.Sections.Add(section);

        _quoteRepo.Quotes.Add(quote);

        // Add a request for this item that is Answered (not Closed)
        _tailorRepo.Requests.Add(new TailorQuotationRequest
        {
            RequestID = 1,
            QuoteItemID = 10,
            PartnerID = 1,
            RequestedByEmployeeID = 1,
            RequestedAt = DateTime.UtcNow,
            Status = TailorQuotationRequestStatus.Answered
        });

        var act = () => quoteService.UpdateSectionStatusAsync(1, 100, QuoteSectionStatus.Sent);
        await Assert.ThrowsAsync<ValidationException>(act);
    }

    [Fact]
    public async Task QuoteService_TransitionToSent_ShouldSucceed_IfAllTailorQuotationsClosed()
    {
        var quoteService = CreateQuoteService();

        var quote = new Quote { QuoteID = 1, CustomerID = 1, CreatedByEmployeeID = 1, CreatedAt = DateTime.UtcNow };
        var section = new QuoteSection { QuoteSectionID = 100, QuoteID = 1, SectionType = QuoteSectionType.Custom, Status = QuoteSectionStatus.AwaitingQuotation };
        var item = new QuoteItem { QuoteItemID = 10, QuoteSectionID = 100, ProductID = 1, Quantity = 1, UnitPrice = 100m };
        section.Items.Add(item);
        quote.Sections.Add(section);

        _quoteRepo.Quotes.Add(quote);

        _tailorRepo.Requests.Add(new TailorQuotationRequest
        {
            RequestID = 1,
            QuoteItemID = 10,
            PartnerID = 1,
            RequestedByEmployeeID = 1,
            RequestedAt = DateTime.UtcNow,
            Status = TailorQuotationRequestStatus.Closed
        });

        await quoteService.UpdateSectionStatusAsync(1, 100, QuoteSectionStatus.Sent);

        Assert.Equal(QuoteSectionStatus.Sent, section.Status);
    }

    private sealed class InMemoryTailorQuotationRepository : ITailorQuotationRepository
    {
        public List<TailorQuotationRequest> Requests { get; } = [];
        public List<TailorQuotationRevision> Revisions { get; } = [];

        public Task<TailorQuotationRequest?> GetByIdAsync(int requestId, CancellationToken cancellationToken = default)
            => Task.FromResult(Requests.FirstOrDefault(r => r.RequestID == requestId));

        public Task<IReadOnlyList<TailorQuotationRequest>> GetByQuoteItemIdAsync(int quoteItemId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<TailorQuotationRequest>>(Requests.Where(r => r.QuoteItemID == quoteItemId).ToList());

        public Task<IReadOnlyList<TailorQuotationRequest>> GetByQuoteSectionIdAsync(int quoteSectionId, CancellationToken cancellationToken = default)
        {
            // Simple mapping assumption for in-memory test
            return Task.FromResult<IReadOnlyList<TailorQuotationRequest>>(Requests.ToList());
        }

        public Task<int> SaveRevisionAsync(TailorQuotationRevision revision, CancellationToken cancellationToken = default)
        {
            if (revision.RevisionID == 0)
                revision.RevisionID = Revisions.Count + 1;
            Revisions.Add(revision);
            return Task.FromResult(1);
        }

        public Task<IReadOnlyList<TailorQuotationRevision>> GetRevisionsByRequestIdAsync(int requestId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<TailorQuotationRevision>>(Revisions.Where(rev => rev.RequestID == requestId).ToList());

        public Task<TailorQuotationRevision?> GetRevisionByIdAsync(int revisionId, CancellationToken cancellationToken = default)
            => Task.FromResult(Revisions.FirstOrDefault(rev => rev.RevisionID == revisionId));

        public int Save(TailorQuotationRequest entity) => 1;
        public int Delete(int id) => 1;
        public IEnumerable<TailorQuotationRequest> SearchGetBy(string? arg = null) => Requests;
        public Task<int> SaveAsync(TailorQuotationRequest entity, CancellationToken cancellationToken = default)
        {
            if (entity.RequestID == 0)
                entity.RequestID = Requests.Count + 1;
            if (!Requests.Contains(entity))
                Requests.Add(entity);
            return Task.FromResult(1);
        }
        public Task<int> DeleteAsync(int id, CancellationToken cancellationToken = default) => Task.FromResult(1);
        public Task<IReadOnlyList<TailorQuotationRequest>> SearchGetByAsync(string? arg = null, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<TailorQuotationRequest>>(Requests.ToList());
    }

    private sealed class InMemoryQuoteRepository : IQuoteRepository
    {
        public List<Quote> Quotes { get; } = [];
        public List<QuoteItem> Items { get; } = [];
        public List<QuoteSection> Sections { get; } = [];
        public List<QuoteItemSpecificationValue> SpecificationValues { get; } = [];

        public void SetupItemAndSection(int quoteItemId, QuoteSectionType sectionType)
        {
            var section = new QuoteSection { QuoteSectionID = 100, SectionType = sectionType };
            var item = new QuoteItem { QuoteItemID = quoteItemId, QuoteSectionID = 100 };
            section.Items.Add(item);
            Sections.Add(section);
            Items.Add(item);
        }

        public Task<Quote?> GetByIdAsync(int quoteId, CancellationToken cancellationToken = default)
            => Task.FromResult(Quotes.FirstOrDefault(q => q.QuoteID == quoteId));

        public Task<Quote?> GetCompleteQuoteAsync(int quoteId, CancellationToken cancellationToken = default)
            => Task.FromResult(Quotes.FirstOrDefault(q => q.QuoteID == quoteId));

        public Task<QuoteSection?> GetSectionByItemIdAsync(int quoteItemId, CancellationToken cancellationToken = default)
        {
            var item = Items.FirstOrDefault(i => i.QuoteItemID == quoteItemId);
            if (item == null) return Task.FromResult<QuoteSection?>(null);
            return Task.FromResult<QuoteSection?>(Sections.FirstOrDefault(s => s.QuoteSectionID == item.QuoteSectionID));
        }

        public Task<QuoteSection?> GetSectionByIdAsync(int quoteSectionId, CancellationToken cancellationToken = default)
            => Task.FromResult(Sections.FirstOrDefault(s => s.QuoteSectionID == quoteSectionId));

        public Task<QuoteItem?> GetItemByIdAsync(int quoteItemId, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.FirstOrDefault(i => i.QuoteItemID == quoteItemId));

        public Task<IEnumerable<QuoteItemSpecificationValue>> GetSpecificationValuesByItemIdAsync(int quoteItemId, CancellationToken cancellationToken = default)
            => Task.FromResult<IEnumerable<QuoteItemSpecificationValue>>(SpecificationValues.Where(v => v.QuoteItemID == quoteItemId).ToList());

        public Task<int> SaveSectionAsync(QuoteSection section, CancellationToken cancellationToken = default) => Task.FromResult(1);
        public Task<int> SaveItemAsync(QuoteItem item, CancellationToken cancellationToken = default) => Task.FromResult(1);
        public Task<int> SaveSpecificationValueAsync(QuoteItemSpecificationValue value, CancellationToken cancellationToken = default) => Task.FromResult(1);

        public int Save(Quote entity) => 1;
        public int Delete(int id) => 1;
        public IEnumerable<Quote> SearchGetBy(string? arg = null) => Quotes;
        public Task<int> SaveAsync(Quote entity, CancellationToken cancellationToken = default) => Task.FromResult(1);
        public Task<int> DeleteAsync(int id, CancellationToken cancellationToken = default) => Task.FromResult(1);
        public Task<IReadOnlyList<Quote>> SearchGetByAsync(string? arg = null, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Quote>>(Quotes.ToList());
    }

    private sealed class FixedAuthService(params string[] permissions) : IAuthorizationService
    {
        private readonly HashSet<string> _permissions = new(permissions);
        public bool HasPermission(string permissionCode) => _permissions.Contains(permissionCode);
        public bool CanView(string resource) => true;
        public bool CanCreate(string resource) => true;
        public bool CanEdit(string resource) => true;
        public bool CanDelete(string resource) => true;
    }

    private sealed class DummyQuoteRepoValidator : IRepositoryValidator<Quote>
    {
        public IEnumerable<string> Validate(Quote entity) => Enumerable.Empty<string>();
    }

    private sealed class InMemoryAttributeRepository : IProductSpecificationAttributeRepository
    {
        public List<ProductSpecificationAttribute> Attributes { get; } = [];

        public int Save(ProductSpecificationAttribute entity) => 1;
        public int Delete(int id) => 1;
        public IEnumerable<ProductSpecificationAttribute> SearchGetBy(string? arg = null) => Attributes;
        public Task<int> SaveAsync(ProductSpecificationAttribute entity, CancellationToken cancellationToken = default) => Task.FromResult(1);
        public Task<int> DeleteAsync(int id, CancellationToken cancellationToken = default) => Task.FromResult(1);
        public Task<IReadOnlyList<ProductSpecificationAttribute>> SearchGetByAsync(string? arg = null, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProductSpecificationAttribute>>(Attributes.Where(a => arg == null || a.AttributeID.ToString() == arg || a.Name!.Contains(arg, StringComparison.OrdinalIgnoreCase)).ToList());
    }

    private sealed class InMemoryProductRepository : IProductRepository
    {
        public List<Product> Products { get; } = [];

        public bool BrandExists(int marcaId) => true;
        public bool SubgroupExists(int subgroupId) => true;
        public bool ServiceProductExists(int productId) => true;
        public bool GoodProductExists(int productId) => true;
        public int Save(Product entity) => 1;
        public int Delete(int id) => 1;
        public IEnumerable<Product> SearchGetBy(string? arg = null) => Products;
        public Task<int> SaveAsync(Product entity, CancellationToken cancellationToken = default) => Task.FromResult(1);
        public Task<int> DeleteAsync(int id, CancellationToken cancellationToken = default) => Task.FromResult(1);
        public Task<IReadOnlyList<Product>> SearchGetByAsync(string? arg = null, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Product>>(Products.Where(p => arg == null || p.ProductID.ToString() == arg).ToList());
    }

    private sealed class InMemoryPartnerPriceTableRepository : IPartnerPriceTableRepository
    {
        public List<PartnerPriceTable> Tables { get; } = [];

        public int Save(PartnerPriceTable entity) => 1;
        public int Delete(int id) => 1;
        public IEnumerable<PartnerPriceTable> SearchGetBy(string? arg = null) => Tables;
        public Task<int> SaveAsync(PartnerPriceTable entity, CancellationToken cancellationToken = default) => Task.FromResult(1);
        public Task<int> DeleteAsync(int id, CancellationToken cancellationToken = default) => Task.FromResult(1);
        public Task<IReadOnlyList<PartnerPriceTable>> SearchGetByAsync(string? arg = null, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<PartnerPriceTable>>(Tables.ToList());
        public Task<PartnerPriceTable?> GetByIdAsync(int priceTableId, CancellationToken cancellationToken = default) => Task.FromResult(Tables.FirstOrDefault(t => t.PriceTableID == priceTableId));
        public Task<PartnerPriceTable?> GetActiveByPartnerAndGroupAsync(int partnerId, int groupId, CancellationToken cancellationToken = default)
            => Task.FromResult(Tables.FirstOrDefault(t => t.PartnerID == partnerId && t.GroupID == groupId && t.IsActive));
        public Task<IEnumerable<PartnerPriceTable>> GetByPartnerIdAsync(int partnerId, CancellationToken cancellationToken = default)
            => Task.FromResult<IEnumerable<PartnerPriceTable>>(Tables.Where(t => t.PartnerID == partnerId).ToList());
        public bool ActiveEntryExists(int partnerId, int groupId, int currentPriceTableId = 0) => Tables.Any(t => t.PartnerID == partnerId && t.GroupID == groupId && t.IsActive && t.PriceTableID != currentPriceTableId);
        public Task<bool> ActiveEntryExistsAsync(int partnerId, int groupId, int currentPriceTableId = 0, CancellationToken cancellationToken = default) => Task.FromResult(ActiveEntryExists(partnerId, groupId, currentPriceTableId));
    }

    private sealed class DummyAttributeRepo : IProductSpecificationAttributeRepository
    {
        public int Save(ProductSpecificationAttribute entity) => 1;
        public int Delete(int id) => 1;
        public IEnumerable<ProductSpecificationAttribute> SearchGetBy(string? arg = null) => [];
        public Task<int> SaveAsync(ProductSpecificationAttribute entity, CancellationToken cancellationToken = default) => Task.FromResult(1);
        public Task<int> DeleteAsync(int id, CancellationToken cancellationToken = default) => Task.FromResult(1);
        public Task<IReadOnlyList<ProductSpecificationAttribute>> SearchGetByAsync(string? arg = null, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ProductSpecificationAttribute>>([]);
    }

    private sealed class DummyProductRepo : IProductRepository
    {
        public bool BrandExists(int marcaId) => true;
        public bool SubgroupExists(int subgroupId) => true;
        public bool ServiceProductExists(int productId) => true;
        public bool GoodProductExists(int productId) => true;
        public int Save(Product entity) => 1;
        public int Delete(int id) => 1;
        public IEnumerable<Product> SearchGetBy(string? arg = null) => [];
        public Task<int> SaveAsync(Product entity, CancellationToken cancellationToken = default) => Task.FromResult(1);
        public Task<int> DeleteAsync(int id, CancellationToken cancellationToken = default) => Task.FromResult(1);
        public Task<IReadOnlyList<Product>> SearchGetByAsync(string? arg = null, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Product>>([]);
    }
}
