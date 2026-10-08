using System.ComponentModel.DataAnnotations;
using Decor.Application.Mappers;
using Decor.Application.Services;
using Decor.Application.Validation;
using Decor.Core.Common;
using Decor.Core.DTOs;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Repositories;
using Decor.Core.Interfaces.Services;
using Decor.Core.Validation;
using Moq;

namespace Decor.Application.Tests;

public sealed class QuoteAggregateValidationTests
{
    [Theory]
    [InlineData(DecorPermissions.QuotesView)]
    [InlineData(DecorPermissions.QuotesCreate)]
    [InlineData(DecorPermissions.QuotesEdit)]
    public async Task GetQuantityUnitsAsync_AllowsQuotePermissionWithoutUnitsView(string permission)
    {
        var authorization = new FixedAuthorizationService(permission);
        Assert.False(authorization.HasPermission(DecorPermissions.UnitsOfMeasureView));
        var units = new Mock<IUnitOfMeasureRepository>(MockBehavior.Strict);
        using var cancellation = new CancellationTokenSource();
        var unit = new UnitOfMeasure { UnitOfMeasureID = 9, AllowsFraction = true };
        units.Setup(repository => repository.SearchGetByAsync(null, 2, 25, cancellation.Token))
            .ReturnsAsync(new[] { unit });
        var service = new QuoteService(new TrackingQuoteRepository(), new QuoteDTOValidator(),
            new ValidQuoteRepositoryValidator(), authorization,
            new FixedProductSpecificationAttributeRepository(), new FixedProductRepository(), unitOfMeasureRepository: units.Object);

        var result = Assert.Single(await service.GetQuantityUnitsAsync(2, 25, cancellation.Token));

        Assert.Equal(unit.ToDTO(), result);
        units.Verify(repository => repository.SearchGetByAsync(null, 2, 25, cancellation.Token), Times.Once);
        units.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(DecorPermissions.UnitsOfMeasureView)]
    [InlineData(DecorPermissions.QuotesApprove)]
    [InlineData("")]
    public async Task GetQuantityUnitsAsync_RequiresQuotePermissionBeforeRepositoryAccess(string permission)
    {
        var units = new Mock<IUnitOfMeasureRepository>(MockBehavior.Strict);
        var service = new QuoteService(new TrackingQuoteRepository(), new QuoteDTOValidator(),
            new ValidQuoteRepositoryValidator(), new FixedAuthorizationService(permission),
            new FixedProductSpecificationAttributeRepository(), new FixedProductRepository(), unitOfMeasureRepository: units.Object);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetQuantityUnitsAsync());

        units.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetQuantityUnitsAsync_WithoutRepositoryReturnsEmptyForAuthorizedQuoteUser()
    {
        Assert.Empty(await CreateService(new TrackingQuoteRepository(), DecorPermissions.QuotesCreate).GetQuantityUnitsAsync());
    }

    [Fact]
    public async Task GetQuantityUnitsAsync_HonorsCancellationBeforeRepositoryAccess()
    {
        var units = new Mock<IUnitOfMeasureRepository>(MockBehavior.Strict);
        var service = new QuoteService(new TrackingQuoteRepository(), new QuoteDTOValidator(),
            new ValidQuoteRepositoryValidator(), new FixedAuthorizationService(DecorPermissions.QuotesCreate),
            new FixedProductSpecificationAttributeRepository(), new FixedProductRepository(), unitOfMeasureRepository: units.Object);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.GetQuantityUnitsAsync(cancellationToken: cancellation.Token));

