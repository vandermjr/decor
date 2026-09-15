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
using FluentAssertions;
using Xunit;

namespace Decor.Application.Tests;

public sealed class EmployeeServiceTests
{
    [Fact]
    public async Task SaveEmployee_CreateAndUpdate_PersistsBasicHrFields()
    {
        var repository = new TrackingEmployeeRepository();
        var service = CreateService(repository, DecorPermissions.EmployeesCreate, DecorPermissions.EmployeesEdit);

        await service.SaveEmployeeAsync(new EmployeeDTO(0, "Ana", "Vendedora", 2500m, "Segunda a sexta, 8h às 17h", null, null, true, null));
        await service.SaveEmployeeAsync(new EmployeeDTO(1, "Ana Maria", "Gerente", 3200m, "Horário comercial", null, null, true, null));

        repository.Employees.Should().ContainSingle();
        repository.Employees[0].Should().Match<Employee>(employee =>
            employee.Name == "Ana Maria" &&
            employee.JobTitle == "Gerente" &&
            employee.BaseSalary == 3200m &&
            employee.WorkScheduleNote == "Horário comercial");
    }

    [Fact]
    public async Task SaveEmployee_NullableBasicHrFields_AreAccepted()
    {
        var repository = new TrackingEmployeeRepository();
        var service = CreateService(repository, DecorPermissions.EmployeesCreate);

        await service.SaveEmployeeAsync(new EmployeeDTO(0, "Ana", null, null, null, null, null, true, null));

        repository.Employees[0].JobTitle.Should().BeNull();
        repository.Employees[0].BaseSalary.Should().BeNull();
        repository.Employees[0].WorkScheduleNote.Should().BeNull();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task SaveEmployee_NonPositiveBaseSalary_ThrowsValidationException(decimal salary)
    {
        var service = CreateService(new TrackingEmployeeRepository(), DecorPermissions.EmployeesCreate);
        var dto = new EmployeeDTO(0, "Ana", "Vendedora", salary, null, null, null, true, null);

        var act = () => service.SaveEmployeeAsync(dto);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*salário base*");
    }

    [Fact]
    public void EmployeeMapper_RoundTripsBasicHrFields()
    {
        var entity = new Employee { EmployeeID = 7, Name = "Ana", JobTitle = "Instaladora", BaseSalary = 2800m, WorkScheduleNote = "Escala variável" };

        var dto = entity.ToDTO();
        var mapped = dto.FromDTO();

        mapped.JobTitle.Should().Be(entity.JobTitle);
        mapped.BaseSalary.Should().Be(entity.BaseSalary);
        mapped.WorkScheduleNote.Should().Be(entity.WorkScheduleNote);
    }

    private static EmployeeService CreateService(TrackingEmployeeRepository repository, params string[] permissions)
        => new(repository, new EmployeeDTOValidator(), new EmployeeRepositoryValidator(repository), new FixedAuthorizationService(permissions));

    private sealed class TrackingEmployeeRepository : IEmployeeRepository
    {
        public List<Employee> Employees { get; } = [];

        public int Save(Employee employee)
        {
            SaveEntity(employee);
            return 1;
        }

        public Task<int> SaveAsync(Employee employee, CancellationToken cancellationToken = default)
        {
            SaveEntity(employee);
            return Task.FromResult(1);
        }

        public int Delete(int employeeId)
        {
            Employees.RemoveAll(employee => employee.EmployeeID == employeeId);
            return 1;
        }

        public Task<int> DeleteAsync(int employeeId, CancellationToken cancellationToken = default)
            => Task.FromResult(Delete(employeeId));

        public IEnumerable<Employee> SearchGetBy(string? arg = null) => Employees;

        public Task<IReadOnlyList<Employee>> SearchGetByAsync(string? arg = null, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Employee>>(Employees);

        private void SaveEntity(Employee employee)
        {
            if (employee.EmployeeID == 0)
                employee.EmployeeID = Employees.Count + 1;

            Employees.RemoveAll(existing => existing.EmployeeID == employee.EmployeeID);
            Employees.Add(employee);
        }
    }

    private sealed class FixedAuthorizationService(params string[] permissions) : IAuthorizationService
    {
        private readonly HashSet<string> _permissions = new(permissions);
        public bool HasPermission(string permission) => _permissions.Contains(permission);
        public bool CanCreate(string resource) => HasPermission($"{resource}.Create");
        public bool CanEdit(string resource) => HasPermission($"{resource}.Edit");
        public bool CanDelete(string resource) => HasPermission($"{resource}.Delete");
        public bool CanView(string resource) => HasPermission($"{resource}.View");
    }
}