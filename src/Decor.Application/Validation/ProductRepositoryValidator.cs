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

        if (!_repository.SubgroupExists(product.SubgroupID))
        {
            errors.Add("O subgrupo informado não existe.");
        }

        return errors;
    }
}
