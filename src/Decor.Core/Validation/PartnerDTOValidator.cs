using System.Collections.Generic;
using Decor.Core.DTOs;

namespace Decor.Core.Validation;
public class PartnerDTOValidator : IDTOValidator<PartnerDTO>
{
    public IEnumerable<string> Validate(PartnerDTO dto)
    {
        var errors = new List<string>();

        // Validação de ID (consistente com o fato de que um ID pode ser 0 para um novo registro)
        if (dto.PartnerID < 0)
        {
            errors.Add("O ID do Parceiro não pode ser um número negativo.");
        }

        // Validação do nome do parceiro
        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            errors.Add("O nome do parceiro é obrigatório.");
        }
        else
        {
            if (dto.Name.Length < 3)
                errors.Add("O nome do parceiro deve ter no mínimo 3 caracteres.");

            if (dto.Name.Length > 150)
                errors.Add("O nome do parceiro não pode exceder 150 caracteres.");
        }

        return errors;
    }
}
