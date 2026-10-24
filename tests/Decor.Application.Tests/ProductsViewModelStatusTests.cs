using Decor.AvaloniaUI.ViewModels;
using Decor.Core.DTOs;
using Decor.Core.Interfaces.Services;
using FluentAssertions;
using static Decor.Application.Tests.RegistrationStatusTestSupport;

namespace Decor.Application.Tests;

public sealed class ProductsViewModelStatusTests
{
    private static ProductDTO Product(int id = 42) => new(id, "789", true, "Produto", 3m, 1, "Marca", 1, "Subgrupo", 1, "Grupo", 1, "Familia", 1, "Classe", "Ref", null, null, null, 1m, 0, null, null, null, null, null);
    private static ProductsViewModel Create(ProductService? products = null, IClassService? classService = null, IFamilyService? familyService = null, IGroupService? groupService = null, ISubgroupService? subgroupService = null, BrandsViewModelStatusTests.BrandService? brands = null, IAuthorizationService? authorization = null)
    {
        var classification = classService ?? new ClassificationService();
        return new(products ?? new ProductService(), brands ?? new BrandsViewModelStatusTests.BrandService(), classification,
            familyService ?? (IFamilyService)classification, groupService ?? (IGroupService)classification,
            subgroupService ?? (ISubgroupService)classification, authorization);
    }

