using Decor.Core.DTOs;

namespace Decor.Core.Interfaces.Services;

public interface IEmployeeService
{
    Task<EmployeeDTO> GetEmployeeByIdAsync(int employeeId, CancellationToken cancellationToken = default);
    Task<IEnumerable<EmployeeDTO>> GetAllEmployeesAsync(int page = 1, int pageSize = 100, CancellationToken cancellationToken = default);
    Task<IEnumerable<EmployeeDTO>> SearchEmployeesAsync(string searchTerm, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default);
    Task SaveEmployeeAsync(EmployeeDTO employee, CancellationToken cancellationToken = default);
    Task DeleteEmployeeAsync(int employeeId, CancellationToken cancellationToken = default);
}
