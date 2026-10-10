using System.Globalization;
using System.Reflection;
using System.ComponentModel.DataAnnotations;
using Decor.AvaloniaUI.ViewModels;
using Decor.Application.CrossCutting.IoC;
using Decor.Application.Services;
using Decor.Core.Common;
using Decor.Core.DTOs;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Services;
using Moq;
using Microsoft.Extensions.DependencyInjection;

namespace Decor.Application.Tests;

public sealed class QuotesViewModelTests
{
    [Fact]
    public async Task Persisted_quote_without_edit_permission_cannot_save_export_or_convert()
    {
        var fixture = new Fixture();
        await fixture.ViewModel.BeginNewAsync();
        fixture.Authorization.Setup(service => service.HasPermission(DecorPermissions.QuotesEdit)).Returns(false);

        Assert.False(fixture.ViewModel.CanSave);
        Assert.False(fixture.ViewModel.SaveCommand.CanExecute(null));
        Assert.False(fixture.ViewModel.GeneratePdfCommand.CanExecute(null));
        Assert.False(fixture.ViewModel.ConvertToOrderCommand.CanExecute(null));
        Assert.False(fixture.ViewModel.CanManageLines);
    }

    [Fact]
    public async Task InitializeAsync_LoadsQuotesWithoutSearchingProducts()
    {
        var fixture = new Fixture();

        await fixture.ViewModel.InitializeAsync();

        Assert.Equal(42, Assert.Single(fixture.ViewModel.Items).QuoteID);
        fixture.Products.VerifyNoOtherCalls();
        Assert.False(fixture.ViewModel.IsBusy);
    }

    [Fact]
    public async Task InitializeAsync_ProjectsQuoteListNamesStatusAndTotal()
    {
        var fixture = new Fixture();
        var createdAt = new DateTime(2026, 10, 6, 12, 0, 0, DateTimeKind.Utc);
        var quote = new QuoteDTO(42, 10, 5, null, (int)QuoteSourceType.Own, createdAt, null,
            CustomerName: "Cliente da lista", CreatedByEmployeeName: "Vendedor da lista",
            ListStatus: "ABERTO", ListTotal: 1234.56m);
        fixture.Quotes.Setup(service => service.SearchQuotesAsync(string.Empty, 1, 500, It.IsAny<CancellationToken>()))
            .ReturnsAsync([quote]);

        await fixture.ViewModel.InitializeAsync();

        var item = Assert.Single(fixture.ViewModel.Items);
        Assert.Equal("Cliente da lista", item.CustomerName);
        Assert.Equal("Vendedor da lista", item.CreatedByEmployeeName);
        Assert.Equal("ABERTO", item.Status);
        Assert.Equal(1234.56m, item.Total);
        Assert.Equal("R$ 1.234,56", item.TotalDisplay);
    }

    [Fact]
    public async Task BeginNewAsync_OpensPersistedDraftWithoutLoadingCatalogs()
    {
        var fixture = new Fixture();

        await fixture.ViewModel.BeginNewAsync();

        fixture.Quotes.Verify(service => service.CreateOpenQuoteAsync(null, null, It.IsAny<CancellationToken>()), Times.Once);
        fixture.Quotes.Verify(service => service.GetQuantityUnitsAsync(1, 100, It.IsAny<CancellationToken>()), Times.Once);
        fixture.Quotes.VerifyNoOtherCalls();
        Assert.True(fixture.ViewModel.IsEditing);
        Assert.Equal(42, fixture.ViewModel.CurrentQuoteId);
        Assert.Equal("ABERTO", fixture.ViewModel.QuoteState);
        Assert.True(fixture.ViewModel.IsQuoteOpen);
        Assert.False(fixture.ViewModel.IsQuoteCancelled);
        Assert.Equal((int)QuoteSectionStatus.Draft, Assert.Single(fixture.ViewModel.Sections).DTO.Status);
        Assert.Empty(fixture.ViewModel.CatalogProducts);
        Assert.Empty(fixture.ViewModel.CatalogServices);
        Assert.Empty(fixture.ViewModel.Products);
        fixture.Products.VerifyNoOtherCalls();
        Assert.False(fixture.ViewModel.IsBusy);
        Assert.False(fixture.ViewModel.HasError);
    }

