using Decor.Core.Entities;

namespace Decor.Core.Interfaces.Repositories;
public interface IProductRepository : IRepository<Product>
{
    // Verifica se a marca referenciada existe.
    bool BrandExists(int marcaId);
    bool SubgroupExists(int subgroupId);
    // Verifica se existe um Product com o ID informado e ProductType = Service.
    bool ServiceProductExists(int productId);
    // Verifica se existe um Product com o ID informado e ProductType = Good.
    bool GoodProductExists(int productId);
}
