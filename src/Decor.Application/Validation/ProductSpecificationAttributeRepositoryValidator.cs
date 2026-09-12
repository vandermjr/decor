using Decor.Core.Entities;
using Decor.Core.Interfaces.Repositories;
using Decor.Core.Validation;

namespace Decor.Application.Validation;
public class ProductSpecificationAttributeRepositoryValidator(IProductSpecificationAttributeRepository Repository) : IRepositoryValidator<ProductSpecificationAttribute>
{
    private readonly IProductSpecificationAttributeRepository _repository = Repository;

    public IEnumerable<string> Validate(ProductSpecificationAttribute attribute)
    {
        var errors = new List<string>();

        // Mesma regra do DTOValidator, garantida também na camada de repositório
        if (attribute.DataType == ProductSpecificationDataType.Enum)
        {
            if (string.IsNullOrWhiteSpace(attribute.EnumOptions))
                errors.Add("As opções (EnumOptions) são obrigatórias quando o Tipo de Dado é Enum.");
        }
        else if (!string.IsNullOrWhiteSpace(attribute.EnumOptions))
        {
            errors.Add("As opções (EnumOptions) só podem ser informadas quando o Tipo de Dado é Enum.");
        }

        return errors;
    }
}
