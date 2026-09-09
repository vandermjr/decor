using System.Collections.Generic;
using Decor.Core.DTOs;

namespace Decor.Core.Validation;
public class StockLocationDTOValidator : IDTOValidator<StockLocationDTO>
{
    public IEnumerable<string> Validate(StockLocationDTO dto)
    {
        var errors = new List<string>();

        // Validação de ID (consistente com o fato de que um ID pode ser 0 para um novo registro)
        if (dto.StockLocationID < 0)
        {
            errors.Add("O ID do Depósito não pode ser um número negativo.");
        }

        // Validação do nome do depósito
        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            errors.Add("O nome do depósito é obrigatório.");
        }
        else
        {
            if (dto.Name.Length < 3)
                errors.Add("O nome do depósito deve ter no mínimo 3 caracteres.");

            if (dto.Name.Length > 150)
                errors.Add("O nome do depósito não pode exceder 150 caracteres.");
        }

        return errors;
    }
}
