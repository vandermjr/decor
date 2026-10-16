using Decor.Core.Entities;
using Decor.Core.DTOs;

namespace Decor.Core.Interfaces.Repositories;
public interface IProductRepository : IRepository<Product>
{
    // Verifica se a marca referenciada existe.
    bool BrandExists(int marcaId);
    bool SubgroupExists(int subgroupId);
    bool UnitOfMeasureExists(int unitOfMeasureId);
    bool ServiceProductExists(int productId);
    Task<ProductCatalogSummaryDTO?> GetCatalogSummaryAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<ProductCatalogSummaryDTO?>(null);
    // Verifica se existe um Product com o ID informado e ProductType = Good.
    bool GoodProductExists(int productId);
}
