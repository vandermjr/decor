using Decor.Core.DTOs;

namespace Decor.Core.Validation;

public class TailorQuotationRevisionDTOValidator : IDTOValidator<TailorQuotationRevisionDTO>
{
    public IEnumerable<string> Validate(TailorQuotationRevisionDTO dto)
    {
        var errors = new List<string>();

        if (dto.RevisionID < 0)
            errors.Add("O ID da revisão não pode ser negativo.");

        if (dto.RequestID <= 0)
            errors.Add("A solicitação é obrigatória.");

        if (dto.Price <= 0)
            errors.Add("O preço da revisão deve ser maior que zero.");

        if (dto.RegisteredByEmployeeID <= 0)
            errors.Add("O funcionário responsável pelo registro é obrigatório.");

        return errors;
    }
}
