using System.Collections.Generic;
using Decor.Core.DTOs;
using Decor.Core.Validation;

namespace Decor.Application.Validation;

public class UnitOfMeasureDTOValidator : IDTOValidator<UnitOfMeasureDTO>
{
    public IEnumerable<string> Validate(UnitOfMeasureDTO dto)
    {
        var errors = new List<string>();

        if (dto.UnitOfMeasureID < 0)
            errors.Add("O ID da unidade de medida não pode ser menor que zero.");

        if (string.IsNullOrWhiteSpace(dto.Code))
            errors.Add("O código da unidade de medida é obrigatório.");
        else
        {
            var code = dto.Code.Trim();
            if (code.Length > 10)
                errors.Add("O código da unidade de medida não pode exceder 10 caracteres.");
        }

        if (string.IsNullOrWhiteSpace(dto.Description))
            errors.Add("A descrição da unidade de medida é obrigatória.");
        else if (dto.Description.Trim().Length > 100)
            errors.Add("A descrição da unidade de medida não pode exceder 100 caracteres.");

        return errors;
    }
}
