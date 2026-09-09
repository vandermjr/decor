using System.Collections.Generic;
using Decor.Core.DTOs;

namespace Decor.Core.Validation;
public class EmployeeDTOValidator : IDTOValidator<EmployeeDTO>
{
    public IEnumerable<string> Validate(EmployeeDTO dto)
    {
        var errors = new List<string>();

        // Validação de ID (consistente com o fato de que um ID pode ser 0 para um novo registro)
        if (dto.EmployeeID < 0)
        {
            errors.Add("O ID do Funcionário não pode ser um número negativo.");
        }

        // Validação do nome do funcionário
        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            errors.Add("O nome do funcionário é obrigatório.");
        }
        else
        {
            if (dto.Name.Length < 3)
                errors.Add("O nome do funcionário deve ter no mínimo 3 caracteres.");

            if (dto.Name.Length > 150)
                errors.Add("O nome do funcionário não pode exceder 150 caracteres.");
        }

        return errors;
    }
}