    [Fact]
    public void Observations_counter_updates_and_respects_utf8_capacity()
    {
        var viewModel = Create();
        viewModel.ObservationsCounter.Should().Be("0/65535 caracteres");
        var notifications = new List<string?>();
        viewModel.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);
        viewModel.Observations = "abc";
        viewModel.ObservationsCounter.Should().Be("3/65535 caracteres");
        notifications.Should().Contain(nameof(ProductsViewModel.ObservationsCounter));
        viewModel.Observations = new string('a', 70000);
        viewModel.Observations!.Length.Should().Be(65535);
    }

    [Fact]
    public void Selected_product_details_update_without_retaining_previous_values()
    {
        var viewModel = Create();
        viewModel.SelectedProduct = Product() with
        {
            SalePrice = 1234.56m,
            CostPrice = 500m,
            CurrentStockQuantity = 10m,
            ReservedStockQuantity = 5m,
            MinimumStock = 10m
        };

        viewModel.HasSelectedProduct.Should().BeTrue();
        viewModel.SelectedSalePriceDisplay.Should().Contain("1.234,56");
        viewModel.SelectedAvailableStockDisplay.Should().Contain("5,00");
        viewModel.SelectedStockLevelValue.Should().Be(50d);
        viewModel.HasSelectedCostPrice.Should().BeTrue();
        viewModel.IsSelectedStockWarning.Should().BeTrue();

        viewModel.SelectedProduct = Product(7) with
        {
            SalePrice = null,
            CostPrice = null,
            CurrentStockQuantity = 10m
        };

        viewModel.SelectedProductCodeDisplay.Should().Be("000007");
        viewModel.SelectedSalePriceDisplay.Should().Be("Não informado");
        viewModel.HasSelectedCostPrice.Should().BeFalse();
        viewModel.SelectedStockLevelValue.Should().Be(100d);
        viewModel.IsSelectedStockPositive.Should().BeTrue();

        viewModel.SelectedProduct = null;
        viewModel.HasSelectedProduct.Should().BeFalse();
        viewModel.SelectedProductCodeDisplay.Should().BeEmpty();
        viewModel.SelectedAvailableStockDisplay.Should().BeEmpty();
    }

    [Fact]
    public async Task Catalog_summary_is_loaded_only_when_requested()
    {
        var productService = new ProductService
        {
            Summary = () => Task.FromResult<ProductCatalogSummaryDTO?>(new(1250, 1180, 42, 18))
        };
        var viewModel = Create(productService);

        await viewModel.InitializeAsync();

        viewModel.CatalogSummary.Should().BeNull();
        await viewModel.EnsureCatalogSummaryAsync();

        viewModel.CatalogSummary.Should().Be(new ProductCatalogSummaryDTO(1250, 1180, 42, 18));
        viewModel.HasCatalogSummary.Should().BeTrue();
        viewModel.TotalProductsDisplay.Should().Be("1.250");
        viewModel.ActiveProductsDisplay.Should().Be("1.180");
        viewModel.InactiveProductsDisplay.Should().Be("70");
        viewModel.OutOfStockProductsDisplay.Should().Be("42");
        viewModel.LowStockProductsDisplay.Should().Be("18");
    }

    [Fact]
    public async Task Catalog_summary_is_cached_after_first_open_and_refreshes_on_demand()
    {
        var calls = 0;
        var viewModel = Create(new ProductService
        {
            Summary = () => Task.FromResult<ProductCatalogSummaryDTO?>(new(++calls, 1, 0, 0))
        });

        await viewModel.InitializeAsync();
        await viewModel.EnsureCatalogSummaryAsync();
        await viewModel.EnsureCatalogSummaryAsync();

        calls.Should().Be(1);
        await viewModel.RefreshCatalogSummaryAsync();

        calls.Should().Be(2);
        viewModel.TotalProductsDisplay.Should().Be("2");
    }

    [Fact]
    public async Task Catalog_indicator_filters_apply_the_matching_stock_or_activity_state()
    {
        var active = Product(1) with { IsActive = true, CurrentStockQuantity = 10m, ReservedStockQuantity = 0m, MinimumStock = 2m };
        var inactive = Product(2) with { IsActive = false, CurrentStockQuantity = 10m, ReservedStockQuantity = 0m, MinimumStock = 2m };
        var outOfStock = Product(3) with { CurrentStockQuantity = 0m, ReservedStockQuantity = 0m, MinimumStock = 2m };
        var lowStock = Product(4) with { CurrentStockQuantity = 3m, ReservedStockQuantity = 0m, MinimumStock = 5m };
        var viewModel = Create(new ProductService
        {
            Search = () => Task.FromResult<IEnumerable<ProductDTO>>([active, inactive, outOfStock, lowStock])
        });

        await viewModel.ShowCatalogIndicatorAsync("out-of-stock");
        viewModel.Products.Should().ContainSingle().Which.ProductID.Should().Be(3);
        viewModel.CatalogIndicatorFilterDisplay.Should().Be("Indicador: sem estoque");

        await viewModel.ShowCatalogIndicatorAsync("low-stock");
        viewModel.Products.Should().ContainSingle().Which.ProductID.Should().Be(4);

        await viewModel.ShowCatalogIndicatorAsync("inactive");
        viewModel.Products.Should().ContainSingle().Which.ProductID.Should().Be(2);

        await viewModel.ShowCatalogIndicatorAsync(null);
        viewModel.Products.Should().HaveCount(4);
        viewModel.HasCatalogIndicatorFilter.Should().BeFalse();
    }

    [Fact]
    public async Task Initialize_without_summary_result_hides_summary_cards()
    {
        var viewModel = Create(new ProductService { Summary = () => Task.FromResult<ProductCatalogSummaryDTO?>(null) });

        await viewModel.InitializeAsync();
        await viewModel.EnsureCatalogSummaryAsync();

        viewModel.CatalogSummary.Should().BeNull();
        viewModel.HasCatalogSummary.Should().BeFalse();
    }

    [Fact]
    public async Task Initialize_when_summary_service_fails_hides_summary_cards()
    {
        var viewModel = Create(new ProductService
        {
            Summary = () => Task.FromException<ProductCatalogSummaryDTO?>(new InvalidOperationException("Database unavailable."))
        });

        await viewModel.InitializeAsync();
        await viewModel.EnsureCatalogSummaryAsync();

        viewModel.CatalogSummary.Should().BeNull();
        viewModel.HasCatalogSummary.Should().BeFalse();
    }

    [Fact]
    public async Task Initialize_without_product_view_permission_does_not_load_summary()
    {
        var summaryCalls = 0;
        var viewModel = Create(new ProductService
        {
            Summary = () =>
            {
                summaryCalls++;
                return Task.FromResult<ProductCatalogSummaryDTO?>(new(10, 8, 1, 2));
            }
        }, authorization: new DeniedAuthorization());

        await viewModel.InitializeAsync();
        await viewModel.EnsureCatalogSummaryAsync();

        summaryCalls.Should().Be(0);
        viewModel.HasCatalogSummary.Should().BeFalse();
    }

    [Fact]
    public async Task Initialize_does_not_load_products_until_explicit_empty_search_and_clear_resets_listing()
    {
        var searchTerms = new List<string>();
        var productService = new ProductService
        {
            Search = () => Task.FromResult<IEnumerable<ProductDTO>>([Product()]),
            SearchRequested = searchTerms.Add
        };
        var viewModel = Create(productService);

        await viewModel.InitializeAsync();
        viewModel.Products.Should().BeEmpty();
        searchTerms.Should().BeEmpty();

        await ExecuteAsync(viewModel, viewModel.SearchCommand, () => viewModel.IsBusy);

        searchTerms.Should().ContainSingle().Which.Should().BeEmpty();
        viewModel.Products.Should().ContainSingle();

        viewModel.SearchText = "produto";
        viewModel.ClearSearchCommand.Execute(null);
        viewModel.SearchText.Should().BeEmpty();
        viewModel.Products.Should().BeEmpty();
        viewModel.HasSelectedProduct.Should().BeFalse();
    }

    [Fact]
    public async Task Classification_filter_includes_products_in_selected_descendant()
    {
        var firstProduct = Product() with { ProductID = 1, ClassID = 1, FamilyID = 1, GroupID = 1, SubgroupID = 1 };
        var otherProduct = Product(2) with { ClassID = 2, FamilyID = 2, GroupID = 2, SubgroupID = 2 };
        var service = new ProductService { Search = () => Task.FromResult<IEnumerable<ProductDTO>>([firstProduct, otherProduct]) };
        var tree = new TreeClassificationService();
        var viewModel = Create(service, tree, tree, tree, tree);

        await viewModel.EnsureClassificationTreeAsync();
        viewModel.FilteredClassificationTreeNodes.Select(node => node.Name)
            .Should().Contain(new[] { "Revestimentos", "Decoração" });
        Assert.All(tree.RequestedPageSizes, pageSize => Assert.InRange(pageSize, 1, 500));
        viewModel.ClassificationSearchText = "Revestimentos";
        Assert.Equal([11], viewModel.FilteredClassificationTreeNodes.Single().Children.Single().Children.Single().Children
            .Select(node => node.SubgroupItem is null ? 0 : node.SubgroupItem.SubgroupID));
        await ExecuteAsync(viewModel, viewModel.SearchCommand, () => viewModel.IsBusy);
        var selectedClass = viewModel.ClassificationTreeNodes.Single(node => node.ClassItem.ClassID == 1);
        viewModel.SelectedCatalogClassificationNode = selectedClass;

        viewModel.Products.Should().ContainSingle().Which.ProductID.Should().Be(1);
        viewModel.ClassificationFilterDisplay.Should().Be("Revestimentos");
        viewModel.SelectedProduct.Should().BeNull();
    }

    [Fact]
    public async Task Classification_filter_loads_catalog_when_selected_before_first_search()
    {
        var matchingProduct = Product(1) with { ClassID = 1, FamilyID = 11, GroupID = 111, SubgroupID = 11 };
        var otherProduct = Product(2) with { ClassID = 2, FamilyID = 21, GroupID = 211, SubgroupID = 21 };
        var service = new ProductService { Search = () => Task.FromResult<IEnumerable<ProductDTO>>([matchingProduct, otherProduct]) };
        var tree = new TreeClassificationService();
        var viewModel = Create(service, tree, tree, tree, tree);

        await viewModel.EnsureClassificationTreeAsync();
        var subgroup = viewModel.ClassificationTreeNodes
            .SelectMany(classNode => classNode.Children)
            .SelectMany(familyNode => familyNode.Children)
            .SelectMany(groupNode => groupNode.Children)
            .Single(node => node.SubgroupItem?.SubgroupID == 11);

        viewModel.SelectedCatalogClassificationNode = subgroup;

        viewModel.Products.Should().ContainSingle().Which.ProductID.Should().Be(1);
        viewModel.ClassificationFilterLabel.Should().Be("Classificação: Revestimentos / Pisos / Porcelanatos / Esmaltados");
    }

    [Fact]
    public async Task Classification_filter_reloads_catalog_after_search_is_cleared()
    {
        var requestCount = 0;
        var matchingProduct = Product(1) with { ClassID = 1, FamilyID = 11, GroupID = 111, SubgroupID = 11 };
        var service = new ProductService
        {
            Search = () =>
            {
                requestCount++;
                return Task.FromResult<IEnumerable<ProductDTO>>([matchingProduct]);
            }
        };
        var tree = new TreeClassificationService();
        var viewModel = Create(service, tree, tree, tree, tree);
        await viewModel.EnsureClassificationTreeAsync();
        await ExecuteAsync(viewModel, viewModel.SearchCommand, () => viewModel.IsBusy);
        viewModel.ClearSearchCommand.Execute(null);

        viewModel.SelectedCatalogClassificationNode = viewModel.ClassificationTreeNodes.Single(node => node.ClassItem.ClassID == 1);

        await WaitForAsync(() => !viewModel.IsBusy && viewModel.Products.Count == 1);
        requestCount.Should().Be(2);
        viewModel.Products.Should().ContainSingle().Which.ProductID.Should().Be(1);
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        var timeout = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (!condition() && DateTime.UtcNow < timeout)
            await Task.Delay(10);
        condition().Should().BeTrue();
    }

    [Fact]
    public async Task Classification_search_finds_leaf_and_expands_its_ancestor_path()
    {
        var tree = new TreeClassificationService();
        var viewModel = Create(classService: tree, familyService: tree, groupService: tree, subgroupService: tree);

        await viewModel.EnsureClassificationTreeAsync();
        viewModel.ClassificationSearchText = "Esmaltados";

        var classNode = viewModel.FilteredClassificationTreeNodes.Should().ContainSingle().Subject;
        classNode.Name.Should().Be("Revestimentos");
        classNode.IsExpanded.Should().BeTrue();
        var familyNode = classNode.Children.Should().ContainSingle().Subject;
        familyNode.IsExpanded.Should().BeTrue();
        var groupNode = familyNode.Children.Should().ContainSingle().Subject;
        groupNode.IsExpanded.Should().BeTrue();
        groupNode.Children.Should().ContainSingle().Which.Name.Should().Be("Esmaltados");

        viewModel.ClassificationSearchText = "Revestimentos Esmaltados";
        var matchingClass = viewModel.FilteredClassificationTreeNodes.Should().ContainSingle().Subject;
        matchingClass.IsExpanded.Should().BeTrue();
        var matchingFamily = matchingClass.Children.Should().ContainSingle().Subject;
        matchingFamily.IsExpanded.Should().BeTrue();
        matchingFamily.Children.Should().ContainSingle().Which.Children.Should().ContainSingle().Which.Name.Should().Be("Esmaltados");
    }

    [Fact]
    public void Catalog_sort_display_exposes_active_column_direction_and_clears_cleanly()
    {
        var viewModel = Create();

        viewModel.CatalogSortDisplay.Should().Be("CÓDIGO: Crescente");
        viewModel.HasCatalogSort.Should().BeTrue();
        viewModel.SetCatalogSortDisplay("Produto: Decrescente");
        viewModel.CatalogSortDisplay.Should().Be("Produto: Decrescente");
        viewModel.HasCatalogSort.Should().BeTrue();
        viewModel.HasActiveCatalogFilters.Should().BeTrue();

        viewModel.SetCatalogSortDisplay(null);
        viewModel.CatalogSortDisplay.Should().BeEmpty();
        viewModel.HasCatalogSort.Should().BeFalse();
        viewModel.HasActiveCatalogFilters.Should().BeFalse();
    }

    [Fact]
    public async Task Editor_classification_is_assigned_only_after_include_command()
    {
        var tree = new TreeClassificationService();
        var viewModel = Create(classService: tree, familyService: tree, groupService: tree, subgroupService: tree);
        await viewModel.BeginNewAsync();
        var subgroup = viewModel.FilteredEditorClassificationTreeNodes
            .SelectMany(classNode => classNode.Children)
            .SelectMany(familyNode => familyNode.Children)
            .SelectMany(groupNode => groupNode.Children)
            .Single(node => node.SubgroupItem?.SubgroupID == 11);

        viewModel.SelectedEditorClassificationNode = subgroup;
        viewModel.SelectedSubgroup.Should().BeNull();
        viewModel.CanIncludeEditorClassification.Should().BeTrue();
        viewModel.IncludeEditorClassificationCommand.Execute(null);

        viewModel.SelectedSubgroup?.SubgroupID.Should().Be(11);
        viewModel.SelectedClassificationPath.Should().Be("Revestimentos / Pisos / Porcelanatos / Esmaltados");
    }

    [Fact]
    public async Task Catalog_summary_loads_while_classification_lookups_are_pending()
    {
        var pendingClasses = new TaskCompletionSource<IEnumerable<ClassDTO>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var classification = new ClassificationService { PendingClasses = pendingClasses };
        var productService = new ProductService
        {
            Summary = () => Task.FromResult<ProductCatalogSummaryDTO?>(new(7, 6, 1, 2))
        };
        var viewModel = Create(productService, classification);

        var initialization = viewModel.InitializeAsync();

        viewModel.IsBusy.Should().BeTrue();
        await viewModel.EnsureCatalogSummaryAsync();
        viewModel.CatalogSummary.Should().Be(new ProductCatalogSummaryDTO(7, 6, 1, 2));
        pendingClasses.SetResult([new ClassDTO(1, "Classe")]);
        await initialization;
    }

    [Fact]
    public async Task Unpermitted_product_actions_are_unavailable()
    {
        var viewModel = Create(authorization: new DeniedAuthorization());
        viewModel.SelectedProduct = Product();
        viewModel.CanNew.Should().BeFalse();
        viewModel.CanEdit.Should().BeFalse();
        viewModel.SearchCommand.CanExecute(null).Should().BeFalse();
        viewModel.NewCommand.CanExecute(null).Should().BeFalse();
        viewModel.EditCommand.CanExecute(null).Should().BeFalse();
        await viewModel.BeginNewAsync();
        viewModel.CanSave.Should().BeFalse();
        viewModel.CancelCommand.CanExecute(null).Should().BeTrue();
    }

    [Theory]
    [InlineData("00123456", false)]
    [InlineData("abcdefgh", true)]
    [InlineData("1234567a", true)]
    public void Barcode_validation_rejects_nonnumeric_codes(string barcode, bool invalid)
    {
        var errors = new Decor.Core.Validation.ProductDTOValidator().Validate(Product() with { Barcode = barcode });
        errors.Any(error => error.Contains("apenas")).Should().Be(invalid);
    }

    private sealed class DeniedAuthorization : IAuthorizationService
    {
        public bool HasPermission(string code) => false;
        public bool CanView(string resource) => false;
        public bool CanCreate(string resource) => false;
        public bool CanEdit(string resource) => false;
        public bool CanDelete(string resource) => false;
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task BeginAndCancel_ShowActionAndSelectedDataThenClearStatus(bool adding)
    {
        var viewModel = Create();
        viewModel.SetValueMatch("Descricao", 4);
        if (adding)
            await viewModel.BeginNewAsync();
        else
        {
            viewModel.SelectedProduct = Product();
            await viewModel.BeginEditAsync();
        }

        viewModel.IsEditing.Should().BeTrue();
        viewModel.IsListVisible.Should().BeFalse();
        viewModel.IsAdding.Should().Be(adding);
        viewModel.ProductId.Should().Be(adding ? 0 : 42);
        viewModel.ProductName.Should().Be(adding ? null : "Produto");
        viewModel.StatusMessage.Should().Be(adding ? "Cadastrando um produto." : "Editando o produto 42.");
        AssertNoListingStatus(viewModel);
        viewModel.CancelEdit();
        viewModel.IsEditing.Should().BeFalse();
        viewModel.IsAdding.Should().BeFalse();
        viewModel.IsListVisible.Should().BeTrue();
        viewModel.StatusMessage.Should().BeEmpty();
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public async Task PendingSearch_DoesNotPublishListingStatusWhileEditing(bool adding, bool empty)
    {
        var pending = new TaskCompletionSource<IEnumerable<ProductDTO>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var viewModel = Create(new ProductService { Search = () => pending.Task });
        await viewModel.InitializeAsync();
        viewModel.SearchText = "Produto";
        var load = ExecuteAsync(viewModel, viewModel.SearchCommand, () => viewModel.IsBusy);
        viewModel.IsBusy.Should().BeTrue();
        if (adding)
            await viewModel.BeginNewAsync();
        else
        {
            viewModel.SelectedProduct = Product();
            await viewModel.BeginEditAsync();
        }
        viewModel.SetValueMatch("Descricao", 2);
        var status = viewModel.StatusMessage;
        var published = ObserveStatus(viewModel);

        pending.SetResult(empty ? [] : [Product(7)]);
        await load;

        viewModel.Products.Count.Should().Be(empty ? 0 : 1);
        viewModel.IsEditing.Should().BeTrue();
        viewModel.StatusMessage.Should().Be(status);
        published.Should().BeEmpty();
        AssertNoListingStatus(viewModel);
        viewModel.CancelEdit();
        viewModel.HasPagination.Should().BeTrue();
    }

    [Theory]
    [InlineData(0, "N\u00e3o foi poss\u00edvel carregar os produtos.")]
    [InlineData(1, "Voc\u00ea n\u00e3o possui permiss\u00e3o para consultar produtos.")]
    public async Task PendingSearchFailure_RemainsVisibleWhileEditing(int kind, string expected)
    {
        var pending = new TaskCompletionSource<IEnumerable<ProductDTO>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var viewModel = Create(new ProductService { Search = () => pending.Task });
        await viewModel.InitializeAsync();
        viewModel.SearchText = "Produto";
        var load = ExecuteAsync(viewModel, viewModel.SearchCommand, () => viewModel.IsBusy);
        await viewModel.BeginNewAsync();
        pending.SetException(Failure(kind));
        await load;

        viewModel.IsEditing.Should().BeTrue();
        viewModel.StatusMessage.Should().Be(expected);
    }

    [Theory]
    [InlineData(0, "N\u00e3o foi poss\u00edvel salvar o produto.")]
    [InlineData(1, "Voc\u00ea n\u00e3o possui permiss\u00e3o para salvar produtos.")]
    [InlineData(2, "Verifique os erros real\u00e7ados nos campos abaixo.")]
    public async Task SaveFailure_RemainsVisibleAndKeepsFormOpen(int kind, string expected)
    {
        var viewModel = Create(new ProductService { Save = () => Task.FromException(Failure(kind)) });
        await viewModel.BeginNewAsync();
        await ExecuteAsync(viewModel, viewModel.SaveCommand, () => viewModel.IsBusy);

        viewModel.IsEditing.Should().BeTrue();
        viewModel.StatusMessage.Should().Be(expected);
    }

    [Theory]
    [InlineData(0, "N\u00e3o foi poss\u00edvel carregar as fam\u00edlias.")]
    [InlineData(1, "N\u00e3o foi poss\u00edvel carregar os grupos.")]
    [InlineData(2, "N\u00e3o foi poss\u00edvel carregar os subgrupos.")]
    public async Task BeginEdit_LookupFailureIsNotReplacedByActionStatus(int level, string expected)
    {
        var viewModel = Create(classService: new ClassificationService { FailureLevel = level });
        viewModel.SelectedProduct = Product();
        await viewModel.BeginEditAsync();

        viewModel.IsEditing.Should().BeTrue();
        viewModel.StatusMessage.Should().Be(expected);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task MissingRequiredLookups_DoesNotOpenFormOrShowActionStatus(bool adding)
    {
        var viewModel = Create(classService: new ClassificationService { MissingClasses = true });
        if (adding)
            await viewModel.BeginNewAsync();
        else
        {
            viewModel.SelectedProduct = Product();
            await viewModel.BeginEditAsync();
        }

        viewModel.IsEditing.Should().BeFalse();
        viewModel.StatusMessage.Should().Be(adding
            ? "N\u00e3o foi poss\u00edvel carregar os dados necess\u00e1rios para incluir o produto."
            : "N\u00e3o foi poss\u00edvel carregar os dados necess\u00e1rios para editar o produto.");
    }

    private sealed class ProductService : IProductService
    {
        public Func<Task<IEnumerable<ProductDTO>>> Search { get; set; } = () => Task.FromResult<IEnumerable<ProductDTO>>([]);
        public Action<string>? SearchRequested { get; set; }
        public Func<Task> Save { get; set; } = () => Task.CompletedTask;
        public Func<Task<ProductCatalogSummaryDTO?>> Summary { get; set; } = () => Task.FromResult<ProductCatalogSummaryDTO?>(null);
        public Task<ProductCatalogSummaryDTO?> GetCatalogSummaryAsync(CancellationToken cancellationToken = default) => Summary();
        public Task<ProductDTO> GetProductByIdAsync(int productId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IEnumerable<ProductDTO>> GetAllProductsAsync(int page = 1, int pageSize = 100, CancellationToken cancellationToken = default) => Task.FromResult<IEnumerable<ProductDTO>>([]);
        public Task<IEnumerable<ProductDTO>> SearchProductsAsync(string searchTerm, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default)
        {
            SearchRequested?.Invoke(searchTerm);
            return Search();
        }
        public Task SaveProductAsync(ProductDTO product, CancellationToken cancellationToken = default) => Save();
    }

    private sealed class ClassificationService : IClassService, IFamilyService, IGroupService, ISubgroupService
    {
        public int FailureLevel { get; init; } = -1;
        public bool MissingClasses { get; init; }
        public TaskCompletionSource<IEnumerable<ClassDTO>>? PendingClasses { get; init; }
        public Task<IEnumerable<ClassDTO>> GetAllAsync(int page = 1, int pageSize = 100, CancellationToken cancellationToken = default) => PendingClasses?.Task ?? Task.FromResult<IEnumerable<ClassDTO>>(MissingClasses ? [] : [new ClassDTO(1, "Classe")]);
        public Task<IEnumerable<FamilyDTO>> GetByClassIdAsync(int classId, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default) => FailureLevel == 0
            ? Task.FromException<IEnumerable<FamilyDTO>>(Failure(0)) : Task.FromResult<IEnumerable<FamilyDTO>>([new FamilyDTO(1, "Familia")]);
        public Task<IEnumerable<GroupDTO>> GetByFamilyIdAsync(int familyId, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default) => FailureLevel == 1
            ? Task.FromException<IEnumerable<GroupDTO>>(Failure(0)) : Task.FromResult<IEnumerable<GroupDTO>>([new GroupDTO(1, "Grupo")]);
        public Task<IEnumerable<SubgroupDTO>> GetByGroupIdAsync(int groupId, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default) => FailureLevel == 2
            ? Task.FromException<IEnumerable<SubgroupDTO>>(Failure(0)) : Task.FromResult<IEnumerable<SubgroupDTO>>([new SubgroupDTO(1, "Subgrupo")]);
    }

    private sealed class TreeClassificationService : IClassService, IFamilyService, IGroupService, ISubgroupService
    {
        public List<int> RequestedPageSizes { get; } = [];
        public Task<IEnumerable<ClassDTO>> GetAllAsync(int page = 1, int pageSize = 100, CancellationToken cancellationToken = default) =>
            Task.FromResult<IEnumerable<ClassDTO>>(Track<ClassDTO>(pageSize, [new(1, "Revestimentos"), new(2, "Decoração")]));
        public Task<IEnumerable<FamilyDTO>> GetByClassIdAsync(int classId, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default) =>
            Task.FromResult<IEnumerable<FamilyDTO>>(Track<FamilyDTO>(pageSize, [new(classId == 1 ? 11 : 21, classId == 1 ? "Pisos" : "Objetos")]));
        public Task<IEnumerable<GroupDTO>> GetByFamilyIdAsync(int familyId, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default) =>
            Task.FromResult<IEnumerable<GroupDTO>>(Track<GroupDTO>(pageSize, [new(familyId == 11 ? 111 : 211, familyId == 11 ? "Porcelanatos" : "Vasos")]));
        public Task<IEnumerable<SubgroupDTO>> GetByGroupIdAsync(int groupId, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default) =>
            Task.FromResult<IEnumerable<SubgroupDTO>>(Track<SubgroupDTO>(pageSize, [new(groupId == 111 ? 11 : 21, groupId == 111 ? "Esmaltados" : "Cerâmica")]));

        private IEnumerable<T> Track<T>(int pageSize, IEnumerable<T> items)
        {
            RequestedPageSizes.Add(pageSize);
            return items;
        }
    }
}
