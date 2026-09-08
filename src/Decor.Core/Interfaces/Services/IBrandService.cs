using Decor.Core.DTOs;

namespace Decor.Core.Interfaces.Services;

public interface IBrandService
{
    Task<BrandDTO> GetBrandByIdAsync(int brandId, CancellationToken cancellationToken = default);
    Task<IEnumerable<BrandDTO>> GetAllBrandsAsync(int page = 1, int pageSize = 100, CancellationToken cancellationToken = default);
    Task<IEnumerable<BrandDTO>> SearchBrandsAsync(string searchTerm, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default);
    Task SaveBrandAsync(BrandDTO brand, CancellationToken cancellationToken = default);
    Task DeleteBrandAsync(int brandId, CancellationToken cancellationToken = default);
}
