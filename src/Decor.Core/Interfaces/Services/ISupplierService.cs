using Decor.Core.DTOs;

namespace Decor.Core.Interfaces.Services;

public interface ISupplierService
{
    Task<SupplierDTO> GetSupplierByIdAsync(int supplierId, CancellationToken cancellationToken = default);
    Task<IEnumerable<SupplierDTO>> GetAllSuppliersAsync(int page = 1, int pageSize = 100, CancellationToken cancellationToken = default);
    Task<IEnumerable<SupplierDTO>> SearchSuppliersAsync(string searchTerm, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default);
    Task SaveSupplierAsync(SupplierDTO supplier, CancellationToken cancellationToken = default);
    Task DeleteSupplierAsync(int supplierId, CancellationToken cancellationToken = default);
}
