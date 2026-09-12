using Decor.Core.DTOs;

namespace Decor.Core.Interfaces.Services;

public interface IProductSpecificationAttributeService
{
    Task<ProductSpecificationAttributeDTO> GetProductSpecificationAttributeByIdAsync(int attributeId, CancellationToken cancellationToken = default);
    Task<IEnumerable<ProductSpecificationAttributeDTO>> GetAllProductSpecificationAttributesAsync(int page = 1, int pageSize = 100, CancellationToken cancellationToken = default);
    Task<IEnumerable<ProductSpecificationAttributeDTO>> SearchProductSpecificationAttributesAsync(string searchTerm, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default);
    Task SaveProductSpecificationAttributeAsync(ProductSpecificationAttributeDTO attribute, CancellationToken cancellationToken = default);
    Task DeleteProductSpecificationAttributeAsync(int attributeId, CancellationToken cancellationToken = default);
}
