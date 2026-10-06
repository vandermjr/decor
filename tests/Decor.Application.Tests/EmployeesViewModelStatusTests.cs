using Decor.AvaloniaUI.ViewModels;
using Decor.Core.DTOs;
using Decor.Core.Interfaces.Services;
using FluentAssertions;
using static Decor.Application.Tests.RegistrationStatusTestSupport;

namespace Decor.Application.Tests;

public sealed class EmployeesViewModelStatusTests
{
    private static EmployeeDTO Employee(int id = 42) => new(id, "Pessoa", "Cargo", 1000m, "Horario", "Documento", "Telefone", true, 3);
    private static EmployeesViewModel Create(EmployeeService? service = null) => new(service ?? new EmployeeService(), new AuthorizationService());

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task BeginAndCancel_ShowActionAndSelectedDataThenClearStatus(bool adding)
    {
        var viewModel = Create();
        if (adding)
            await viewModel.BeginNewAsync();
        else
        {
            viewModel.SelectedEmployee = Employee();
            viewModel.BeginEdit();
        }

        viewModel.IsEditing.Should().BeTrue();
        viewModel.IsAdding.Should().Be(adding);
        viewModel.EmployeeId.Should().Be(adding ? 0 : 42);
        viewModel.Name.Should().Be(adding ? string.Empty : "Pessoa");
        viewModel.StatusMessage.Should().Be(adding ? "Cadastrando um funcion\u00e1rio." : "Editando o funcion\u00e1rio 42.");
        AssertNoListingStatus(viewModel);
        viewModel.CancelEdit();
        viewModel.IsEditing.Should().BeFalse();
        viewModel.IsAdding.Should().BeFalse();
        viewModel.SelectedEmployee.Should().BeNull();
        viewModel.HasError.Should().BeFalse();
        viewModel.StatusMessage.Should().BeEmpty();
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public async Task PendingLoad_DoesNotPublishListingStatusWhileEditing(bool adding, bool empty)
    {
        var pending = new TaskCompletionSource<IEnumerable<EmployeeDTO>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var viewModel = Create(new EmployeeService { Search = () => pending.Task });
        var load = viewModel.InitializeAsync();
        viewModel.IsBusy.Should().BeTrue();
        if (adding)
            await viewModel.BeginNewAsync();
        else
        {
            viewModel.SelectedEmployee = Employee();
            viewModel.BeginEdit();
        }
        var status = viewModel.StatusMessage;
        var published = ObserveStatus(viewModel);

        pending.SetResult(empty ? [] : [Employee(7)]);
        await load.WaitAsync(TimeSpan.FromSeconds(5));

        viewModel.Employees.Count.Should().Be(empty ? 0 : 1);
        viewModel.IsEditing.Should().BeTrue();
        viewModel.StatusMessage.Should().Be(status);
        published.Should().BeEmpty();
        AssertNoListingStatus(viewModel);
    }

    [Theory]
    [InlineData(0, "N\u00e3o foi poss\u00edvel carregar os funcion\u00e1rios.")]
    [InlineData(1, "Voc\u00ea n\u00e3o possui permiss\u00e3o para consultar funcion\u00e1rios.")]
    public async Task PendingLoadFailure_RemainsVisibleWhileEditing(int kind, string expected)
    {
        var pending = new TaskCompletionSource<IEnumerable<EmployeeDTO>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var viewModel = Create(new EmployeeService { Search = () => pending.Task });
        var load = viewModel.InitializeAsync();
        await viewModel.BeginNewAsync();
        pending.SetException(Failure(kind));
        await load.WaitAsync(TimeSpan.FromSeconds(5));

        viewModel.IsEditing.Should().BeTrue();
        viewModel.StatusMessage.Should().Be(expected);
    }

    [Theory]
    [InlineData(0, "N\u00e3o foi poss\u00edvel salvar o funcion\u00e1rio.")]
    [InlineData(1, "Voc\u00ea n\u00e3o possui permiss\u00e3o para salvar funcion\u00e1rios.")]
    [InlineData(2, "Nome invalido")]
    public async Task SaveFailure_RemainsVisibleAndKeepsFormOpen(int kind, string expected)
    {
        var viewModel = Create(new EmployeeService { Save = () => Task.FromException(Failure(kind)) });
        await viewModel.BeginNewAsync();
        await ExecuteAsync(viewModel, viewModel.SaveCommand, () => viewModel.IsBusy);

        viewModel.IsEditing.Should().BeTrue();
        viewModel.HasError.Should().BeTrue();
        viewModel.ErrorMessage.Should().Be(expected);
        if (kind == 2)
            viewModel.StatusMessage.Should().Be("Verifique os dados informados.");
    }

    [Fact]
    public async Task LoadOutsideEditing_ShowsListingStatus()
    {
        var viewModel = Create(new EmployeeService { Search = () => Task.FromResult<IEnumerable<EmployeeDTO>>([Employee()]) });
        await viewModel.InitializeAsync();
        viewModel.StatusMessage.Should().Be("1 funcion\u00e1rio.");
    }

    private sealed class EmployeeService : IEmployeeService
    {
        public Func<Task<IEnumerable<EmployeeDTO>>> Search { get; set; } = () => Task.FromResult<IEnumerable<EmployeeDTO>>([]);
        public Func<Task> Save { get; set; } = () => Task.CompletedTask;
        public Task<EmployeeDTO> GetEmployeeByIdAsync(int employeeId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IEnumerable<EmployeeDTO>> GetAllEmployeesAsync(int page = 1, int pageSize = 100, CancellationToken cancellationToken = default) => Task.FromResult<IEnumerable<EmployeeDTO>>([]);
        public Task<IEnumerable<EmployeeDTO>> SearchEmployeesAsync(string searchTerm, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default) => Search();
        public Task SaveEmployeeAsync(EmployeeDTO employee, CancellationToken cancellationToken = default) => Save();
        public Task DeleteEmployeeAsync(int employeeId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}