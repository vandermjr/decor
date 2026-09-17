using Decor.Core.Entities;
using Decor.Core.Interfaces.Repositories;
using Decor.Core.Validation;

namespace Decor.Application.Validation;
public class ProductRepositoryValidator(IProductRepository Repository) : IRepositoryValidator<Product>
{
    private readonly IProductRepository _repository = Repository;

    public IEnumerable<string> Validate(Product product)
    {
        var errors = new List<string>();

        if (!_repository.BrandExists(product.BrandID))
        {
            errors.Add("A marca informada não existe.");
        }

        if (product.SubgroupID.HasValue && !_repository.SubgroupExists(product.SubgroupID.Value))
        {
            errors.Add("O subgrupo informado não existe.");
        }

        if (product.StockUnitID.HasValue && !_repository.UnitOfMeasureExists(product.StockUnitID.Value))
        {
            errors.Add("A unidade de medida informada não existe.");
        }

        if (product.DefaultInstallationServiceID.HasValue && !_repository.ServiceProductExists(product.DefaultInstallationServiceID.Value))
        {
            errors.Add("O serviço de instalação padrão informado não existe ou não é do tipo Serviço.");
        }

        return errors;
    }
}
