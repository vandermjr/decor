using Decor.Core.Entities;

namespace Decor.Core.Interfaces.Repositories;
public interface IProductRepository : IRepository<Product>
{
    // Verifica se a marca referenciada existe.
    bool BrandExists(int marcaId);
    bool SubgroupExists(int subgroupId);
}
