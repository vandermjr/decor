using Decor.AvaloniaUI.ViewModels;
using Decor.Core.DTOs;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Services;
using Moq;

namespace Decor.Application.Tests;

public sealed class ContextualSearchPaginationTests
{
    [Theory]
    [InlineData(LookupSearchContext.Customer, "Cliente")]
    [InlineData(LookupSearchContext.Employee, "Vendedor")]
    [InlineData(LookupSearchContext.Partner, "Parceiro")]
    [InlineData(LookupSearchContext.Product, "Produto")]
    [InlineData(LookupSearchContext.Service, "Serviço")]
    public async Task Initialization_loads_all_pages_and_preserves_projections(LookupSearchContext context, string category)
    {
        var fixture = new Fixture();
        var calls = new List<(string Text, int Page, int Size, CancellationToken Token)>();
        Task<IEnumerable<T>> Page<T>(string text, int page, int size, CancellationToken token, Func<int, T> create)
        {
            calls.Add((text, page, size, token));
            return Task.FromResult(Enumerable.Range(1, 451).Skip((page - 1) * size).Take(size).Select(create));
        }

        fixture.Customers.Setup(service => service.SearchCustomersAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns((string text, int page, int size, CancellationToken token) => Page(text, page, size, token, Customer));
        fixture.Employees.Setup(service => service.SearchEmployeesAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns((string text, int page, int size, CancellationToken token) => Page(text, page, size, token,
                code => new EmployeeDTO(code, $"Employee {code}", "Sales", null, null, "document", null, true, null)));
        fixture.Partners.Setup(service => service.SearchPartnersAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns((string text, int page, int size, CancellationToken token) => Page(text, page, size, token,
                code => new PartnerDTO(code, $"Partner {code}", "document", null, default(PartnerType), true)));
        fixture.Products.Setup(service => service.SearchProductsAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns((string text, int page, int size, CancellationToken token) => Page(text, page, size, token, Product));
        fixture.Services.Setup(service => service.SearchServicesAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns((string text, int page, int size, CancellationToken token) => Page(text, page, size, token, Service));
        fixture.ViewModel.SearchText = "reset me";

        await fixture.ViewModel.InitializeAsync(context);

        Assert.Equal(new[] { 1, 2, 3 }, calls.Select(call => call.Page));
        Assert.All(calls, call =>
        {
            Assert.Equal(string.Empty, call.Text);
            Assert.Equal(200, call.Size);
            Assert.True(call.Token.CanBeCanceled);
        });
        Assert.Equal(451, fixture.ViewModel.Results.Count);
        Assert.Equal(Enumerable.Range(1, 451), fixture.ViewModel.Results.Select(item => item.Code));
        Assert.Equal("451 resultados", fixture.ViewModel.ResultCount);
        var last = fixture.ViewModel.Results.Last();
        Assert.Equal(category, last.Category);
        Assert.Equal(context switch
        {
            LookupSearchContext.Customer => "Customer 451",
            LookupSearchContext.Employee => "Employee 451",
            LookupSearchContext.Partner => "Partner 451",
            LookupSearchContext.Service => "Service 451",
            _ => "Product 451"
        }, last.Name);
        Assert.Equal(context switch
        {
            LookupSearchContext.Customer => "document · phone",
            LookupSearchContext.Employee => "Sales",
            LookupSearchContext.Partner => "document",
            LookupSearchContext.Service => 12m.ToString("C2"),
            _ => string.Join(" · ", "barcode", 12m.ToString("C2"))
        }, last.Detail);
        Assert.Equal(context switch
        {
            LookupSearchContext.Customer => typeof(CustomerDTO),
            LookupSearchContext.Employee => typeof(EmployeeDTO),
            LookupSearchContext.Partner => typeof(PartnerDTO),
            LookupSearchContext.Service => typeof(ServiceDTO),
            _ => typeof(ProductDTO)
        }, last.Value.GetType());
        Assert.Equal(category, fixture.ViewModel.Results[0].Category);
        Assert.Null(fixture.ViewModel.SelectedItem);
        Assert.False(fixture.ViewModel.IsBusy);
        Assert.False(fixture.ViewModel.HasError);
    }

    [Fact]
    public async Task Full_inactive_page_does_not_end_paging_and_exact_multiple_requests_empty_page()
    {
        var fixture = new Fixture();
        fixture.Customers.Setup(service => service.SearchCustomersAsync("", It.IsAny<int>(), 200, It.IsAny<CancellationToken>()))
            .Returns((string text, int page, int size, CancellationToken token) => Task.FromResult<IEnumerable<CustomerDTO>>(page switch
            {
                1 => Enumerable.Range(1, 200).Select(code => Customer(code) with { IsActive = false }),
                2 => Enumerable.Range(201, 200).Select(Customer),
                _ => []
            }));

        await fixture.ViewModel.InitializeAsync(LookupSearchContext.Customer);

        Assert.Equal(Enumerable.Range(201, 200), fixture.ViewModel.Results.Select(item => item.Code));
        fixture.Customers.Verify(service => service.SearchCustomersAsync("", 3, 200, It.IsAny<CancellationToken>()), Times.Once);
        fixture.Customers.Verify(service => service.SearchCustomersAsync("", 4, 200, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Reinitialization_cancels_pending_page_and_ignores_stale_completion(bool staleFailure)
    {
        var fixture = new Fixture();
        var pending = new TaskCompletionSource<IEnumerable<CustomerDTO>>();
        CancellationToken oldToken = default;
        fixture.Customers.Setup(service => service.SearchCustomersAsync("", 1, 200, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Range(1, 200).Select(Customer));
        fixture.Customers.Setup(service => service.SearchCustomersAsync("", 2, 200, It.IsAny<CancellationToken>()))
            .Callback((string text, int page, int size, CancellationToken token) => oldToken = token)
            .Returns(pending.Task);
        fixture.Partners.Setup(service => service.SearchPartnersAsync("", 1, 200, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new PartnerDTO(999, "New partner", null, null, default, true) });
        var oldSearch = fixture.ViewModel.InitializeAsync(LookupSearchContext.Customer);
        fixture.ViewModel.SearchText = "reset me";
        fixture.ViewModel.SelectedItem = new LookupSearchItem(1, "Old", "", "Cliente", Customer(1));

        await fixture.ViewModel.InitializeAsync(LookupSearchContext.Partner);

        Assert.True(oldToken.IsCancellationRequested);
        Assert.Equal("", fixture.ViewModel.SearchText);
        Assert.Equal("Pesquisar parceiro", fixture.ViewModel.Title);
        Assert.Null(fixture.ViewModel.SelectedItem);
        if (staleFailure) pending.SetException(new UnauthorizedAccessException());
        else pending.SetResult(new[] { Customer(201) });
        await oldSearch;

        Assert.Equal(999, Assert.Single(fixture.ViewModel.Results).Code);
        Assert.Equal("1 resultado", fixture.ViewModel.ResultCount);
        Assert.False(fixture.ViewModel.HasError);
        Assert.False(fixture.ViewModel.IsBusy);
        fixture.Customers.Verify(service => service.SearchCustomersAsync("", 3, 200, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Canceled_search_cannot_clear_busy_state_of_new_search_and_query_is_snapshotted()
    {
        var fixture = new Fixture();
        fixture.Customers.Setup(service => service.SearchCustomersAsync("", 1, 200, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Customer(1) });
        await fixture.ViewModel.InitializeAsync(LookupSearchContext.Customer);
        var oldPage = new TaskCompletionSource<IEnumerable<CustomerDTO>>();
        var newPage = new TaskCompletionSource<IEnumerable<CustomerDTO>>();
        CancellationToken oldToken = default;
        fixture.Customers.Setup(service => service.SearchCustomersAsync("old", 1, 200, It.IsAny<CancellationToken>()))
            .Callback((string text, int page, int size, CancellationToken token) => oldToken = token)
            .Returns(oldPage.Task);
        fixture.Customers.Setup(service => service.SearchCustomersAsync("new", 1, 200, It.IsAny<CancellationToken>()))
            .Returns(newPage.Task);
        fixture.Customers.Setup(service => service.SearchCustomersAsync("new", 2, 200, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Customer(201) });
        fixture.ViewModel.SearchText = "old";
        var oldSearch = fixture.ViewModel.SearchAsync();
        fixture.ViewModel.SearchText = "new";
        var newSearch = fixture.ViewModel.SearchAsync();

        Assert.True(oldToken.IsCancellationRequested);
        oldPage.SetCanceled(oldToken);
        await oldSearch;
        Assert.True(fixture.ViewModel.IsBusy);
        Assert.False(fixture.ViewModel.SearchCommand.CanExecute(null));
        fixture.ViewModel.SearchText = "changed while fetching";
        newPage.SetResult(Enumerable.Range(1, 200).Select(Customer));
        await newSearch;

        Assert.Equal(201, fixture.ViewModel.Results.Count);
        Assert.False(fixture.ViewModel.IsBusy);
        Assert.True(fixture.ViewModel.SearchCommand.CanExecute(null));
        Assert.False(fixture.ViewModel.HasError);
        fixture.Customers.Verify(service => service.SearchCustomersAsync("new", 2, 200, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Current_failure_clears_results_selection_and_notifies_count(bool unauthorized)
    {
        var fixture = new Fixture();
        fixture.Customers.Setup(service => service.SearchCustomersAsync("", 1, 200, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Customer(1) });
        await fixture.ViewModel.InitializeAsync(LookupSearchContext.Customer);
        fixture.ViewModel.SelectedItem = fixture.ViewModel.Results[0];
        var properties = new List<string?>();
        fixture.ViewModel.PropertyChanged += (_, args) => properties.Add(args.PropertyName);
        fixture.Customers.Setup(service => service.SearchCustomersAsync("fail", 1, 200, It.IsAny<CancellationToken>()))
            .ThrowsAsync(unauthorized ? new UnauthorizedAccessException() : new InvalidOperationException());
        fixture.ViewModel.SearchText = "fail";

        await fixture.ViewModel.SearchAsync();

        Assert.Empty(fixture.ViewModel.Results);
        Assert.Null(fixture.ViewModel.SelectedItem);
        Assert.Contains(nameof(ContextualSearchViewModel.ResultCount), properties);
        Assert.Equal(unauthorized
            ? "Você não tem permissão para pesquisar este tipo de registro."
            : "Não foi possível carregar os resultados da pesquisa.", fixture.ViewModel.ErrorMessage);
        Assert.False(fixture.ViewModel.IsBusy);
    }

    private static CustomerDTO Customer(int code) => new(code, $"Customer {code}", "document", "phone", null, null, true);

    private static ProductDTO Product(int code) => new(code, "barcode", true, $"Product {code}", 0,
        null, null, null, null, null, null, null, null, null, null, null, null, null, null, 0,
        1, null, 12m, null, null, null);

    private static ServiceDTO Service(int code) => new(code, $"Service {code}", true, null, 12m, null, null);

    [Fact]
    public async Task ProductContext_ExcludesLegacyServicesAndNeverQueriesIndependentCatalog()
    {
        var fixture = new Fixture();
        fixture.Products.Setup(service => service.SearchProductsAsync("", 1, 200, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Product(7), Product(8) with { ProductType = 2 } });

        await fixture.ViewModel.InitializeAsync(LookupSearchContext.Product);

        Assert.Equal("Pesquisar produto", fixture.ViewModel.Title);
        Assert.Equal(7, Assert.Single(fixture.ViewModel.Results).Code);
        Assert.Equal("Produto", fixture.ViewModel.Results[0].Category);
        fixture.Services.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ServiceContext_OnlyQueriesIndependentCatalogAndFiltersInactiveServices()
    {
        var fixture = new Fixture();
        fixture.Services.Setup(service => service.SearchServicesAsync("", 1, 200, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Service(7), Service(8) with { IsActive = false } });

        await fixture.ViewModel.InitializeAsync(LookupSearchContext.Service);

        Assert.Equal("Pesquisar serviço", fixture.ViewModel.Title);
        var selected = Assert.Single(fixture.ViewModel.Results);
        Assert.Equal(7, selected.Code);
        Assert.IsType<ServiceDTO>(selected.Value);
        Assert.Equal("Serviço", selected.Category);
        fixture.Products.VerifyNoOtherCalls();
    }

    private sealed class Fixture
    {
        public Mock<ICustomerService> Customers { get; } = new(MockBehavior.Strict);
        public Mock<IEmployeeService> Employees { get; } = new(MockBehavior.Strict);
        public Mock<IPartnerService> Partners { get; } = new(MockBehavior.Strict);
        public Mock<IProductService> Products { get; } = new(MockBehavior.Strict);
        public Mock<IServiceCatalogService> Services { get; } = new(MockBehavior.Strict);
        public ContextualSearchViewModel ViewModel { get; }

        public Fixture() => ViewModel = new(Customers.Object, Employees.Object, Partners.Object, Products.Object, Services.Object);
    }
}