using Decor.AvaloniaUI.ViewModels;
using Decor.Core.DTOs;
using Decor.Core.Interfaces.Services;
using FluentAssertions;
using static Decor.Application.Tests.RegistrationStatusTestSupport;

namespace Decor.Application.Tests;

public sealed class ProductsViewModelStatusTests
{
    private static ProductDTO Product(int id = 42) => new(id, "789", true, "Produto", 3m, 1, "Marca", 1, "Subgrupo", 1, "Grupo", 1, "Familia", 1, "Classe", "Ref", null, null, null, 1m, 0, null, null, null, null, null);
    private static ProductsViewModel Create(ProductService? products = null, ClassificationService? classification = null, BrandsViewModelStatusTests.BrandService? brands = null, IAuthorizationService? authorization = null)
    {
        classification ??= new ClassificationService();
        return new(products ?? new ProductService(), brands ?? new BrandsViewModelStatusTests.BrandService(), classification, classification, classification, classification, authorization);
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
        viewModel.Description.Should().Be(adding ? null : "Produto");
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
        var viewModel = Create(classification: new ClassificationService { FailureLevel = level });
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
        var viewModel = Create(classification: new ClassificationService { MissingClasses = true });
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
        public Func<Task> Save { get; set; } = () => Task.CompletedTask;
        public Task<ProductDTO> GetProductByIdAsync(int productId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IEnumerable<ProductDTO>> GetAllProductsAsync(int page = 1, int pageSize = 100, CancellationToken cancellationToken = default) => Task.FromResult<IEnumerable<ProductDTO>>([]);
        public Task<IEnumerable<ProductDTO>> SearchProductsAsync(string searchTerm, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default) => Search();
        public Task SaveProductAsync(ProductDTO product, CancellationToken cancellationToken = default) => Save();
    }

    private sealed class ClassificationService : IClassService, IFamilyService, IGroupService, ISubgroupService
    {
        public int FailureLevel { get; init; } = -1;
        public bool MissingClasses { get; init; }
        public Task<IEnumerable<ClassDTO>> GetAllAsync(int page = 1, int pageSize = 100, CancellationToken cancellationToken = default) => Task.FromResult<IEnumerable<ClassDTO>>(MissingClasses ? [] : [new ClassDTO(1, "Classe")]);
        public Task<IEnumerable<FamilyDTO>> GetByClassIdAsync(int classId, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default) => FailureLevel == 0
            ? Task.FromException<IEnumerable<FamilyDTO>>(Failure(0)) : Task.FromResult<IEnumerable<FamilyDTO>>([new FamilyDTO(1, "Familia")]);
        public Task<IEnumerable<GroupDTO>> GetByFamilyIdAsync(int familyId, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default) => FailureLevel == 1
            ? Task.FromException<IEnumerable<GroupDTO>>(Failure(0)) : Task.FromResult<IEnumerable<GroupDTO>>([new GroupDTO(1, "Grupo")]);
        public Task<IEnumerable<SubgroupDTO>> GetByGroupIdAsync(int groupId, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default) => FailureLevel == 2
            ? Task.FromException<IEnumerable<SubgroupDTO>>(Failure(0)) : Task.FromResult<IEnumerable<SubgroupDTO>>([new SubgroupDTO(1, "Subgrupo")]);
    }
}