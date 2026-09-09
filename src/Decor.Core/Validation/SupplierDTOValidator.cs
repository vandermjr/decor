using System.Collections.Generic;
using Decor.Core.DTOs;

namespace Decor.Core.Validation;
public class SupplierDTOValidator : IDTOValidator<SupplierDTO>
{
    public IEnumerable<string> Validate(SupplierDTO dto)
    {
        var errors = new List<string>();

        // Validação de ID (consistente com o fato de que um ID pode ser 0 para um novo registro)
        if (dto.SupplierID < 0)
        {
            errors.Add("O ID do Fornecedor não pode ser um número negativo.");
        }

        // Validação da razão social do fornecedor
        if (string.IsNullOrWhiteSpace(dto.CorporateName))
        {
            errors.Add("A razão social do fornecedor é obrigatória.");
        }
        else
        {
            if (dto.CorporateName.Length < 3)
                errors.Add("A razão social do fornecedor deve ter no mínimo 3 caracteres.");

            if (dto.CorporateName.Length > 150)
                errors.Add("A razão social do fornecedor não pode exceder 150 caracteres.");
        }

        return errors;
    }
}