        units.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(QuoteSourceType.Own)]
    [InlineData(QuoteSourceType.Store)]
    [InlineData(QuoteSourceType.Other)]
    public void QuoteRepositoryValidator_AllowsSourceWithoutPartner(QuoteSourceType source)
    {
        var partners = new Mock<IPartnerRepository>(MockBehavior.Strict);
        var validator = new QuoteRepositoryValidator(Mock.Of<IQuoteRepository>(), partners.Object);
        var quote = CreateQuote();
        quote.SourceType = source;
        quote.SourcePartnerID = null;
        Assert.Empty(validator.Validate(quote));
        partners.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(QuoteSourceType.Own, 7, true)]
    [InlineData(QuoteSourceType.Store, 7, true)]
    [InlineData(QuoteSourceType.Other, 7, true)]
    [InlineData(QuoteSourceType.Store, 8, false)]
    [InlineData(QuoteSourceType.Own, 8, false)]
    [InlineData(QuoteSourceType.Other, 8, false)]
    [InlineData(QuoteSourceType.Store, 0, false)]
    [InlineData(QuoteSourceType.Store, -1, false)]
    public void QuoteRepositoryValidator_ValidatesSuppliedPartner(QuoteSourceType source, int partnerId, bool valid)
    {
        var partners = new Mock<IPartnerRepository>();
        partners.Setup(repository => repository.SearchGetBy(It.IsAny<string>()))
            .Returns(new[] { new Partner { PartnerID = 7 } });
        var quote = CreateQuote();
        quote.SourceType = source;
        quote.SourcePartnerID = partnerId;
        Assert.Equal(valid, !new QuoteRepositoryValidator(Mock.Of<IQuoteRepository>(), partners.Object).Validate(quote).Any());
    }

    [Fact]
    public void QuoteRepositoryValidator_PreservesRequiredParties()
    {
        var quote = CreateQuote();
        quote.SourceType = QuoteSourceType.Store;
        quote.SourcePartnerID = null;
        quote.CustomerID = null;
        quote.CreatedByEmployeeID = null;
        Assert.Equal(2, new QuoteRepositoryValidator(Mock.Of<IQuoteRepository>(), Mock.Of<IPartnerRepository>()).Validate(quote).Count());
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(3, true)]
    [InlineData(4, false)]
    public void QuoteDTOValidator_ValidatesSourceRange(int sourceType, bool valid)
    {
        var dto = CreateQuote().ToDTO() with { SourceType = sourceType, SourcePartnerID = null };
        Assert.Equal(valid, !new QuoteDTOValidator().Validate(dto).Any());
        Assert.Equal(QuoteSourceType.Own, QuoteSourceType.DirectCapture);
        Assert.Equal(QuoteSourceType.Store, QuoteSourceType.ArchitectPartner);
    }

    [Fact]
    public async Task CreateOpenQuoteAsync_PersistsRealIdAndCatalogDraftWithoutParties()
    {
        var repository = new TrackingQuoteRepository();
        var service = CreateService(repository, DecorPermissions.QuotesCreate);

        var dto = await service.CreateOpenQuoteAsync(null, null);

        Assert.True(dto.QuoteID > 0);
        Assert.Equal(0, dto.CustomerID);
        Assert.Equal(0, dto.CreatedByEmployeeID);
        Assert.Null(repository.SavedQuote!.CustomerID);
        Assert.Null(repository.SavedQuote.CreatedByEmployeeID);
        var section = Assert.Single(dto.Sections!);
        Assert.True(section.QuoteSectionID > 0);
        Assert.Equal(dto.QuoteID, section.QuoteID);
        Assert.Equal((int)QuoteSectionType.Catalog, section.SectionType);
        Assert.Equal((int)QuoteSectionStatus.Draft, section.Status);
    }

    [Fact]
    public async Task CreateOpenQuoteAsync_PreservesOptionalCreatorIds()
    {
        var repository = new TrackingQuoteRepository();
        var dto = await CreateService(repository, DecorPermissions.QuotesCreate).CreateOpenQuoteAsync(5, 73);
        Assert.Equal(5, dto.CreatedByEmployeeID);
        Assert.Equal(73, dto.CreatedByUserID);
        Assert.Equal(0m, dto.DiscountAmount);
    }

    [Fact]
    public async Task CreateOpenQuoteAsync_RequiresCreatePermissionBeforePersistence()
    {
        var repository = new TrackingQuoteRepository();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => CreateService(repository).CreateOpenQuoteAsync(null, null));
        Assert.Null(repository.SavedQuote);
    }

    [Fact]
    public async Task SaveQuoteAsync_StillRequiresPartiesForAnOpenQuote()
    {
        var repository = new TrackingQuoteRepository();
        var service = CreateService(repository, DecorPermissions.QuotesCreate, DecorPermissions.QuotesEdit);
        var dto = await service.CreateOpenQuoteAsync(null, null);
        await Assert.ThrowsAsync<ValidationException>(() => service.SaveQuoteAsync(dto));
    }

    [Fact]
    public async Task CancelQuoteAsync_RejectsAllSectionsWithoutDeletingTheQuote()
    {
        var repository = new TrackingQuoteRepository();
        var quote = CreateQuote();
        foreach (var status in new[] { QuoteSectionStatus.Draft, QuoteSectionStatus.AwaitingQuotation, QuoteSectionStatus.Sent, QuoteSectionStatus.Approved })
            quote.Sections.Add(CreateSection(status));
        repository.SetQuoteForTest(quote);

        var service = CreateService(repository, DecorPermissions.QuotesApprove);
        await service.CancelQuoteAsync(1);
        await service.CancelQuoteAsync(1);

        Assert.All(repository.SavedQuote!.Sections, section => Assert.Equal(QuoteSectionStatus.Rejected, section.Status));
        Assert.False(repository.WasDeleted);
    }

    [Fact]
    public async Task CancelQuoteAsync_DoesNotPartiallyCancelConvertedQuote()
    {
        var repository = new TrackingQuoteRepository();
        var quote = CreateQuote();
        quote.Sections.Add(CreateSection(QuoteSectionStatus.Draft));
        quote.Sections.Add(CreateSection(QuoteSectionStatus.ConvertedToOrder));
        repository.SetQuoteForTest(quote);
        await Assert.ThrowsAsync<ValidationException>(() => CreateService(repository, DecorPermissions.QuotesApprove).CancelQuoteAsync(1));
        Assert.Equal(QuoteSectionStatus.Draft, quote.Sections.First().Status);
        Assert.Null(repository.SavedQuote);
    }

    [Fact]
    public async Task CancelQuoteAsync_RequiresApprovePermission()
    {
        var repository = new TrackingQuoteRepository();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => CreateService(repository, DecorPermissions.QuotesEdit).CancelQuoteAsync(1));
        Assert.Null(repository.SavedQuote);
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(11, false)]
    [InlineData(10, true)]
    [InlineData(0, true)]
    public void QuoteDTOValidator_ValidatesDiscountAgainstSubtotal(decimal discount, bool valid)
    {
        var quote = CreateQuote();
        var section = CreateSection(QuoteSectionStatus.Draft);
        section.Items.Add(CreateItem(10m));
        quote.Sections.Add(section);
        var dto = quote.ToDTO() with { DiscountAmount = discount };
        Assert.Equal(valid, !new QuoteDTOValidator().Validate(dto).Any());
    }

    [Fact]
    public async Task SaveQuoteAsync_UsesPersistedItemsToValidateAndPersistDiscount()
    {
        var repository = new TrackingQuoteRepository();
        var quote = CreateQuote();
        var section = CreateSection(QuoteSectionStatus.Draft);
        section.Items.Add(CreateItem(10m));
        quote.Sections.Add(section);
        repository.SetQuoteForTest(quote);
        var service = CreateService(repository, DecorPermissions.QuotesEdit);
        var dto = quote.ToDTO() with { Sections = null, DiscountAmount = 11m };
        await Assert.ThrowsAsync<ValidationException>(() => service.SaveQuoteAsync(dto));
        await service.SaveQuoteAsync(dto with { DiscountAmount = 5m });
        Assert.Equal(5m, repository.SavedQuote!.DiscountAmount);
    }

    [Theory]
    [InlineData(QuoteSectionStatus.AwaitingQuotation)]
    [InlineData(QuoteSectionStatus.Sent)]
    [InlineData(QuoteSectionStatus.Approved)]
    [InlineData(QuoteSectionStatus.Rejected)]
    [InlineData(QuoteSectionStatus.ConvertedToOrder)]
    public async Task DeleteQuoteItemAsync_RequiresDraftSection(QuoteSectionStatus status)
    {
        var repository = CreateDeletionRepository(status);
        await Assert.ThrowsAsync<ValidationException>(() => CreateService(repository, DecorPermissions.QuotesEdit).DeleteQuoteItemAsync(1, 1));
        Assert.Null(repository.DeletedItem);
    }

    [Theory]
    [InlineData(DecorPermissions.QuotesDelete)]
    [InlineData(DecorPermissions.QuotesCreate)]
    [InlineData(DecorPermissions.QuotesView)]
    [InlineData(DecorPermissions.QuotesApprove)]
    [InlineData("")]
    public async Task DeleteQuoteItemAsync_RequiresEditBeforeRepositoryAccess(string permission)
    {
        var repository = CreateDeletionRepository();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => CreateService(repository, permission).DeleteQuoteItemAsync(1, 1));
        Assert.Equal(0, repository.CompleteQuoteReads);
        Assert.Null(repository.DeletedItem);
    }

    [Theory]
    [InlineData(999, 1)]
    [InlineData(1, 999)]
    public async Task DeleteQuoteItemAsync_RejectsMissingQuoteOrCrossQuoteItem(int quoteId, int itemId)
    {
        var repository = CreateDeletionRepository();
        var otherQuote = CreateQuote();
        otherQuote.QuoteID = 999;
        var otherSection = CreateSection(QuoteSectionStatus.Draft);
        otherSection.QuoteID = 999;
        otherSection.QuoteSectionID = 999;
        var otherItem = CreateItem(10m);
        otherItem.QuoteItemID = 999;
        otherItem.QuoteSectionID = 999;
        otherSection.Items.Add(otherItem);
        otherQuote.Sections.Add(otherSection);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => CreateService(repository, DecorPermissions.QuotesEdit).DeleteQuoteItemAsync(quoteId, itemId));
        Assert.Single(otherSection.Items);
        Assert.Null(repository.DeletedItem);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DeleteQuoteItemAsync_RejectsWrongSectionOrQuoteOwner(bool wrongQuote)
    {
        var repository = CreateDeletionRepository();
        var section = (await repository.GetCompleteQuoteAsync(1))!.Sections.Single();
        if (wrongQuote) section.QuoteID = 999;
        else section.Items.Single().QuoteSectionID = 999;

        await Assert.ThrowsAsync<KeyNotFoundException>(() => CreateService(repository, DecorPermissions.QuotesEdit).DeleteQuoteItemAsync(1, 1));
        Assert.Null(repository.DeletedItem);
    }

    [Fact]
    public async Task DeleteQuoteItemAsync_RejectsConvertedDiscountAllocation()
    {
        var repository = CreateDeletionRepository();
        var quote = (await repository.GetCompleteQuoteAsync(1))!;
        quote.DiscountAmount = 1m;
        var converted = CreateSection(QuoteSectionStatus.ConvertedToOrder);
        converted.QuoteSectionID = 2;
        var item = CreateItem(20m);
        item.QuoteItemID = 2;
        item.QuoteSectionID = 2;
        converted.Items.Add(item);
        quote.Sections.Add(converted);

        await Assert.ThrowsAsync<ValidationException>(() => CreateService(repository, DecorPermissions.QuotesEdit).DeleteQuoteItemAsync(1, 1));
        Assert.Null(repository.DeletedItem);
    }

    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(5, 5, true)]
    [InlineData(6, 5, false)]
    [InlineData(1, 0, false)]
    public async Task DeleteQuoteItemAsync_ValidatesRemainingSubtotalAndPersists(int discount, int remainingPrice, bool succeeds)
    {
        var repository = CreateDeletionRepository();
        var quote = (await repository.GetCompleteQuoteAsync(1))!;
        quote.DiscountAmount = discount;
        var otherSection = CreateSection(QuoteSectionStatus.Draft);
        otherSection.QuoteSectionID = 2;
        var remaining = CreateItem(remainingPrice);
        remaining.QuoteItemID = 2;
        remaining.QuoteSectionID = 2;
        otherSection.Items.Add(remaining);
        quote.Sections.Add(otherSection);
        using var cancellation = new CancellationTokenSource();
        var service = CreateService(repository, DecorPermissions.QuotesEdit);

        if (succeeds)
        {
            await service.DeleteQuoteItemAsync(1, 1, cancellation.Token);
            Assert.Equal((1, 1), repository.DeletedItem!.Value);
            Assert.Equal(cancellation.Token, repository.DeleteItemToken);
            Assert.Empty((await repository.GetCompleteQuoteAsync(1))!.Sections.First().Items);
            Assert.Single(otherSection.Items);
            Assert.Equal(discount, quote.DiscountAmount);
            Assert.Null(repository.SavedQuote);
            Assert.False(repository.WasDeleted);
        }
        else
        {
            await Assert.ThrowsAsync<ValidationException>(() => service.DeleteQuoteItemAsync(1, 1, cancellation.Token));
            Assert.Null(repository.DeletedItem);
            Assert.Single(quote.Sections.First().Items);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public async Task DeleteQuoteItemAsync_RequiresExactlyOneAffectedRow(int affectedRows)
    {
        var repository = CreateDeletionRepository();
        repository.DeleteItemAffectedRows = affectedRows;
        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateService(repository, DecorPermissions.QuotesEdit).DeleteQuoteItemAsync(1, 1));
    }

    [Fact]
    public async Task DeleteQuoteItemAsync_RejectsCancellationBeforeRepositoryAccess()
    {
        var repository = CreateDeletionRepository();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CreateService(repository, DecorPermissions.QuotesEdit).DeleteQuoteItemAsync(1, 1, cancellation.Token));
        Assert.Equal(0, repository.CompleteQuoteReads);
        Assert.Null(repository.DeletedItem);
    }

    [Fact]
    public async Task DeleteQuoteItemAsync_RejectsCanceledQuote()
    {
        var repository = CreateDeletionRepository();
        var service = CreateService(repository, DecorPermissions.QuotesApprove, DecorPermissions.QuotesEdit);
        await service.CancelQuoteAsync(1);
        await Assert.ThrowsAsync<ValidationException>(() => service.DeleteQuoteItemAsync(1, 1));
        Assert.Null(repository.DeletedItem);
    }

    private static TrackingQuoteRepository CreateDeletionRepository(QuoteSectionStatus status = QuoteSectionStatus.Draft)
    {
        var repository = new TrackingQuoteRepository();
        var quote = CreateQuote();
        var section = CreateSection(status);
        section.Items.Add(CreateItem(10m));
        quote.Sections.Add(section);
        repository.SetQuoteForTest(quote);
        return repository;
    }

    private static QuoteService CreateService(TrackingQuoteRepository repository, params string[] permissions)
        => new(repository, new QuoteDTOValidator(), new ValidQuoteRepositoryValidator(), new FixedAuthorizationService(permissions),
            new FixedProductSpecificationAttributeRepository(), new FixedProductRepository());

    [Theory]
    [InlineData(0, null)]
    [InlineData(null, 0)]
    [InlineData(-1, 5)]
    public async Task CreateOpenQuoteAsync_RejectsNonpositiveOptionalIds(int? employeeId, int? userId)
    {
        var repository = new TrackingQuoteRepository();
        await Assert.ThrowsAsync<ValidationException>(() => CreateService(repository, DecorPermissions.QuotesCreate).CreateOpenQuoteAsync(employeeId, userId));
        Assert.Null(repository.SavedQuote);
    }

    [Fact]
    public async Task CreateOpenQuoteAsync_HonorsCancellationBeforePersistence()
    {
        var repository = new TrackingQuoteRepository();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CreateService(repository, DecorPermissions.QuotesCreate).CreateOpenQuoteAsync(null, null, cancellation.Token));
        Assert.Null(repository.SavedQuote);
    }

    [Fact]
    public async Task CancelQuoteAsync_RejectsMissingQuote()
    {
        var repository = new TrackingQuoteRepository();
        await Assert.ThrowsAsync<KeyNotFoundException>(() => CreateService(repository, DecorPermissions.QuotesApprove).CancelQuoteAsync(999));
        Assert.Null(repository.SavedQuote);
    }

    [Fact]
    public async Task SaveQuoteAsync_RejectsChangingDiscountAfterConversion()
    {
        var repository = new TrackingQuoteRepository();
        var quote = CreateQuote();
        quote.DiscountAmount = 5m;
        quote.Sections.Add(CreateSection(QuoteSectionStatus.ConvertedToOrder));
        repository.SetQuoteForTest(quote);
        await Assert.ThrowsAsync<ValidationException>(() => CreateService(repository, DecorPermissions.QuotesEdit)
            .SaveQuoteAsync(quote.ToDTO() with { DiscountAmount = 6m }));
        Assert.Null(repository.SavedQuote);
    }

    [Fact]
    public async Task ConvertedDiscountQuote_PreventsChangesToAllocationBase()
    {
        var repository = new TrackingQuoteRepository();
        var quote = CreateQuote();
        quote.DiscountAmount = 5m;
        quote.Sections.Add(CreateSection(QuoteSectionStatus.ConvertedToOrder));
        quote.Sections.Add(CreateSection(QuoteSectionStatus.Draft));
        repository.SetQuoteForTest(quote);
        var service = CreateService(repository, DecorPermissions.QuotesEdit);
        await Assert.ThrowsAsync<ValidationException>(() => service.CreateSectionAsync(1, QuoteSectionType.Custom));
        await Assert.ThrowsAsync<ValidationException>(() => service.SaveQuoteItemAsync(1, new QuoteItemDTO(0, 1, 2, 1m, 25m, false)));
    }

    [Fact]
    public void Quote_mapper_roundtrips_missing_parties_and_discount()
    {
        var quote = new Quote { DiscountAmount = 12.50m };

        var dto = quote.ToDTO();
        var entity = dto.FromDTO();

        Assert.Equal(0, dto.CustomerID);
        Assert.Equal(0, dto.CreatedByEmployeeID);
        Assert.Null(entity.CustomerID);
        Assert.Null(entity.CreatedByEmployeeID);
        Assert.Equal(12.50m, entity.DiscountAmount);
    }

    [Fact]
    public async Task CreateSectionAsync_PersistsANewDraftSectionForTheQuote()
    {
        var repository = new TrackingQuoteRepository();
        repository.SetQuoteForTest(CreateQuote());
        var service = new QuoteService(
            repository,
            new QuoteDTOValidator(),
            new ValidQuoteRepositoryValidator(),
            new FixedAuthorizationService(DecorPermissions.QuotesEdit),
            new FixedProductSpecificationAttributeRepository(),
            new FixedProductRepository());

        var section = await service.CreateSectionAsync(1, QuoteSectionType.Custom);

        Assert.NotNull(repository.SavedSection);
        Assert.Equal(1, repository.SavedSection.QuoteID);
        Assert.Equal(QuoteSectionType.Custom, repository.SavedSection.SectionType);
        Assert.Equal(QuoteSectionStatus.Draft, repository.SavedSection.Status);
        Assert.Equal(QuoteSectionStatus.Draft, (QuoteSectionStatus)section.Status);
    }

    [Fact]
    public async Task SaveQuoteItemAsync_RejectsASectionOutsideTheQuote()
    {
        var repository = new TrackingQuoteRepository();
        var quote = CreateQuote();
        quote.Sections.Add(CreateSection(QuoteSectionStatus.Draft));
        repository.SetQuoteForTest(quote);
        var service = new QuoteService(
            repository,
            new QuoteDTOValidator(),
            new ValidQuoteRepositoryValidator(),
            new FixedAuthorizationService(DecorPermissions.QuotesEdit),
            new FixedProductSpecificationAttributeRepository(),
            new FixedProductRepository());

        var action = () => service.SaveQuoteItemAsync(1, new QuoteItemDTO(0, 999, 2, 1m, 25m, false));

        await action.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task SaveQuoteItemAsync_RejectsZeroQuantity()
    {
        var repository = new TrackingQuoteRepository();
        var quote = CreateQuote();
        quote.Sections.Add(CreateSection(QuoteSectionStatus.Draft));
        repository.SetQuoteForTest(quote);
        var service = new QuoteService(
            repository,
            new QuoteDTOValidator(),
            new ValidQuoteRepositoryValidator(),
            new FixedAuthorizationService(DecorPermissions.QuotesEdit),
            new FixedProductSpecificationAttributeRepository(),
            new FixedProductRepository());

        var action = () => service.SaveQuoteItemAsync(1, new QuoteItemDTO(0, 1, 2, 0m, 25m, false));

        await action.Should().ThrowAsync<ValidationException>().WithMessage("*quantidade*");
    }

    [Fact]
    public void Quote_mapper_preserves_the_user_who_created_it()
    {
        var quote = CreateQuote();
        quote.CreatedByUserID = 73;

        var dto = quote.ToDTO();

        Assert.Equal(73, dto.CreatedByUserID);
        Assert.Equal(quote.CreatedByEmployeeID, dto.CreatedByEmployeeID);
    }

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

    [Theory]
    [InlineData(false, ProductType.Good, "1.5", false)]
    [InlineData(false, ProductType.Good, "2.000", true)]
    [InlineData(false, ProductType.Good, "999999999", true)]
    [InlineData(true, ProductType.Good, "1.125", true)]
    [InlineData(true, ProductType.Good, "1.1251", false)]
    [InlineData(true, ProductType.Good, "999999999.999", true)]
    [InlineData(true, ProductType.Good, "1000000000", false)]
    [InlineData(null, ProductType.Good, "1.125", true)]
    [InlineData(null, ProductType.Good, "1.1251", false)]
    [InlineData(null, ProductType.Service, "0.125", true)]
    [InlineData(null, ProductType.Service, "0.1251", false)]
    public async Task SaveQuoteItemAsync_UsesUnitFractionRuleAndDatabasePrecision(bool? allowsFraction,
        ProductType type, string quantityText, bool valid)
    {
        var quote = CreateQuote();
        quote.Sections.Add(CreateSection(QuoteSectionStatus.Draft));
        var quotes = new Mock<IQuoteRepository>(MockBehavior.Strict);
        quotes.Setup(repository => repository.GetCompleteQuoteAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(quote);
        quotes.Setup(repository => repository.SaveItemAsync(It.IsAny<QuoteItem>(), It.IsAny<CancellationToken>())).ReturnsAsync(1);
        var products = new Mock<IProductRepository>(MockBehavior.Strict);
        products.Setup(repository => repository.SearchGetByAsync("2", 1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new Product { ProductID = 2, IsActive = true, ProductType = type,
                StockUnitID = allowsFraction.HasValue ? 9 : null } });
        var units = new Mock<IUnitOfMeasureRepository>(MockBehavior.Strict);
        if (allowsFraction.HasValue)
            units.Setup(repository => repository.GetByIdAsync(9, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new UnitOfMeasure { UnitOfMeasureID = 9, AllowsFraction = allowsFraction.Value });
        var service = new QuoteService(quotes.Object, new QuoteDTOValidator(), new ValidQuoteRepositoryValidator(),
            new FixedAuthorizationService(DecorPermissions.QuotesEdit), new FixedProductSpecificationAttributeRepository(),
            products.Object, unitOfMeasureRepository: units.Object);
        var quantity = decimal.Parse(quantityText, System.Globalization.CultureInfo.InvariantCulture);
        var item = new QuoteItemDTO(0, 1, 2, quantity, 25m, false);

        if (valid)
            Assert.Equal(quantity, (await service.SaveQuoteItemAsync(1, item)).Quantity);
        else
            await Assert.ThrowsAsync<ValidationException>(() => service.SaveQuoteItemAsync(1, item));

        quotes.Verify(repository => repository.SaveItemAsync(It.Is<QuoteItem>(saved => saved.Quantity == quantity),
            It.IsAny<CancellationToken>()), valid ? Times.Once() : Times.Never());
        units.Verify(repository => repository.GetByIdAsync(9, It.IsAny<CancellationToken>()),
            allowsFraction.HasValue ? Times.Once() : Times.Never());
        units.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task SaveQuoteItemAsync_RejectsPartialProductIdMatch()
    {
        var repository = new TrackingQuoteRepository();
        var quote = CreateQuote();
        quote.Sections.Add(CreateSection(QuoteSectionStatus.Draft));
        repository.SetQuoteForTest(quote);
        var products = new Mock<IProductRepository>();
        products.Setup(productRepository => productRepository.SearchGetByAsync("2", 1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new Product { ProductID = 12, IsActive = true } });
        var service = new QuoteService(repository, new QuoteDTOValidator(), new ValidQuoteRepositoryValidator(),
            new FixedAuthorizationService(DecorPermissions.QuotesEdit), new FixedProductSpecificationAttributeRepository(), products.Object);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.SaveQuoteItemAsync(1, new QuoteItemDTO(0, 1, 2, 1m, 25m, false)));
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
        public QuoteSection? SavedSection { get; private set; }
        public Quote? SavedQuote { get; private set; }
        public bool WasDeleted { get; private set; }
        public (int ItemId, int SectionId)? DeletedItem { get; private set; }
        public CancellationToken DeleteItemToken { get; private set; }
        public int DeleteItemAffectedRows { get; set; } = 1;
        public int CompleteQuoteReads { get; private set; }

        public void SetQuoteForTest(Quote quote) => _quote = quote;

        public int Save(Quote entity) => 1;
        public int Delete(int id) => 1;
        public IEnumerable<Quote> SearchGetBy(string? arg = null) => [];
        public Task<int> SaveAsync(Quote entity, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (entity.QuoteID == 0) entity.QuoteID = 42;
            foreach (var section in entity.Sections)
            {
                section.QuoteID = entity.QuoteID;
                if (section.QuoteSectionID == 0) section.QuoteSectionID = 81;
            }
            SavedQuote = entity;
            _quote = entity;
            return Task.FromResult(1);
        }
        public Task<int> DeleteAsync(int id, CancellationToken cancellationToken = default)
        {
            WasDeleted = true;
            return Task.FromResult(1);
        }
        public Task<IReadOnlyList<Quote>> SearchGetByAsync(string? arg = null, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Quote>>([]);
        public Task<Quote?> GetByIdAsync(int quoteId, CancellationToken cancellationToken = default) => Task.FromResult<Quote?>(_quote);
        public Task<Quote?> GetCompleteQuoteAsync(int quoteId, CancellationToken cancellationToken = default)
        {
            CompleteQuoteReads++;
            return Task.FromResult(_quote?.QuoteID == quoteId ? _quote : null);
        }
        public Task<QuoteSection?> GetSectionByItemIdAsync(int quoteItemId, CancellationToken cancellationToken = default) => Task.FromResult(_quote?.Sections.FirstOrDefault(s => s.Items.Any(i => i.QuoteItemID == quoteItemId)));
        public Task<QuoteSection?> GetSectionByIdAsync(int quoteSectionId, CancellationToken cancellationToken = default) => Task.FromResult(_quote?.Sections.FirstOrDefault(s => s.QuoteSectionID == quoteSectionId));
        public Task<QuoteItem?> GetItemByIdAsync(int quoteItemId, CancellationToken cancellationToken = default) => Task.FromResult(_quote?.Sections.SelectMany(s => s.Items).FirstOrDefault(i => i.QuoteItemID == quoteItemId));
        public Task<IEnumerable<QuoteItemSpecificationValue>> GetSpecificationValuesByItemIdAsync(int quoteItemId, CancellationToken cancellationToken = default)
            => Task.FromResult<IEnumerable<QuoteItemSpecificationValue>>(_quote?.Sections.SelectMany(s => s.Items).FirstOrDefault(i => i.QuoteItemID == quoteItemId)?.SpecificationValues ?? []);
        public Task<int> SaveSectionAsync(QuoteSection section, CancellationToken cancellationToken = default)
        {
            section.QuoteSectionID = 2;
            SavedSection = section;
            _quote?.Sections.Add(section);
            return Task.FromResult(1);
        }
        public Task<int> SaveItemAsync(QuoteItem item, CancellationToken cancellationToken = default) => Task.FromResult(1);
        public Task<int> DeleteItemAsync(int quoteItemId, int sectionId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DeletedItem = (quoteItemId, sectionId);
            DeleteItemToken = cancellationToken;
            if (DeleteItemAffectedRows == 1)
            {
                var section = _quote!.Sections.Single(candidate => candidate.QuoteSectionID == sectionId);
                section.Items.Remove(section.Items.Single(item => item.QuoteItemID == quoteItemId));
            }
            return Task.FromResult(DeleteItemAffectedRows);
        }
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
        public bool UnitOfMeasureExists(int unitOfMeasureId) => true;
        public bool ServiceProductExists(int productId) => true;
        public bool GoodProductExists(int productId) => true;
    }
}
