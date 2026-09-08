using System.Collections.Generic;
using Decor.Core.DTOs;

namespace Decor.Core.Validation;
public class BrandDTOValidator : IDTOValidator<BrandDTO>
{
    public IEnumerable<string> Validate(BrandDTO dto)
    {
        var errors = new List<string>();

        // Validação de ID (consistente com o fato de que um ID pode ser 0 para um novo registro)
        if (dto.BrandID < 0)
        {
            errors.Add("O ID da Marca não pode ser um número negativo.");
        }

        // Validação do nome da marca, similar à descrição do produto
        if (string.IsNullOrWhiteSpace(dto.BrandName))
        {
            errors.Add("O nome da marca é obrigatório.");
        }
        else
        {
            if (dto.BrandName.Length < 3)
                errors.Add("O nome da marca deve ter no mínimo 3 caracteres.");

            if (dto.BrandName.Length > 50)
                errors.Add("O nome da marca não pode exceder 50 caracteres.");
        }

        return errors;
    }
}