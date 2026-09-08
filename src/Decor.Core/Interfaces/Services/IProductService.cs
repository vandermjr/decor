using Decor.Core.DTOs;

namespace Decor.Core.Interfaces.Services;

public interface IProductService
{
    Task<ProductDTO> GetProductByIdAsync(int productId, CancellationToken cancellationToken = default);
    Task<IEnumerable<ProductDTO>> GetAllProductsAsync(int page = 1, int pageSize = 100, CancellationToken cancellationToken = default);
    Task<IEnumerable<ProductDTO>> SearchProductsAsync(string searchTerm, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default);
    Task SaveProductAsync(ProductDTO product, CancellationToken cancellationToken = default);
}