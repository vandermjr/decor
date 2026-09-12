using Decor.Core.Entities;
using Decor.Core.Interfaces.Repositories;
using Decor.Core.Validation;

namespace Decor.Application.Validation;

public class ProductKitComponentRepositoryValidator(IProductKitComponentRepository repository, IProductRepository productRepository) : IRepositoryValidator<ProductKitComponent>
{
    private readonly IProductKitComponentRepository _repository = repository;
    private readonly IProductRepository _productRepository = productRepository;

    public IEnumerable<string> Validate(ProductKitComponent component)
    {
        var errors = new List<string>();

        if (!_productRepository.GoodProductExists(component.KitProductID))
            errors.Add("O Produto Kit informado não existe ou não é do tipo Bem (Good).");

        if (!_productRepository.GoodProductExists(component.ComponentProductID))
            errors.Add("O Produto Componente informado não existe ou não é do tipo Bem (Good).");

        // Evita ciclo indireto: o componente já ser Kit que tem o Kit atual como um de seus componentes.
        if (_repository.RelationExists(component.ComponentProductID, component.KitProductID))
            errors.Add("Ciclo detectado: o produto componente já possui o produto kit como um de seus próprios componentes.");

        return errors;
    }
}
