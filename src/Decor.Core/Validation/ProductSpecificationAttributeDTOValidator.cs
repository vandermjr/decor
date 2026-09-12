using System.Collections.Generic;
using Decor.Core.DTOs;
using Decor.Core.Entities;

namespace Decor.Core.Validation;
public class ProductSpecificationAttributeDTOValidator : IDTOValidator<ProductSpecificationAttributeDTO>
{
    public IEnumerable<string> Validate(ProductSpecificationAttributeDTO dto)
    {
        var errors = new List<string>();

        if (dto.AttributeID < 0)
            errors.Add("O ID do Atributo não pode ser um número negativo.");

        if (dto.ProductCategoryID <= 0)
            errors.Add("A categoria do produto é obrigatória.");

        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            errors.Add("O nome do atributo é obrigatório.");
        }
        else
        {
            if (dto.Name.Length < 2)
                errors.Add("O nome do atributo deve ter no mínimo 2 caracteres.");

            if (dto.Name.Length > 100)
                errors.Add("O nome do atributo não pode exceder 100 caracteres.");
        }

        if (dto.DisplayOrder < 0)
            errors.Add("A ordem de exibição não pode ser negativa.");

        // DataType = Enum exige EnumOptions preenchido; nos demais DataTypes, EnumOptions deve ser nulo
        if (dto.DataType == ProductSpecificationDataType.Enum)
        {
            if (string.IsNullOrWhiteSpace(dto.EnumOptions))
                errors.Add("As opções (EnumOptions) são obrigatórias quando o Tipo de Dado é Enum.");
        }
        else if (!string.IsNullOrWhiteSpace(dto.EnumOptions))
        {
            errors.Add("As opções (EnumOptions) só podem ser informadas quando o Tipo de Dado é Enum.");
        }

        return errors;
    }
}
