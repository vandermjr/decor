using Decor.Core.DTOs;
using Decor.Core.Entities;

namespace Decor.Core.Interfaces.Repositories;
public interface IProductKitComponentRepository : IRepository<ProductKitComponent>
{
    Task<IEnumerable<ProductKitComponent>> GetByKitProductIdAsync(int kitProductId, CancellationToken cancellationToken = default);
    // Projeção de Quantity + Product.SalePrice usada para calcular o preço sugerido do kit.
    Task<IEnumerable<KitComponentPricingDTO>> GetPricingByKitProductIdAsync(int kitProductId, CancellationToken cancellationToken = default);
    // Verifica se já existe uma relação (kitProductId, componentProductId) cadastrada — usado para detectar ciclo indireto.
    bool RelationExists(int kitProductId, int componentProductId);
}