    [Fact]
    public async Task BeginNewAsync_WhenOpeningFails_DoesNotShowEditorAndReportsError()
    {
        var fixture = new Fixture();
        fixture.Quotes.Setup(service => service.CreateOpenQuoteAsync(null, null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Opening failed."));

        await fixture.ViewModel.BeginNewAsync();

        Assert.False(fixture.ViewModel.IsEditing);
        Assert.False(fixture.ViewModel.IsBusy);
        Assert.Equal(0, fixture.ViewModel.CurrentQuoteId);
        Assert.Empty(fixture.ViewModel.Sections);
        Assert.True(fixture.ViewModel.HasError);
        Assert.Equal("Opening failed.", fixture.ViewModel.ErrorMessage);
        Assert.Equal("N\u00e3o foi poss\u00edvel abrir o or\u00e7amento.", fixture.ViewModel.StatusMessage);
        fixture.Quotes.Verify(service => service.CreateOpenQuoteAsync(null, null, It.IsAny<CancellationToken>()), Times.Once);
        fixture.Quotes.Verify(service => service.GetQuantityUnitsAsync(1, 100, It.IsAny<CancellationToken>()), Times.Once);
        fixture.Quotes.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task SaveLineCommand_SelectedProductPersistsQuantityAndPriceAndReloadsItems()
    {
        var fixture = new Fixture();
        await fixture.ViewModel.BeginNewAsync();
        await SearchAsync(fixture.ViewModel, "Product");
        fixture.ViewModel.SelectedProduct = Assert.Single(fixture.ViewModel.CatalogProducts);
        Assert.Equal(100m.ToString(CultureInfo.CurrentCulture), fixture.ViewModel.UnitPriceInput);
        fixture.ViewModel.QuantityInput = "2";
        fixture.ViewModel.UnitPriceInput = 125.5m.ToString(CultureInfo.CurrentCulture);
        fixture.ViewModel.HasInstallationService = true;
        Assert.True(fixture.ViewModel.SaveLineCommand.CanExecute(null));

        await InvokeAsync(fixture.ViewModel, "SaveLineAsync");

        fixture.Quotes.Verify(service => service.SaveQuoteItemAsync(42,
            It.Is<QuoteItemDTO>(item => item.QuoteItemID == 0 && item.QuoteSectionID == 81
                && item.ProductID == 7 && item.Quantity == 2m && item.UnitPrice == 125.5m
                && item.HasInstallationService), It.IsAny<CancellationToken>()), Times.Once);
        fixture.Quotes.Verify(service => service.GetQuoteByIdAsync(42, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(251m, Assert.Single(fixture.ViewModel.AllLines).Total);
        Assert.Equal(251m, fixture.ViewModel.QuoteTotal);
        Assert.Null(fixture.ViewModel.SelectedLine);
        Assert.Equal("1", fixture.ViewModel.QuantityInput);
        Assert.Empty(fixture.ViewModel.UnitPriceInput);
        Assert.False(fixture.ViewModel.HasInstallationService);
        Assert.False(fixture.ViewModel.HasError);
    }

    [Fact]
    public async Task SelectedLine_UpdatesExistingItemAndNewLineResetsEditor()
    {
        var fixture = new Fixture(new QuoteItemDTO(11, 81, 7, 2m, 90m, true));
        await fixture.ViewModel.BeginNewAsync();
        fixture.ViewModel.SelectedLine = Assert.Single(fixture.ViewModel.AllLines);
        Assert.Equal(7, fixture.ViewModel.SelectedProduct?.ProductID);
        Assert.Equal("2", fixture.ViewModel.QuantityInput);
        Assert.Equal(90m.ToString(CultureInfo.CurrentCulture), fixture.ViewModel.UnitPriceInput);
        Assert.True(fixture.ViewModel.HasInstallationService);
        fixture.ViewModel.QuantityInput = "3";
        fixture.ViewModel.UnitPriceInput = "80";

        await InvokeAsync(fixture.ViewModel, "SaveLineAsync");

        fixture.Quotes.Verify(service => service.SaveQuoteItemAsync(42,
            It.Is<QuoteItemDTO>(item => item.QuoteItemID == 11 && item.ProductID == 7
                && item.Quantity == 3m && item.UnitPrice == 80m && item.HasInstallationService),
            It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(11, Assert.Single(fixture.ViewModel.AllLines).DTO.QuoteItemID);
        fixture.ViewModel.SelectedLine = Assert.Single(fixture.ViewModel.AllLines);
        fixture.ViewModel.NewLineCommand.Execute(null);
        Assert.Null(fixture.ViewModel.SelectedLine);
        Assert.Null(fixture.ViewModel.SelectedProduct);
        Assert.Equal("1", fixture.ViewModel.QuantityInput);
        Assert.Empty(fixture.ViewModel.UnitPriceInput);
        Assert.False(fixture.ViewModel.HasInstallationService);
        Assert.Equal(81, fixture.ViewModel.SelectedSection?.DTO.QuoteSectionID);
        Assert.False(fixture.ViewModel.SaveLineCommand.CanExecute(null));
    }

    [Fact]
    public async Task SaveCommand_PersistsDiscountAndKeepsNetTotalAfterReload()
    {
        var fixture = new Fixture(new QuoteItemDTO(11, 81, 7, 2m, 100m, false));
        await fixture.ViewModel.BeginNewAsync();
        fixture.ViewModel.SelectedCustomer = new QuoteCustomerOption(10, "Customer", null);
        fixture.ViewModel.SelectedEmployee = new QuoteEmployeeOption(5, "Employee", null);
        fixture.ViewModel.DiscountInput = "30";
        Assert.Equal(200m, fixture.ViewModel.QuoteTotal);
        Assert.Equal(170m, fixture.ViewModel.NetTotal);
        Assert.True(fixture.ViewModel.SaveCommand.CanExecute(null));

        var saved = await (Task<bool>)InvokeAsync(fixture.ViewModel, "SaveAsync");

        Assert.True(saved);
        fixture.Quotes.Verify(service => service.SaveQuoteAsync(It.Is<QuoteDTO>(quote => quote.QuoteID == 42
            && quote.CustomerID == 10 && quote.CreatedByEmployeeID == 5 && quote.DiscountAmount == 30m),
            It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(170m, fixture.ViewModel.NetTotal);
        Assert.Equal(30m, fixture.ViewModel.DiscountAmount);
        Assert.Equal(1, fixture.ViewModel.ItemCount);
        Assert.True(fixture.ViewModel.IsEditing);
        Assert.False(fixture.ViewModel.HasError);
    }

    [Fact]
    public async Task SelectedLine_AfterRestrictedCatalogSearch_RetainsItsProductForEditing()
    {
        var fixture = new Fixture(new QuoteItemDTO(11, 81, 7, 2m, 90m, false));
        await fixture.ViewModel.BeginNewAsync();
        fixture.ViewModel.CatalogTabIndex = 1;

        await SearchAsync(fixture.ViewModel, "Service");
        Assert.Empty(fixture.ViewModel.CatalogProducts);
        Assert.Equal(8, Assert.Single(fixture.ViewModel.CatalogServices).ServiceID);
        fixture.ViewModel.SelectedLine = Assert.Single(fixture.ViewModel.AllLines);

        Assert.Equal(7, fixture.ViewModel.SelectedProduct?.ProductID);
        Assert.True(fixture.ViewModel.SaveLineCommand.CanExecute(null));
        Assert.Equal("2", fixture.ViewModel.QuantityInput);
        Assert.Equal(90m.ToString(CultureInfo.CurrentCulture), fixture.ViewModel.UnitPriceInput);
    }

    [Fact]
    public async Task BeginEditAsync_LoadsOnlyKnownItemProductsById()
    {
        var fixture = new Fixture(new QuoteItemDTO(11, 81, 7, 2m, 90m, false));
        await fixture.ViewModel.InitializeAsync();
        fixture.ViewModel.SelectedItem = Assert.Single(fixture.ViewModel.Items);

        await fixture.ViewModel.BeginEditAsync();

        fixture.Products.Verify(service => service.GetProductByIdAsync(7, It.IsAny<CancellationToken>()), Times.Once);
        fixture.Products.Verify(service => service.SearchProductsAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Empty(fixture.ViewModel.Products);
        Assert.Empty(fixture.ViewModel.CatalogProducts);
        Assert.Empty(fixture.ViewModel.CatalogServices);
        Assert.Equal("Produto", Assert.Single(fixture.ViewModel.AllLines).Category);
        fixture.ViewModel.SelectedLine = Assert.Single(fixture.ViewModel.AllLines);
        Assert.Equal(7, fixture.ViewModel.SelectedProduct?.ProductID);
    }

    [Fact]
    public async Task ProductSearchText_DebouncesLatestTypedQuery()
    {
        var fixture = new Fixture();
        var requested = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Products.Setup(service => service.SearchProductsAsync("final marca:Acme tipo:produto", 1, 25, It.IsAny<CancellationToken>()))
            .Returns((string query, int page, int size, CancellationToken token) =>
            {
                requested.TrySetResult(query);
                return Task.FromResult<IEnumerable<ProductDTO>>([Product(7, ProductType.Good)]);
            });
        await fixture.ViewModel.BeginNewAsync();
        fixture.ViewModel.ProductSearchText = "first";
        fixture.ViewModel.ProductSearchText = "final marca:Acme";

        Assert.Equal("final marca:Acme tipo:produto", await requested.Task.WaitAsync(TimeSpan.FromSeconds(5)));
        await WaitUntilAsync(() => fixture.ViewModel.CatalogProducts.Count == 1 && !fixture.ViewModel.IsCatalogBusy);

        fixture.Products.Verify(service => service.SearchProductsAsync(It.IsAny<string>(), 1, 25, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(7, Assert.Single(fixture.ViewModel.CatalogProducts).ProductID);
    }

    [Theory]
    [InlineData(24, false)]
    [InlineData(25, false)]
    [InlineData(26, true)]
    public async Task CatalogPagination_UsesSamePageSizeForLookaheadAndRespectsBoundaries(int total, bool hasNext)
    {
        var fixture = new Fixture();
        var products = Enumerable.Range(1, total).Select(id => Product(id, ProductType.Good)).ToArray();
        fixture.Products.Setup(service => service.SearchProductsAsync("paged tipo:produto", It.IsAny<int>(), 25, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string query, int page, int size, CancellationToken token) => products.Skip((page - 1) * size).Take(size));
        await fixture.ViewModel.BeginNewAsync();

        await SearchAsync(fixture.ViewModel, "paged");

        Assert.Equal(Math.Min(total, 25), fixture.ViewModel.CatalogProducts.Count);
        Assert.Equal(hasNext, fixture.ViewModel.HasCatalogNext);
        Assert.False(fixture.ViewModel.HasCatalogPrevious);
        Assert.Equal("Página 1", fixture.ViewModel.CatalogPageDisplay);
        Assert.False(fixture.ViewModel.CatalogPreviousCommand.CanExecute(null));
        Assert.Equal(hasNext, fixture.ViewModel.CatalogNextCommand.CanExecute(null));
        fixture.Products.Verify(service => service.SearchProductsAsync("paged tipo:produto", 2, 25, It.IsAny<CancellationToken>()), total >= 25 ? Times.Once() : Times.Never());
        if (!hasNext) return;

        fixture.ViewModel.CatalogNextCommand.Execute(null);
        await WaitUntilAsync(() => fixture.ViewModel.CatalogPageDisplay == "Página 2" && !fixture.ViewModel.IsCatalogBusy);
        Assert.Equal(26, Assert.Single(fixture.ViewModel.CatalogProducts).ProductID);
        Assert.False(fixture.ViewModel.HasCatalogNext);
        Assert.True(fixture.ViewModel.HasCatalogPrevious);
        fixture.ViewModel.CatalogPreviousCommand.Execute(null);
        await WaitUntilAsync(() => fixture.ViewModel.CatalogPageDisplay == "Página 1" && !fixture.ViewModel.IsCatalogBusy);
        Assert.Equal(25, fixture.ViewModel.CatalogProducts.Count);
    }

    [Theory]
    [InlineData(10)]
    [InlineData(25)]
    [InlineData(50)]
    [InlineData(100)]
    public async Task CatalogPaginationAdapter_NavigatesServerPagesAndReloadsSizeAtPageOne(int pageSize)
    {
        var fixture = new Fixture();
        var products = Enumerable.Range(1, 201).Select(id => Product(id, ProductType.Good)).ToArray();
        fixture.Products.Setup(service => service.SearchProductsAsync("paged tipo:produto", It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string query, int page, int size, CancellationToken token) => products.Skip((page - 1) * size).Take(size));
        await fixture.ViewModel.BeginNewAsync();
        IStatusBarSource pagination = fixture.ViewModel.CatalogPagination;
        Assert.Equal(25, pagination.SelectedPageSize);
        Assert.Equal(new[] { 10, 25, 50, 100 }, pagination.PageSizeOptions);
        Assert.NotSame(fixture.ViewModel.Listing, pagination);
        Assert.Empty(fixture.ViewModel.CatalogProducts);
        Assert.True(pagination.HasPagination);
        Assert.False(pagination.FirstPageCommand!.CanExecute(null));
        Assert.False(pagination.PreviousPageCommand!.CanExecute(null));
        Assert.False(pagination.NextPageCommand!.CanExecute(null));
        Assert.False(pagination.LastPageCommand!.CanExecute(null));
        await SearchAsync(fixture.ViewModel, "paged");
        Assert.True(pagination.HasPagination);
        Assert.Equal("Página 1", pagination.PaginationStatus);
        Assert.Equal("Página 1", pagination.PaginationPageStatus);
        Assert.False(pagination.FirstPageCommand!.CanExecute(null));
        Assert.False(pagination.PreviousPageCommand!.CanExecute(null));
        Assert.False(pagination.HasLastPage);
        Assert.False(pagination.LastPageCommand!.CanExecute(null));
        pagination.NextPageCommand!.Execute(null);
        await WaitUntilAsync(() => fixture.ViewModel.CatalogPageDisplay == "Página 2" && !fixture.ViewModel.IsCatalogBusy);
        pagination.SelectedPageSize = pageSize;
        await WaitUntilAsync(() => fixture.ViewModel.CatalogProducts.Count == pageSize && !fixture.ViewModel.IsCatalogBusy);
        if (pageSize == 25)
        {
            Assert.Equal("Página 2", pagination.PaginationPageStatus);
            pagination.FirstPageCommand.Execute(null);
            await WaitUntilAsync(() => fixture.ViewModel.CatalogPageDisplay == "Página 1" && !fixture.ViewModel.IsCatalogBusy);
        }
        Assert.Equal("Página 1", pagination.PaginationPageStatus);
        Assert.Equal(1, fixture.ViewModel.CatalogProducts[0].ProductID);
        fixture.Products.Verify(service => service.SearchProductsAsync("paged tipo:produto", 2, pageSize, It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        pagination.NextPageCommand.Execute(null);
        await WaitUntilAsync(() => fixture.ViewModel.CatalogPageDisplay == "Página 2" && !fixture.ViewModel.IsCatalogBusy);
        Assert.True(pagination.FirstPageCommand.CanExecute(null));
        pagination.PreviousPageCommand.Execute(null);
        await WaitUntilAsync(() => fixture.ViewModel.CatalogPageDisplay == "Página 1" && !fixture.ViewModel.IsCatalogBusy);
        pagination.NextPageCommand.Execute(null);
        await WaitUntilAsync(() => fixture.ViewModel.CatalogPageDisplay == "Página 2" && !fixture.ViewModel.IsCatalogBusy);
        pagination.NextPageCommand.Execute(null);
        await WaitUntilAsync(() => fixture.ViewModel.CatalogPageDisplay == "Página 3" && !fixture.ViewModel.IsCatalogBusy);
        pagination.FirstPageCommand.Execute(null);
        await WaitUntilAsync(() => fixture.ViewModel.CatalogPageDisplay == "Página 1" && !fixture.ViewModel.IsCatalogBusy);
        Assert.Equal(1, fixture.ViewModel.CatalogProducts[0].ProductID);
        pagination.SelectedPageSize = 11;
        Assert.Equal(pageSize, pagination.SelectedPageSize);
        Assert.Equal(10, fixture.ViewModel.SelectedPageSize);
        await fixture.ViewModel.ReturnToListAsync();
        Assert.False(pagination.HasPagination);
        Assert.False(pagination.NextPageCommand.CanExecute(null));
    }

    [Fact]
    public async Task CatalogPaginationAdapter_SizeChangeCancelsPendingPageAndNotifiesFooter()
    {
        var fixture = new Fixture();
        var pending = new TaskCompletionSource<IEnumerable<ProductDTO>>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken requestToken = default;
        var products = Enumerable.Range(1, 51).Select(id => Product(id, ProductType.Good)).ToArray();
        fixture.Products.Setup(service => service.SearchProductsAsync("paged tipo:produto", It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string query, int page, int size, CancellationToken token) => products.Skip((page - 1) * size).Take(size));
        await fixture.ViewModel.BeginNewAsync();
        await SearchAsync(fixture.ViewModel, "paged");
        fixture.Products.Setup(service => service.SearchProductsAsync("paged tipo:produto", 2, 25, It.IsAny<CancellationToken>()))
            .Returns((string query, int page, int size, CancellationToken token) => { requestToken = token; return pending.Task; });
        IStatusBarSource pagination = fixture.ViewModel.CatalogPagination;
        var notifications = new List<string?>();
        pagination.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);
        pagination.NextPageCommand!.Execute(null);
        Assert.True(fixture.ViewModel.IsCatalogBusy);
        Assert.False(pagination.NextPageCommand.CanExecute(null));
        Assert.False(pagination.PreviousPageCommand!.CanExecute(null));
        Assert.False(pagination.FirstPageCommand!.CanExecute(null));
        pagination.SelectedPageSize = 50;
        await WaitUntilAsync(() => fixture.ViewModel.CatalogProducts.Count == 50 && !fixture.ViewModel.IsCatalogBusy);
        Assert.True(requestToken.IsCancellationRequested);
        pending.SetException(new InvalidOperationException("Stale page failure"));
        Assert.Equal("Página 1", pagination.PaginationPageStatus);
        Assert.Equal(1, fixture.ViewModel.CatalogProducts[0].ProductID);
        Assert.True(pagination.NextPageCommand.CanExecute(null));
        Assert.Contains(nameof(IStatusBarSource.SelectedPageSize), notifications);
        Assert.Contains(nameof(IStatusBarSource.PaginationPageStatus), notifications);
        Assert.Contains(nameof(IStatusBarSource.HasNextPage), notifications);
        fixture.Products.Verify(service => service.SearchProductsAsync("paged tipo:produto", 1, 50, It.IsAny<CancellationToken>()), Times.Once);
        fixture.Products.Verify(service => service.SearchProductsAsync("paged tipo:produto", 2, 50, It.IsAny<CancellationToken>()), Times.Once);
        pagination.NextPageCommand.Execute(null);
        await WaitUntilAsync(() => fixture.ViewModel.CatalogPageDisplay == "Página 2" && !fixture.ViewModel.IsCatalogBusy);
        Assert.Equal(51, Assert.Single(fixture.ViewModel.CatalogProducts).ProductID);
        Assert.False(pagination.HasNextPage);
        Assert.False(pagination.NextPageCommand.CanExecute(null));
        Assert.False(fixture.ViewModel.HasError);
    }

    [Fact]
    public async Task CatalogTabIndex_SearchesOnlySelectedKindAndClearsOtherResults()
    {
        var fixture = new Fixture();
        await fixture.ViewModel.BeginNewAsync();
        await SearchAsync(fixture.ViewModel, "Product");
        fixture.Services.Setup(service => service.SearchServicesAsync("Product", 1, 25, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Service(8) });

        fixture.ViewModel.CatalogTabIndex = 1;

        await WaitUntilAsync(() => fixture.ViewModel.CatalogServices.Count == 1 && !fixture.ViewModel.IsCatalogBusy);
        Assert.Empty(fixture.ViewModel.CatalogProducts);
        Assert.Equal("Serviço", Assert.Single(fixture.ViewModel.CatalogServices).Category);
        fixture.Services.Verify(service => service.SearchServicesAsync("Product", 1, 25, It.IsAny<CancellationToken>()), Times.Once);
        fixture.Products.Verify(service => service.SearchProductsAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CatalogSearch_IgnoresOlderCompletionAndErrors(bool failOlder)
    {
        var fixture = new Fixture();
        var older = new TaskCompletionSource<IEnumerable<ProductDTO>>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken oldToken = default;
        fixture.Products.Setup(service => service.SearchProductsAsync("old tipo:produto", 1, 25, It.IsAny<CancellationToken>()))
            .Returns((string query, int page, int size, CancellationToken token) => { oldToken = token; return older.Task; });
        fixture.Products.Setup(service => service.SearchProductsAsync("latest tipo:produto", 1, 25, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Product(17, ProductType.Good) });
        await fixture.ViewModel.BeginNewAsync();
        fixture.ViewModel.ProductSearchText = "old";
        var oldSearch = InvokeAsync(fixture.ViewModel, "SearchProductsAsync");
        Assert.True(fixture.ViewModel.IsCatalogBusy);
        Assert.False(fixture.ViewModel.CatalogNextCommand.CanExecute(null));

        await SearchAsync(fixture.ViewModel, "latest");
        if (failOlder) older.SetException(new InvalidOperationException("Old failure"));
        else older.SetResult([Product(7, ProductType.Good)]);
        await oldSearch;

        Assert.True(oldToken.IsCancellationRequested);
        Assert.Equal(17, Assert.Single(fixture.ViewModel.CatalogProducts).ProductID);
        Assert.False(fixture.ViewModel.HasError);
        Assert.False(fixture.ViewModel.IsCatalogBusy);
    }

    [Fact]
    public async Task BlankSearch_CancelsPendingResultWithoutRequestAndPreservesSelection()
    {
        var fixture = new Fixture();
        var pending = new TaskCompletionSource<IEnumerable<ProductDTO>>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken requestToken = default;
        fixture.Products.Setup(service => service.SearchProductsAsync("pending tipo:produto", 1, 25, It.IsAny<CancellationToken>()))
            .Returns((string query, int page, int size, CancellationToken token) => { requestToken = token; return pending.Task; });
        await fixture.ViewModel.BeginNewAsync();
        await SearchAsync(fixture.ViewModel, "Product");
        fixture.ViewModel.SelectedProduct = Assert.Single(fixture.ViewModel.CatalogProducts);
        fixture.ViewModel.ProductSearchText = "pending";
        var search = InvokeAsync(fixture.ViewModel, "SearchProductsAsync");

        fixture.ViewModel.ProductSearchText = "  ";
        pending.SetResult([Product(9, ProductType.Good)]);
        await search;
        await InvokeAsync(fixture.ViewModel, "SearchProductsAsync");

        Assert.True(requestToken.IsCancellationRequested);
        Assert.Empty(fixture.ViewModel.Products);
        Assert.Empty(fixture.ViewModel.CatalogProducts);
        Assert.Empty(fixture.ViewModel.CatalogServices);
        Assert.Equal(7, fixture.ViewModel.SelectedProduct?.ProductID);
        Assert.False(fixture.ViewModel.IsCatalogBusy);
        Assert.False(fixture.ViewModel.HasCatalogNext);
        fixture.Products.Verify(service => service.SearchProductsAsync(It.IsAny<string>(), 1, 25, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task ClearAndReturnToList_QueryAllAndIncludeNewDraftOnlyOnExit()
    {
        var fixture = new Fixture();
        fixture.ViewModel.SearchText = "filtered";
        fixture.ViewModel.ClearSearchCommand.Execute(null);
        await WaitUntilAsync(() => fixture.ViewModel.Items.Count == 1 && !fixture.ViewModel.IsBusy);
        Assert.Empty(fixture.ViewModel.SearchText);
        fixture.Quotes.Verify(service => service.SearchQuotesAsync(string.Empty, 1, 500, It.IsAny<CancellationToken>()), Times.Once);
        fixture.ViewModel.Items.Clear();
        await fixture.ViewModel.BeginNewAsync();
        Assert.Empty(fixture.ViewModel.Items);
        fixture.ViewModel.SearchText = "old";

        await fixture.ViewModel.ReturnToListAsync();

        Assert.False(fixture.ViewModel.IsEditing);
        Assert.Empty(fixture.ViewModel.SearchText);
        Assert.Equal(42, Assert.Single(fixture.ViewModel.Items).QuoteID);
        fixture.Quotes.Verify(service => service.SearchQuotesAsync(string.Empty, 1, 500, It.IsAny<CancellationToken>()), Times.Exactly(2));
        await fixture.ViewModel.BeginNewAsync();
        fixture.ViewModel.CancelCommand.Execute(null);
        await WaitUntilAsync(() => !fixture.ViewModel.IsEditing && !fixture.ViewModel.IsBusy);
        fixture.Quotes.Verify(service => service.SearchQuotesAsync(string.Empty, 1, 500, It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    [Theory]
    [InlineData("Própria", 1)]
    [InlineData("Loja", 2)]
    [InlineData("Outra", 3)]
    public async Task SaveAsync_PersistsSourceDateAndNotesWithoutPartnerRequirement(string source, int sourceId)
    {
        var fixture = new Fixture();
        await fixture.ViewModel.BeginNewAsync();
        Assert.Equal(new[] { "Própria", "Loja", "Outra" }, fixture.ViewModel.SourceTypes);
        Assert.Equal("Própria", fixture.ViewModel.SourceType);
        fixture.ViewModel.SelectedCustomer = new QuoteCustomerOption(10, "Customer", null);
        fixture.ViewModel.SelectedEmployee = new QuoteEmployeeOption(5, "Employee", null);
        fixture.ViewModel.SourceType = source;
        fixture.ViewModel.Notes = new string('x', 65536);
        var chosen = new DateTime(2026, 9, 15, 0, 0, 0, DateTimeKind.Local);
        fixture.ViewModel.SelectedQuoteDate = chosen;
        var stored = fixture.ViewModel.QuoteDate.ToUniversalTime();

        Assert.True(await (Task<bool>)InvokeAsync(fixture.ViewModel, "SaveAsync"));

        fixture.Quotes.Verify(service => service.SaveQuoteAsync(It.Is<QuoteDTO>(quote => quote.SourceType == sourceId
            && quote.SourcePartnerID == null && quote.CreatedAt == stored && quote.CreatedAt.Kind == DateTimeKind.Utc
            && quote.CreatedByUserID == null && quote.Notes!.Length == 65535), It.IsAny<CancellationToken>()), Times.Once);
        fixture.Quotes.Verify(service => service.SearchQuotesAsync(string.Empty, 1, 500, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(65535, fixture.ViewModel.NotesLimit);
        Assert.Equal("65535/65535 caracteres", fixture.ViewModel.NotesCounter);
        Assert.False(fixture.ViewModel.IsPartnerOrigin);
        Assert.True(fixture.ViewModel.IsEditing);
    }

    [Theory]
    [InlineData("\u00e9", 65533, "65534/65535 caracteres", true)]
    [InlineData("\u20ac", 65533, "65533/65535 caracteres", false)]
    [InlineData("\U0001f600", 65531, "65532/65535 caracteres", true)]
    [InlineData("\U0001f600", 65532, "65532/65535 caracteres", false)]
    public void NotesRules_TruncatesSafelyAndCountsCharacters(string suffix, int prefixLength, string counter, bool includesSuffix)
    {
        var fixture = new Fixture();
        var notifications = new List<string?>();
        fixture.ViewModel.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);
        var prefix = new string('x', prefixLength);

        fixture.ViewModel.Notes = prefix + suffix + "extra";

        Assert.Equal(counter, fixture.ViewModel.NotesCounter);
        Assert.Equal(includesSuffix ? prefix + suffix : prefix, fixture.ViewModel.Notes);
        Assert.Contains(nameof(QuotesViewModel.Notes), notifications);
        Assert.Contains(nameof(QuotesViewModel.NotesCounter), notifications);
        fixture.ViewModel.Notes = string.Empty;
        Assert.Equal("0/65535 caracteres", fixture.ViewModel.NotesCounter);
    }

    [Fact]
    public async Task NumericWrappers_NotifyClampAndRoundWhileRetainingStringInputs()
    {
        var fixture = new Fixture(new QuoteItemDTO(11, 81, 7, 2m, 100m, false));
        await fixture.ViewModel.BeginNewAsync();
        var notifications = new List<string?>();
        fixture.ViewModel.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);
        fixture.ViewModel.QuantityValue = 2.5m;
        fixture.ViewModel.QuantityValue = -1m;
        fixture.ViewModel.UnitPriceValue = 12.345m;
        fixture.ViewModel.UnitPriceValue = -1m;
        fixture.ViewModel.DiscountValue = 10.125m;
        fixture.ViewModel.DiscountValue = 201m;
        Assert.Equal(2.5m, fixture.ViewModel.QuantityValue);
        Assert.Equal(12.34m, fixture.ViewModel.UnitPriceValue);
        Assert.Equal(10.12m, fixture.ViewModel.DiscountValue);
        Assert.Contains(nameof(QuotesViewModel.QuantityValue), notifications);
        Assert.Contains(nameof(QuotesViewModel.UnitPriceValue), notifications);
        Assert.Contains(nameof(QuotesViewModel.DiscountValue), notifications);
        fixture.ViewModel.QuantityInput = "3";
        fixture.ViewModel.UnitPriceInput = "4";
        fixture.ViewModel.DiscountInput = "5";
        Assert.Equal(3m, fixture.ViewModel.QuantityValue);
        Assert.Equal(4m, fixture.ViewModel.UnitPriceValue);
        Assert.Equal(5m, fixture.ViewModel.DiscountValue);
        Assert.False(string.IsNullOrWhiteSpace(fixture.ViewModel.CurrencySymbol));
        fixture.ViewModel.SelectedCustomer = new QuoteCustomerOption(10, "Customer", null);
        fixture.ViewModel.SelectedEmployee = new QuoteEmployeeOption(5, "Employee", null);
        fixture.ViewModel.ClearCustomerCommand.Execute(null);
        fixture.ViewModel.ClearEmployeeCommand.Execute(null);
        Assert.False(fixture.ViewModel.HasSelectedCustomer);
        Assert.False(fixture.ViewModel.HasSelectedEmployee);
    }

    [Fact]
    public async Task SaleValue_RecalculatesNotifiesAndPersistsQuantityWithUnitPrice()
    {
        var fixture = new Fixture();
        await fixture.ViewModel.BeginNewAsync();
        await SearchAsync(fixture.ViewModel, "Product");
        var notifications = new List<string?>();
        fixture.ViewModel.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);

        fixture.ViewModel.SelectedProduct = Assert.Single(fixture.ViewModel.CatalogProducts);

        Assert.Equal(100m, fixture.ViewModel.SaleValue);
        Assert.Contains(nameof(QuotesViewModel.SaleValue), notifications);
        notifications.Clear();
        fixture.ViewModel.QuantityValue = 2.5m;
        Assert.Equal(250m, fixture.ViewModel.SaleValue);
        Assert.Contains(nameof(QuotesViewModel.SaleValue), notifications);
        notifications.Clear();
        fixture.ViewModel.SaleValue = 300m;
        Assert.Equal(120m, fixture.ViewModel.UnitPriceValue);
        Assert.Equal(2.5m, fixture.ViewModel.QuantityValue);
        Assert.Equal(300m, fixture.ViewModel.SaleValue);
        Assert.Contains(nameof(QuotesViewModel.SaleValue), notifications);

        await InvokeAsync(fixture.ViewModel, "SaveLineAsync");

        fixture.Quotes.Verify(service => service.SaveQuoteItemAsync(42,
            It.Is<QuoteItemDTO>(item => item.ProductID == 7 && item.Quantity == 2.5m && item.UnitPrice == 120m),
            It.IsAny<CancellationToken>()), Times.Once);
        var persisted = Assert.Single(fixture.ViewModel.AllLines);
        Assert.Equal(2.5m, persisted.DTO.Quantity);
        Assert.Equal(120m, persisted.DTO.UnitPrice);
        Assert.Equal(300m, persisted.Total);
        Assert.Equal(300m, fixture.ViewModel.QuoteTotal);
        Assert.False(fixture.ViewModel.HasError);
    }

    [Fact]
    public async Task DeleteLineAsync_RemovesExistingItemReloadsTotalsAndResetsSelection()
    {
        var fixture = new Fixture(new QuoteItemDTO(11, 81, 7, 2m, 100m, false));
        await fixture.ViewModel.BeginNewAsync();
        var line = Assert.Single(fixture.ViewModel.AllLines);
        fixture.ViewModel.SelectedLine = line;
        Assert.True(fixture.ViewModel.CanDeleteLine(line));
        Assert.Equal(200m, fixture.ViewModel.QuoteTotal);

        await fixture.ViewModel.DeleteLineAsync(line);

        fixture.Quotes.Verify(service => service.DeleteQuoteItemAsync(42, 11, It.IsAny<CancellationToken>()), Times.Once);
        fixture.Quotes.Verify(service => service.GetQuoteByIdAsync(42, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Empty(fixture.ViewModel.AllLines);
        Assert.Empty(fixture.ViewModel.SectionItems);
        Assert.Equal(0m, fixture.ViewModel.QuoteTotal);
        Assert.Null(fixture.ViewModel.SelectedLine);
        Assert.Null(fixture.ViewModel.SelectedProduct);
        Assert.Equal(1m, fixture.ViewModel.QuantityValue);
        Assert.Null(fixture.ViewModel.UnitPriceValue);
        Assert.False(fixture.ViewModel.IsBusy);
        Assert.False(fixture.ViewModel.HasError);
    }

    [Fact]
    public async Task DeleteLineAsync_WhenQuoteIsCancelled_DoesNotCallService()
    {
        var fixture = new Fixture(new QuoteItemDTO(11, 81, 7, 2m, 100m, false));
        await fixture.ViewModel.BeginNewAsync();
        await InvokeAsync(fixture.ViewModel, "CancelQuoteAsync");
        var line = Assert.Single(fixture.ViewModel.AllLines);
        fixture.ViewModel.SelectedLine = line;
        Assert.True(fixture.ViewModel.IsQuoteCancelled);
        Assert.False(fixture.ViewModel.CanDeleteLine(line));

        await fixture.ViewModel.DeleteLineAsync(line);

        fixture.Quotes.Verify(service => service.DeleteQuoteItemAsync(It.IsAny<int>(), It.IsAny<int>(),
            It.IsAny<CancellationToken>()), Times.Never);
        Assert.Same(line, Assert.Single(fixture.ViewModel.AllLines));
        Assert.Same(line, fixture.ViewModel.SelectedLine);
        Assert.Equal(200m, fixture.ViewModel.QuoteTotal);
    }

    [Fact]
    public async Task DeleteLineAsync_WithoutEditPermission_DoesNotCallService()
    {
        var fixture = new Fixture(new QuoteItemDTO(11, 81, 7, 2m, 100m, false));
        await fixture.ViewModel.BeginNewAsync();
        var line = Assert.Single(fixture.ViewModel.AllLines);
        fixture.Authorization.Setup(service => service.HasPermission(DecorPermissions.QuotesEdit)).Returns(false);
        Assert.False(fixture.ViewModel.CanDeleteLine(line));

        await fixture.ViewModel.DeleteLineAsync(line);

        fixture.Quotes.Verify(service => service.DeleteQuoteItemAsync(It.IsAny<int>(), It.IsAny<int>(),
            It.IsAny<CancellationToken>()), Times.Never);
        fixture.Quotes.Verify(service => service.GetQuoteByIdAsync(42, It.IsAny<CancellationToken>()), Times.Never);
        Assert.Same(line, Assert.Single(fixture.ViewModel.AllLines));
        Assert.Equal(200m, fixture.ViewModel.QuoteTotal);
        Assert.False(fixture.ViewModel.IsBusy);
    }

    [Fact]
    public async Task DeleteLineAsync_WhenValidationFails_PreservesItemsAndExposesError()
    {
        var fixture = new Fixture(new QuoteItemDTO(11, 81, 7, 2m, 100m, false));
        fixture.Quotes.Setup(service => service.DeleteQuoteItemAsync(42, 11, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ValidationException("Deletion failed."));
        await fixture.ViewModel.BeginNewAsync();
        var line = Assert.Single(fixture.ViewModel.AllLines);
        var sectionLine = Assert.Single(fixture.ViewModel.SectionItems);
        fixture.ViewModel.SelectedLine = line;

        await fixture.ViewModel.DeleteLineAsync(line);

        fixture.Quotes.Verify(service => service.DeleteQuoteItemAsync(42, 11, It.IsAny<CancellationToken>()), Times.Once);
        fixture.Quotes.Verify(service => service.GetQuoteByIdAsync(42, It.IsAny<CancellationToken>()), Times.Never);
        Assert.Same(line, Assert.Single(fixture.ViewModel.AllLines));
        Assert.Same(sectionLine, Assert.Single(fixture.ViewModel.SectionItems));
        Assert.Same(line, fixture.ViewModel.SelectedLine);
        Assert.Equal(200m, fixture.ViewModel.QuoteTotal);
        Assert.Equal(7, fixture.ViewModel.SelectedProduct?.ProductID);
        Assert.Equal(2m, fixture.ViewModel.QuantityValue);
        Assert.Equal(100m, fixture.ViewModel.UnitPriceValue);
        Assert.True(fixture.ViewModel.HasError);
        Assert.Equal("Deletion failed.", fixture.ViewModel.ErrorMessage);
        Assert.False(fixture.ViewModel.IsBusy);
    }

    private static async Task SearchAsync(QuotesViewModel viewModel, string text)
    {
        viewModel.ProductSearchText = text;
        await InvokeAsync(viewModel, "SearchProductsAsync");
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!condition()) await Task.Delay(10, cancellation.Token);
    }

    [Fact]
    public async Task CancelQuoteAsync_RefreshesListingAndNotifiesStateWithoutBusyGap()
    {
        var fixture = new Fixture();
        await fixture.ViewModel.BeginNewAsync();
        var notifications = new List<string?>();
        var busyStates = new List<bool>();
        fixture.ViewModel.PropertyChanged += (_, args) =>
        {
            notifications.Add(args.PropertyName);
            if (args.PropertyName == nameof(QuotesViewModel.IsBusy)) busyStates.Add(fixture.ViewModel.IsBusy);
        };

        await InvokeAsync(fixture.ViewModel, "CancelQuoteAsync");

        Assert.Equal("CANCELADO", fixture.ViewModel.QuoteState);
        Assert.True(fixture.ViewModel.IsQuoteCancelled);
        Assert.False(fixture.ViewModel.IsQuoteOpen);
        Assert.False(fixture.ViewModel.CanSave);
        Assert.Contains(nameof(QuotesViewModel.IsQuoteCancelled), notifications);
        Assert.Contains(nameof(QuotesViewModel.IsQuoteOpen), notifications);
        Assert.Equal(new[] { true, false }, busyStates);
        Assert.Single(fixture.ViewModel.Items);
        fixture.Quotes.Verify(service => service.SearchQuotesAsync(string.Empty, 1, 500, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task QuoteState_WithConvertedAndRejectedSectionsIsConvertedToOrder()
    {
        var fixture = new Fixture();
        await fixture.ViewModel.BeginNewAsync();
        var createdAt = new DateTime(2026, 10, 6, 12, 0, 0, DateTimeKind.Utc);
        fixture.ViewModel.Sections.Clear();
        fixture.ViewModel.Sections.Add(new QuoteSectionOption(
            new QuoteSectionDTO(81, 42, 1, (int)QuoteSectionStatus.ConvertedToOrder, null, null, createdAt, [], 93), "Pedido"));
        fixture.ViewModel.Sections.Add(new QuoteSectionOption(
            new QuoteSectionDTO(82, 42, 1, (int)QuoteSectionStatus.Rejected, null, null, createdAt, []), "Cancelada"));

        Assert.Equal("Ref. Pedido Nº 93", fixture.ViewModel.QuoteState);
        Assert.True(fixture.ViewModel.IsQuoteConverted);
        Assert.False(fixture.ViewModel.IsQuoteOpen);
        Assert.False(fixture.ViewModel.IsQuoteCancelled);
    }

    [Fact]
    public async Task ConvertToOrderAsync_RefreshesListingAfterSaveAndConversion()
    {
        var fixture = new Fixture(new QuoteItemDTO(11, 81, 7, 2m, 100m, false));
        await fixture.ViewModel.BeginNewAsync();
        fixture.ViewModel.SelectedCustomer = new QuoteCustomerOption(10, "Customer", null);
        fixture.ViewModel.SelectedEmployee = new QuoteEmployeeOption(5, "Employee", null);

        await InvokeAsync(fixture.ViewModel, "ConvertToOrderAsync");

        Assert.Equal("Ref. Pedido Nº 99", fixture.ViewModel.QuoteState);
        Assert.False(fixture.ViewModel.IsQuoteOpen);
        Assert.False(fixture.ViewModel.IsQuoteCancelled);
        Assert.False(fixture.ViewModel.CanSave);
        Assert.False(fixture.ViewModel.HasError);
        Assert.Equal("Pedido(s) criado(s): 99.", fixture.ViewModel.StatusMessage);
        fixture.Orders.Verify(service => service.ConvertFromQuoteAsync(81, It.IsAny<CancellationToken>()), Times.Once);
        fixture.Quotes.Verify(service => service.SearchQuotesAsync(string.Empty, 1, 500, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task ClearProductsCommand_ClearsWithoutSearchingOrResettingLineEditor()
    {
        var fixture = new Fixture(new QuoteItemDTO(11, 81, 7, 2m, 90m, false));
        await fixture.ViewModel.BeginNewAsync();
        await SearchAsync(fixture.ViewModel, "Product");
        fixture.ViewModel.SelectedLine = Assert.Single(fixture.ViewModel.AllLines);

        fixture.ViewModel.ClearProductsCommand.Execute(null);

        Assert.Empty(fixture.ViewModel.ProductSearchText);
        Assert.Empty(fixture.ViewModel.Products);
        Assert.Equal(11, fixture.ViewModel.SelectedLine?.DTO.QuoteItemID);
        Assert.Equal(7, fixture.ViewModel.SelectedProduct?.ProductID);
        Assert.Equal(2m, fixture.ViewModel.QuantityValue);
        Assert.Equal(90m, fixture.ViewModel.UnitPriceValue);
        fixture.Products.Verify(service => service.SearchProductsAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(false, "N0", "1", "999999999")]
    [InlineData(true, "N3", "0.001", "999999999.999")]
    public async Task SelectedProduct_UsesCachedUnitFormatAndBounds(bool allowsFraction, string format,
        string minimum, string maximum)
    {
        var fixture = new Fixture();
        fixture.Quotes.Setup(service => service.GetQuantityUnitsAsync(1, 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new UnitOfMeasureDTO(9, "M", "Metro", allowsFraction, true) });
        await fixture.ViewModel.BeginNewAsync();
        var notifications = new List<string?>();
        fixture.ViewModel.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);

        fixture.ViewModel.SelectedProduct = new QuoteProductOption(Product(7, ProductType.Good) with { StockUnitID = 9 }, "Product");

        Assert.Equal(format, fixture.ViewModel.QuantityFormat);
        Assert.Equal(decimal.Parse(minimum, CultureInfo.InvariantCulture), fixture.ViewModel.QuantityMinimum);
        Assert.Equal(decimal.Parse(maximum, CultureInfo.InvariantCulture), fixture.ViewModel.QuantityMaximum);
        Assert.Equal("M", fixture.ViewModel.QuantityUnitLabel);
        Assert.Equal("Metro", fixture.ViewModel.QuantityUnitDescription);
        Assert.Contains(nameof(QuotesViewModel.QuantityFormat), notifications);
        Assert.Contains(nameof(QuotesViewModel.QuantityMinimum), notifications);
        Assert.Contains(nameof(QuotesViewModel.QuantityMaximum), notifications);
        fixture.Quotes.Verify(service => service.GetQuantityUnitsAsync(1, 100, It.IsAny<CancellationToken>()), Times.Once);
        fixture.Units.VerifyNoOtherCalls();
        fixture.Products.VerifyNoOtherCalls();

        fixture.ViewModel.SelectedProduct = new QuoteProductOption(Service(8), "Service");
        Assert.Equal("N3", fixture.ViewModel.QuantityFormat);
        Assert.Equal(0.001m, fixture.ViewModel.QuantityMinimum);
        Assert.Empty(fixture.ViewModel.QuantityUnitLabel);
        fixture.ViewModel.SelectedProduct = null;
        Assert.Equal("N3", fixture.ViewModel.QuantityFormat);
    }

    [Fact]
    public async Task Catalog_ShowsSalePriceAndUnitUsedForFractionalCalculation()
    {
        var fixture = new Fixture();
        fixture.Quotes.Setup(service => service.GetQuantityUnitsAsync(1, 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new UnitOfMeasureDTO(9, "M", "Metro", true, true) });
        fixture.Products.Setup(service => service.SearchProductsAsync("Product tipo:produto", 1, 25, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Product(7, ProductType.Good) with { StockUnitID = 9, SalePrice = 100m } });
        await fixture.ViewModel.BeginNewAsync();
        await SearchAsync(fixture.ViewModel, "Product");

        var product = Assert.Single(fixture.ViewModel.CatalogProducts);
        Assert.Equal("M", product.Unit);
        Assert.Equal(100m, product.Price);
        fixture.ViewModel.SelectedProduct = product;
        fixture.ViewModel.QuantityValue = 1.125m;
        Assert.Equal(product.Unit, fixture.ViewModel.QuantityUnitLabel);
        Assert.Equal("N3", fixture.ViewModel.QuantityFormat);
        Assert.Equal(112.5m, fixture.ViewModel.SaleValue);

        await InvokeAsync(fixture.ViewModel, "SaveLineAsync");

        fixture.Quotes.Verify(service => service.SaveQuoteItemAsync(42,
            It.Is<QuoteItemDTO>(item => item.ProductID == 7 && item.Quantity == 1.125m && item.UnitPrice == 100m),
            It.IsAny<CancellationToken>()), Times.Once);
        fixture.ViewModel.ApplyLookupSelection(LookupSearchContext.Product, product.DTO);
        Assert.Equal("M", fixture.ViewModel.SelectedProduct!.Unit);
    }

    [Theory]
    [InlineData(false, "1.5", false)]
    [InlineData(false, "2.000", true)]
    [InlineData(true, "1.125", true)]
    [InlineData(true, "1.1251", false)]
    [InlineData(null, "1.125", true)]
    [InlineData(null, "1.1251", false)]
    [InlineData(null, "1000000000", false)]
    public async Task SaveLine_RejectsInvalidPrecisionWithoutRounding(bool? allowsFraction, string quantityText, bool valid)
    {
        var fixture = new Fixture();
        if (allowsFraction.HasValue)
            fixture.Quotes.Setup(service => service.GetQuantityUnitsAsync(1, 100, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { new UnitOfMeasureDTO(9, "M", "Metro", allowsFraction.Value, true) });
        await fixture.ViewModel.BeginNewAsync();
        fixture.ViewModel.SelectedProduct = new QuoteProductOption(Product(7, ProductType.Good) with
            { StockUnitID = allowsFraction.HasValue ? 9 : null }, "Product");
        var quantity = decimal.Parse(quantityText, CultureInfo.InvariantCulture);
        fixture.ViewModel.QuantityInput = quantity.ToString(CultureInfo.CurrentCulture);

        await InvokeAsync(fixture.ViewModel, "SaveLineAsync");

        fixture.Quotes.Verify(service => service.SaveQuoteItemAsync(42, It.Is<QuoteItemDTO>(item => item.Quantity == quantity),
            It.IsAny<CancellationToken>()), valid ? Times.Once() : Times.Never());
        Assert.Equal(!valid, fixture.ViewModel.HasError);
        if (!valid) Assert.Equal(quantity, fixture.ViewModel.QuantityValue);
    }

    [Fact]
    public async Task BeginEdit_LoadsUnitsForExistingLineWithoutLoadingProductCatalog()
    {
        var fixture = new Fixture(new QuoteItemDTO(11, 81, 7, 2m, 90m, false));
        fixture.Quotes.Setup(service => service.GetQuantityUnitsAsync(1, 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new UnitOfMeasureDTO(9, "UN", "Unidade", false, true) });
        fixture.Products.Setup(service => service.GetProductByIdAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Product(7, ProductType.Good) with { StockUnitID = 9 });
        await fixture.ViewModel.InitializeAsync();
        fixture.ViewModel.SelectedItem = Assert.Single(fixture.ViewModel.Items);

        await fixture.ViewModel.BeginEditAsync();
        fixture.ViewModel.SelectedLine = Assert.Single(fixture.ViewModel.AllLines);

        Assert.Equal("N0", fixture.ViewModel.QuantityFormat);
        Assert.Equal("UN", fixture.ViewModel.QuantityUnitLabel);
        fixture.Products.Verify(service => service.GetProductByIdAsync(7, It.IsAny<CancellationToken>()), Times.Once);
        fixture.Products.VerifyNoOtherCalls();
        fixture.Quotes.Verify(service => service.GetQuantityUnitsAsync(1, 100, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task BeginNew_LoadsAllUnitPagesAndRefreshesRulesOnNextOpening()
    {
        var fixture = new Fixture();
        fixture.Quotes.Setup(service => service.GetQuantityUnitsAsync(1, 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Range(1, 100).Select(id => new UnitOfMeasureDTO(id, "UN", "Unidade", false, true)).ToArray());
        fixture.Quotes.Setup(service => service.GetQuantityUnitsAsync(2, 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new UnitOfMeasureDTO(101, "M", "Metro", true, true) });
        await fixture.ViewModel.BeginNewAsync();
        fixture.ViewModel.SelectedProduct = new QuoteProductOption(Product(7, ProductType.Good) with { StockUnitID = 101 }, "Product");

        Assert.Equal("M", fixture.ViewModel.QuantityUnitLabel);
        Assert.Equal("N3", fixture.ViewModel.QuantityFormat);
        fixture.Quotes.Verify(service => service.GetQuantityUnitsAsync(2, 100, It.IsAny<CancellationToken>()), Times.Once);

        fixture.Quotes.Setup(service => service.GetQuantityUnitsAsync(1, 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new UnitOfMeasureDTO(101, "M", "Metro", false, true) });
        await fixture.ViewModel.BeginNewAsync();
        fixture.ViewModel.SelectedProduct = new QuoteProductOption(Product(7, ProductType.Good) with { StockUnitID = 101 }, "Product");

        Assert.Equal("N0", fixture.ViewModel.QuantityFormat);
        fixture.Products.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BeginNew_LoadsQuoteUnitsWithCreatePermissionRegardlessOfLegacyUnitService(bool injectLegacyService)
    {
        var fixture = new Fixture();
        fixture.Quotes.Setup(service => service.GetQuantityUnitsAsync(1, 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new UnitOfMeasureDTO(9, "M", "Metro", true, true) });
        fixture.Units.Setup(service => service.GetAllAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException());
        var authorization = new Mock<IAuthorizationService>();
        authorization.Setup(service => service.HasPermission(It.IsAny<string>()))
            .Returns((string permission) => permission == DecorPermissions.QuotesCreate);
        var employees = new Mock<IEmployeeService>(MockBehavior.Strict);
        employees.Setup(service => service.SearchEmployeesAsync(It.IsAny<string>(), 1, 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<EmployeeDTO>());
        var viewModel = new QuotesViewModel(fixture.Quotes.Object, authorization.Object, Mock.Of<ICustomerService>(),
            employees.Object, Mock.Of<IPartnerService>(), fixture.Products.Object, Mock.Of<IAuthenticatedUserContext>(),
            fixture.Orders.Object, injectLegacyService ? fixture.Units.Object : null);

        await viewModel.BeginNewAsync();
        viewModel.SelectedProduct = new QuoteProductOption(Product(7, ProductType.Good) with { StockUnitID = 9 }, "Product");

        Assert.False(authorization.Object.HasPermission(DecorPermissions.UnitsOfMeasureView));
        Assert.False(viewModel.HasError);
        Assert.True(viewModel.IsEditing);
        Assert.Equal(42, viewModel.CurrentQuoteId);
        Assert.Equal("M", viewModel.QuantityUnitLabel);
        Assert.Equal("N3", viewModel.QuantityFormat);
        fixture.Quotes.Verify(service => service.GetQuantityUnitsAsync(1, 100, It.IsAny<CancellationToken>()), Times.Once);
        fixture.Quotes.Verify(service => service.CreateOpenQuoteAsync(null, null, It.IsAny<CancellationToken>()), Times.Once);
        fixture.Units.VerifyNoOtherCalls();
    }

    private static Task InvokeAsync(QuotesViewModel viewModel, string methodName) =>
        (Task)typeof(QuotesViewModel).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(viewModel, null)!;

    private static ServiceDTO Service(int id) => new(id, "Service", true, null, 100m, 15m, null);

    [Fact]
    public async Task RuntimeDependencyInjection_RegistersAndSuppliesIndependentCatalogToOptionalConstructors()
    {
        var fixture = new Fixture();
        var services = new ServiceCollection();
        services.AddApplicationServices();
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IServiceCatalogService)
            && descriptor.ImplementationType == typeof(ServiceCatalogService));
        services.AddSingleton(fixture.Services.Object);
        services.AddSingleton(fixture.Products.Object);
        services.AddSingleton(fixture.Quotes.Object);
        services.AddSingleton(fixture.Orders.Object);
        services.AddSingleton(fixture.Units.Object);
        services.AddSingleton(fixture.Authorization.Object);
        services.AddSingleton(Mock.Of<ICustomerService>());
        var employees = new Mock<IEmployeeService>();
        employees.Setup(service => service.SearchEmployeesAsync(It.IsAny<string>(), 1, 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<EmployeeDTO>());
        services.AddSingleton(employees.Object);
        services.AddSingleton(Mock.Of<IPartnerService>());
        services.AddSingleton(Mock.Of<IAuthenticatedUserContext>());
        services.AddTransient<QuotesViewModel>();
        fixture.Services.Setup(service => service.SearchServicesAsync("", 1, 200, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Service(8) });
        using var provider = services.BuildServiceProvider();
        var quote = provider.GetRequiredService<QuotesViewModel>();

        await quote.BeginNewAsync();
        quote.CatalogTabIndex = 1;
        await SearchAsync(quote, "Service");
        Assert.Equal(8, Assert.Single(quote.CatalogServices).ServiceID);
        quote.ApplyLookupSelection(LookupSearchContext.Service, Service(8));
        Assert.Equal(8, quote.SelectedProduct?.ServiceID);
        Assert.False(quote.HasError);
        fixture.Products.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(10)]
    [InlineData(25)]
    [InlineData(50)]
    [InlineData(100)]
    public async Task ServiceCatalogPagination_PreservesAdapterPageSizeAndLookahead(int pageSize)
    {
        var fixture = new Fixture();
        var services = Enumerable.Range(1, pageSize + 1).Select(Service).ToArray();
        fixture.Services.Setup(service => service.SearchServicesAsync("paged", It.IsAny<int>(), pageSize, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string text, int page, int size, CancellationToken token) => services.Skip((page - 1) * size).Take(size));
        await fixture.ViewModel.BeginNewAsync();
        fixture.ViewModel.CatalogPagination.SelectedPageSize = pageSize;
        fixture.ViewModel.CatalogTabIndex = 1;

        await SearchAsync(fixture.ViewModel, "paged");

        Assert.Equal(pageSize, fixture.ViewModel.CatalogServices.Count);
        Assert.Empty(fixture.ViewModel.CatalogProducts);
        Assert.True(fixture.ViewModel.CatalogPagination.HasNextPage);
        fixture.Services.Verify(service => service.SearchServicesAsync("paged", 2, pageSize, It.IsAny<CancellationToken>()), Times.Once);
        fixture.ViewModel.CatalogPagination.NextPageCommand.Execute(null);
        await WaitUntilAsync(() => fixture.ViewModel.CatalogPageDisplay == "Página 2" && !fixture.ViewModel.IsCatalogBusy);
        var last = Assert.Single(fixture.ViewModel.CatalogServices);
        Assert.Equal(pageSize + 1, last.Code);
        Assert.Equal(pageSize + 1, last.ServiceID);
        Assert.Null(last.ProductID);
        Assert.Null(last.DTO);
        Assert.Equal(100m, last.Price);
        Assert.False(fixture.ViewModel.CatalogPagination.HasNextPage);
        Assert.True(fixture.ViewModel.CatalogPagination.HasPreviousPage);
        fixture.Products.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SwitchingToServices_CancelsProductRequestAndIgnoresItsLateResult(bool failOlder)
    {
        var fixture = new Fixture();
        var pending = new TaskCompletionSource<IEnumerable<ProductDTO>>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken requestToken = default;
        fixture.Products.Setup(service => service.SearchProductsAsync("mixed tipo:produto", 1, 25, It.IsAny<CancellationToken>()))
            .Returns((string query, int page, int size, CancellationToken token) => { requestToken = token; return pending.Task; });
        fixture.Services.Setup(service => service.SearchServicesAsync("mixed", 1, 25, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Service(7) });
        await fixture.ViewModel.BeginNewAsync();
        fixture.ViewModel.ProductSearchText = "mixed";
        var oldSearch = InvokeAsync(fixture.ViewModel, "SearchProductsAsync");

        fixture.ViewModel.CatalogTabIndex = 1;
        await WaitUntilAsync(() => fixture.ViewModel.CatalogServices.Count == 1 && !fixture.ViewModel.IsCatalogBusy);
        if (failOlder) pending.SetException(new InvalidOperationException("Old product failure"));
        else pending.SetResult([Product(7, ProductType.Good)]);
        await oldSearch;

        Assert.True(requestToken.IsCancellationRequested);
        Assert.Empty(fixture.ViewModel.CatalogProducts);
        Assert.Equal(7, Assert.Single(fixture.ViewModel.CatalogServices).ServiceID);
        Assert.False(fixture.ViewModel.HasError);
        Assert.False(fixture.ViewModel.IsCatalogBusy);
    }

    [Fact]
    public async Task ServiceCatalog_FiltersInactiveRowsWithoutLosingNextPage()
    {
        var fixture = new Fixture();
        fixture.Services.Setup(service => service.SearchServicesAsync("inactive", 1, 25, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Range(1, 25).Select(id => Service(id) with { IsActive = false }));
        fixture.Services.Setup(service => service.SearchServicesAsync("inactive", 2, 25, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Service(26) });
        await fixture.ViewModel.BeginNewAsync();
        fixture.ViewModel.CatalogTabIndex = 1;

        await SearchAsync(fixture.ViewModel, "inactive");

        Assert.Empty(fixture.ViewModel.CatalogServices);
        Assert.True(fixture.ViewModel.HasCatalogNext);
        fixture.ViewModel.CatalogNextCommand.Execute(null);
        await WaitUntilAsync(() => fixture.ViewModel.CatalogPageDisplay == "Página 2" && !fixture.ViewModel.IsCatalogBusy);
        Assert.Equal(26, Assert.Single(fixture.ViewModel.CatalogServices).ServiceID);
        fixture.Products.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("1.125", true)]
    [InlineData("1.1251", false)]
    public async Task ServiceLookupSelection_SavesOnlyServiceReferenceWithExistingQuantityPrecision(string quantityText, bool valid)
    {
        var fixture = new Fixture();
        await fixture.ViewModel.BeginNewAsync();
        fixture.ViewModel.ApplyLookupSelection(LookupSearchContext.Service, Service(7));
        var selected = fixture.ViewModel.SelectedProduct!;
        Assert.Null(selected.DTO);
        Assert.Null(selected.ProductID);
        Assert.Equal(7, selected.ServiceID);
        Assert.Equal(100m, fixture.ViewModel.UnitPriceValue);
        fixture.ViewModel.QuantityValue = decimal.Parse(quantityText, CultureInfo.InvariantCulture);

        await InvokeAsync(fixture.ViewModel, "SaveLineAsync");

        fixture.Quotes.Verify(service => service.SaveQuoteItemAsync(42,
            It.Is<QuoteItemDTO>(item => item.ProductID == null && item.ServiceID == 7), It.IsAny<CancellationToken>()),
            valid ? Times.Once() : Times.Never());
        Assert.Equal(!valid, fixture.ViewModel.HasError);
        fixture.Products.VerifyNoOtherCalls();
        fixture.Services.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ServiceSearchWithoutOptionalCatalog_ReportsErrorWithoutProductFallback()
    {
        var fixture = new Fixture();
        var employees = new Mock<IEmployeeService>();
        employees.Setup(service => service.SearchEmployeesAsync(It.IsAny<string>(), 1, 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<EmployeeDTO>());
        var viewModel = new QuotesViewModel(fixture.Quotes.Object, fixture.Authorization.Object, Mock.Of<ICustomerService>(),
            employees.Object, Mock.Of<IPartnerService>(), fixture.Products.Object, Mock.Of<IAuthenticatedUserContext>());
        await viewModel.BeginNewAsync();
        viewModel.CatalogTabIndex = 1;

        await SearchAsync(viewModel, "Service");

        Assert.True(viewModel.HasError);
        Assert.False(viewModel.IsCatalogBusy);
        Assert.Empty(viewModel.CatalogServices);
        fixture.Products.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ExistingServiceAndProductWithSameCode_LoadSeparatelyAndSaveServiceReference()
    {
        var fixture = new Fixture(new QuoteItemDTO(11, 81, 7, 2m, 90m, false),
            new QuoteItemDTO(12, 81, null, 1.125m, 80m, false, ServiceID: 7));
        await fixture.ViewModel.BeginNewAsync();

        fixture.Products.Verify(service => service.GetProductByIdAsync(7, It.IsAny<CancellationToken>()), Times.Once);
        fixture.Services.Verify(service => service.GetServiceByIdAsync(7, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(new[] { "Product", "Service" }, fixture.ViewModel.AllLines.Select(line => line.ProductName));
        Assert.Equal(new[] { "Produto", "Serviço" }, fixture.ViewModel.AllLines.Select(line => line.Category));
        Assert.Equal(new[] { "Produto", "Serviço" }, fixture.ViewModel.SectionItems.Select(line => line.Category));
        fixture.ViewModel.SelectedLine = fixture.ViewModel.AllLines[1];
        Assert.Null(fixture.ViewModel.SelectedProduct!.ProductID);
        Assert.Equal(7, fixture.ViewModel.SelectedProduct.ServiceID);
        Assert.Null(fixture.ViewModel.SelectedProduct.DTO);
        Assert.Equal("N3", fixture.ViewModel.QuantityFormat);
        Assert.Empty(fixture.ViewModel.QuantityUnitLabel);
        Assert.Equal(1.125m, fixture.ViewModel.QuantityValue);
        Assert.Equal(80m, fixture.ViewModel.UnitPriceValue);

        await InvokeAsync(fixture.ViewModel, "SaveLineAsync");

        fixture.Quotes.Verify(service => service.SaveQuoteItemAsync(42,
            It.Is<QuoteItemDTO>(item => item.QuoteItemID == 12 && item.ProductID == null && item.ServiceID == 7
                && item.Quantity == 1.125m && item.UnitPrice == 80m), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal("Serviço", Assert.Single(fixture.ViewModel.AllLines).Category);
        Assert.False(fixture.ViewModel.HasError);
    }

    [Fact]
    public async Task ChangingLineFromProductToSameCodeService_StartsNewLineAndUsesCatalogPrice()
    {
        var fixture = new Fixture(new QuoteItemDTO(11, 81, 7, 2m, 90m, true));
        fixture.Services.Setup(service => service.SearchServicesAsync("Service", 1, 25, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Service(7) });
        await fixture.ViewModel.BeginNewAsync();
        fixture.ViewModel.CatalogTabIndex = 1;
        await SearchAsync(fixture.ViewModel, "Service");
        fixture.ViewModel.SelectedLine = Assert.Single(fixture.ViewModel.AllLines);
        fixture.ViewModel.SelectedProduct = Assert.Single(fixture.ViewModel.CatalogServices);

        Assert.Null(fixture.ViewModel.SelectedLine);
        Assert.Equal(1m, fixture.ViewModel.QuantityValue);
        Assert.Equal(100m, fixture.ViewModel.UnitPriceValue);
        await InvokeAsync(fixture.ViewModel, "SaveLineAsync");

        fixture.Quotes.Verify(service => service.SaveQuoteItemAsync(42,
            It.Is<QuoteItemDTO>(item => item.QuoteItemID == 0 && item.ProductID == null && item.ServiceID == 7),
            It.IsAny<CancellationToken>()), Times.Once);
        fixture.Products.Verify(service => service.SearchProductsAsync(It.IsAny<string>(), It.IsAny<int>(),
            It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.False(fixture.ViewModel.HasError);
    }

    private static ProductDTO Product(int id, ProductType type) => new(
        ProductID: id, Barcode: null, IsActive: true, Description: type == ProductType.Service ? "Service" : "Product",
        StockQuantity: 0m, BrandID: null, BrandName: null, SubgroupID: null, SubgroupName: null,
        GroupID: null, GroupName: null, FamilyID: null, FamilyName: null, ClassID: null, ClassName: null,
        ManufacturerRef: null, AuxiliarRef: null, Dimensions: null, Observations: null, MinimumStock: 0m,
        ProductType: (int)type, CostPrice: null, SalePrice: 100m, EmployeeCommissionValue: null,
        DefaultInstallationServiceID: null, StockUnitID: null);

    private sealed class Fixture
    {
        public Mock<IQuoteService> Quotes { get; } = new(MockBehavior.Strict);
        public Mock<IProductService> Products { get; } = new(MockBehavior.Strict);
        public Mock<IServiceCatalogService> Services { get; } = new(MockBehavior.Strict);
        public Mock<IOrderService> Orders { get; } = new(MockBehavior.Strict);
        public Mock<IUnitOfMeasureService> Units { get; } = new(MockBehavior.Strict);
        public Mock<IAuthorizationService> Authorization { get; } = new();
        public QuotesViewModel ViewModel { get; }
        private QuoteDTO _quote;

        public Fixture(params QuoteItemDTO[] items)
        {
            var createdAt = new DateTime(2026, 10, 6, 12, 0, 0, DateTimeKind.Utc);
            _quote = new QuoteDTO(42, 0, 0, null, (int)QuoteSourceType.DirectCapture, createdAt, null,
                [new QuoteSectionDTO(81, 42, (int)QuoteSectionType.Catalog, (int)QuoteSectionStatus.Draft,
                    null, null, createdAt, items)]);
            Quotes.Setup(service => service.CreateOpenQuoteAsync(null, null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => _quote);
            Quotes.Setup(service => service.GetQuoteByIdAsync(42, It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => _quote);
            Quotes.Setup(service => service.DeleteQuoteItemAsync(42, It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .Callback((int quoteId, int itemId, CancellationToken token) =>
                {
                    _quote = _quote with
                    {
                        Sections = _quote.Sections!.Select(section => section with
                        {
                            Items = section.Items!.Where(item => item.QuoteItemID != itemId).ToArray()
                        }).ToArray()
                    };
                })
                .Returns(Task.CompletedTask);
            Quotes.Setup(service => service.SearchQuotesAsync(It.IsAny<string>(), 1, 500, It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new[] { _quote });
            Quotes.Setup(service => service.SaveQuoteItemAsync(42, It.IsAny<QuoteItemDTO>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((int quoteId, QuoteItemDTO item, CancellationToken cancellationToken) =>
                {
                    var persisted = item with { QuoteItemID = item.QuoteItemID == 0 ? 11 : item.QuoteItemID };
                    var section = _quote.Sections!.Single();
                    _quote = _quote with { Sections = [section with { Items = [persisted] }] };
                    return persisted;
                });
            Quotes.Setup(service => service.SaveQuoteAsync(It.IsAny<QuoteDTO>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((QuoteDTO quote, CancellationToken cancellationToken) =>
                {
                    _quote = quote with { Sections = _quote.Sections };
                    return quote.QuoteID;
                });
            Quotes.Setup(service => service.CancelQuoteAsync(42, It.IsAny<CancellationToken>()))
                .Returns(() =>
                {
                    _quote = _quote with { Sections = _quote.Sections!.Select(section => section with { Status = (int)QuoteSectionStatus.Rejected }).ToArray() };
                    return Task.CompletedTask;
                });
            Quotes.Setup(service => service.UpdateSectionStatusAsync(42, 81, It.IsAny<QuoteSectionStatus>(), It.IsAny<CancellationToken>()))
                .Returns((int quoteId, int sectionId, QuoteSectionStatus status, CancellationToken token) =>
                {
                    _quote = _quote with { Sections = _quote.Sections!.Select(section => section with { Status = (int)status }).ToArray() };
                    return Task.CompletedTask;
                });
            Orders.Setup(service => service.ConvertFromQuoteAsync(81, It.IsAny<CancellationToken>()))
                .ReturnsAsync(() =>
                {
                    _quote = _quote with { Sections = _quote.Sections!.Select(section => section with
                    {
                        Status = (int)QuoteSectionStatus.ConvertedToOrder,
                        OrderID = 99
                    }).ToArray() };
                    return new OrderDTO(99, 81, 10, 1, 1, false, null, null, createdAt);
                });
            Products.Setup(service => service.GetProductByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((int id, CancellationToken token) => Product(id, ProductType.Good) with { SubgroupName = "Subgroup" });
            Products.Setup(service => service.SearchProductsAsync("Product tipo:produto", 1, 25, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { Product(7, ProductType.Good) });
            Services.Setup(service => service.SearchServicesAsync("Service", 1, 25, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { Service(8) });
            Services.Setup(service => service.GetServiceByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((int id, CancellationToken token) => Service(id));
            var employees = new Mock<IEmployeeService>(MockBehavior.Strict);
            employees.Setup(service => service.SearchEmployeesAsync(It.IsAny<string>(), 1, 100, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<EmployeeDTO>());
            Authorization.Setup(service => service.HasPermission(It.IsAny<string>())).Returns(true);
            Quotes.Setup(service => service.GetQuantityUnitsAsync(1, 100, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<UnitOfMeasureDTO>());
            ViewModel = new QuotesViewModel(Quotes.Object, Authorization.Object, Mock.Of<ICustomerService>(),
                employees.Object, Mock.Of<IPartnerService>(), Products.Object, Mock.Of<IAuthenticatedUserContext>(), Orders.Object, Units.Object, Services.Object);
        }
    }
}