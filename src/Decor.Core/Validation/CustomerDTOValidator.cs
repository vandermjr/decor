using System.Collections.Generic;
using Decor.Core.DTOs;

namespace Decor.Core.Validation;
public class CustomerDTOValidator : IDTOValidator<CustomerDTO>
{
    public IEnumerable<string> Validate(CustomerDTO dto)
    {
        var errors = new List<string>();

        // Validação de ID (consistente com o fato de que um ID pode ser 0 para um novo registro)
        if (dto.CustomerID < 0)
        {
            errors.Add("O ID do Cliente não pode ser um número negativo.");
        }

        // Validação do nome do cliente
        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            errors.Add("O nome do cliente é obrigatório.");
        }
        else
        {
            if (dto.Name.Length < 3)
                errors.Add("O nome do cliente deve ter no mínimo 3 caracteres.");

            if (dto.Name.Length > 150)
                errors.Add("O nome do cliente não pode exceder 150 caracteres.");
        }

        return errors;
    }
}
