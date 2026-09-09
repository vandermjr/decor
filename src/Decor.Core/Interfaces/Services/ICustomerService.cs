using Decor.Core.DTOs;

namespace Decor.Core.Interfaces.Services;

public interface ICustomerService
{
    Task<CustomerDTO> GetCustomerByIdAsync(int customerId, CancellationToken cancellationToken = default);
    Task<IEnumerable<CustomerDTO>> GetAllCustomersAsync(int page = 1, int pageSize = 100, CancellationToken cancellationToken = default);
    Task<IEnumerable<CustomerDTO>> SearchCustomersAsync(string searchTerm, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default);
    Task SaveCustomerAsync(CustomerDTO customer, CancellationToken cancellationToken = default);
    Task DeleteCustomerAsync(int customerId, CancellationToken cancellationToken = default);
}
