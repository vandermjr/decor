using Decor.Core.DTOs;

namespace Decor.Core.Interfaces.Services;

public interface IProductKitComponentService
{
    Task<ProductKitComponentDTO> GetProductKitComponentByIdAsync(int componentId, CancellationToken cancellationToken = default);
    Task<IEnumerable<ProductKitComponentDTO>> GetAllProductKitComponentsAsync(int page = 1, int pageSize = 100, CancellationToken cancellationToken = default);
    Task<IEnumerable<ProductKitComponentDTO>> SearchProductKitComponentsAsync(string searchTerm, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default);
    Task<IEnumerable<ProductKitComponentDTO>> GetByKitProductIdAsync(int kitProductId, CancellationToken cancellationToken = default);
    Task SaveProductKitComponentAsync(ProductKitComponentDTO productKitComponent, CancellationToken cancellationToken = default);
    Task DeleteProductKitComponentAsync(int componentId, CancellationToken cancellationToken = default);
    // Soma de (ComponentProductID.SalePrice x Quantity) para todos os componentes do Kit; null se algum componente não tiver SalePrice.
    Task<decimal?> GetSuggestedKitPriceAsync(int kitProductId, CancellationToken cancellationToken = default);
}
