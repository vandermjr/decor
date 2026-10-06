using Decor.AvaloniaUI.ViewModels;
using Decor.Core.DTOs;
using Decor.Core.Interfaces.Services;
using FluentAssertions;
using static Decor.Application.Tests.RegistrationStatusTestSupport;

namespace Decor.Application.Tests;

public sealed class BrandsViewModelStatusTests
{
    [Fact]
    public async Task BeginNewAndCancel_ShowActionThenClearStatus()
    {
        var viewModel = new BrandsViewModel(new BrandService());

        await viewModel.BeginNewAsync();

        viewModel.IsEditing.Should().BeTrue();
        viewModel.IsAdding.Should().BeTrue();
        viewModel.BrandId.Should().Be(0);
        viewModel.StatusMessage.Should().Be("Cadastrando uma marca.");
        viewModel.CancelEdit();
        viewModel.IsEditing.Should().BeFalse();
        viewModel.IsAdding.Should().BeFalse();
        viewModel.StatusMessage.Should().BeEmpty();
    }

    [Fact]
    public void BeginEditAndCancel_ShowSelectedIdThenClearStatus()
    {
        var viewModel = new BrandsViewModel(new BrandService()) { SelectedBrand = new BrandDTO(42, "Marca") };

        viewModel.BeginEdit();

        viewModel.IsEditing.Should().BeTrue();
        viewModel.IsAdding.Should().BeFalse();
        viewModel.BrandId.Should().Be(42);
        viewModel.BrandName.Should().Be("Marca");
        viewModel.StatusMessage.Should().Be("Editando a marca 42.");
        viewModel.CancelEdit();
        viewModel.SelectedBrand.Should().BeNull();
        viewModel.StatusMessage.Should().BeEmpty();
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public async Task PendingSearch_DoesNotReplaceEditingStatusWithListCount(bool adding, bool empty)
    {
        var pending = new TaskCompletionSource<IEnumerable<BrandDTO>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var viewModel = new BrandsViewModel(new BrandService { Search = () => pending.Task });
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(viewModel.IsBusy) && !viewModel.IsBusy)
                completed.TrySetResult();
        };
        viewModel.SearchCommand.Execute(null);
        viewModel.IsBusy.Should().BeTrue();
        if (adding)
            await viewModel.BeginNewAsync();
        else
        {
            viewModel.SelectedBrand = new BrandDTO(42, "Marca");
            viewModel.BeginEdit();
        }
        var status = viewModel.StatusMessage;
        var published = new List<string>();
        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(viewModel.StatusMessage))
                published.Add(viewModel.StatusMessage);
        };

        pending.SetResult(empty ? [] : [new BrandDTO(7, "Outra marca")]);
        await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));

        viewModel.Brands.Count.Should().Be(empty ? 0 : 1);
        viewModel.IsEditing.Should().BeTrue();
        viewModel.StatusMessage.Should().Be(status);
        published.Should().BeEmpty();
        AssertNoListingStatus(viewModel);
    }

    [Theory]
    [InlineData(0, "N\u00e3o foi poss\u00edvel salvar a marca.")]
    [InlineData(1, "Voc\u00ea n\u00e3o possui permiss\u00e3o para salvar marcas.")]
    [InlineData(2, "Verifique os erros real\u00e7ados nos campos abaixo.")]
    public async Task SaveFailure_RemainsVisibleAndKeepsFormOpen(int kind, string expected)
    {
        var viewModel = new BrandsViewModel(new BrandService { Save = () => Task.FromException(Failure(kind)) });
        await viewModel.BeginNewAsync();
        await ExecuteAsync(viewModel, viewModel.SaveCommand, () => viewModel.IsBusy);

        viewModel.IsEditing.Should().BeTrue();
        viewModel.StatusMessage.Should().Be(expected);
        if (kind == 2)
            viewModel.HasBrandNameError.Should().BeTrue();
    }

    [Theory]
    [InlineData(0, "N\u00e3o foi poss\u00edvel carregar as marcas.")]
    [InlineData(1, "Voc\u00ea n\u00e3o possui permiss\u00e3o para consultar marcas.")]
    public async Task PendingSearchFailure_RemainsVisibleWhileEditing(int kind, string expected)
    {
        var pending = new TaskCompletionSource<IEnumerable<BrandDTO>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var viewModel = new BrandsViewModel(new BrandService { Search = () => pending.Task });
        var load = ExecuteAsync(viewModel, viewModel.SearchCommand, () => viewModel.IsBusy);
        await viewModel.BeginNewAsync();
        pending.SetException(Failure(kind));
        await load;

        viewModel.IsEditing.Should().BeTrue();
        viewModel.StatusMessage.Should().Be(expected);
    }

    [Fact]
    public async Task SearchOutsideEditing_ShowsListingStatus()
    {
        var viewModel = new BrandsViewModel(new BrandService { Search = () => Task.FromResult<IEnumerable<BrandDTO>>([new BrandDTO(1, "Marca")]) });
        await ExecuteAsync(viewModel, viewModel.SearchCommand, () => viewModel.IsBusy);
        viewModel.StatusMessage.Should().Be("1 marca(s).");
    }

    internal sealed class BrandService : IBrandService
    {
        public Func<Task<IEnumerable<BrandDTO>>> Search { get; set; } = () => Task.FromResult<IEnumerable<BrandDTO>>([]);
        public Func<Task> Save { get; set; } = () => Task.CompletedTask;
        public Task<BrandDTO> GetBrandByIdAsync(int brandId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IEnumerable<BrandDTO>> GetAllBrandsAsync(int page = 1, int pageSize = 100, CancellationToken cancellationToken = default) => Task.FromResult<IEnumerable<BrandDTO>>([new BrandDTO(1, "Marca")]);
        public Task<IEnumerable<BrandDTO>> SearchBrandsAsync(string searchTerm, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default) => Search();
        public Task SaveBrandAsync(BrandDTO brand, CancellationToken cancellationToken = default) => Save();
        public Task DeleteBrandAsync(int brandId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}