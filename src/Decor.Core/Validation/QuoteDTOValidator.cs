using Decor.Core.DTOs;

namespace Decor.Core.Validation;

public class QuoteDTOValidator : IDTOValidator<QuoteDTO>
{
    public IEnumerable<string> Validate(QuoteDTO dto)
    {
        var errors = new List<string>();

        if (dto.CustomerID <= 0)
            errors.Add("O cliente do orçamento é obrigatório.");

        if (dto.CreatedByEmployeeID <= 0)
            errors.Add("O funcionário responsável pela criação do orçamento é obrigatório.");

        if (dto.SourceType is < 1 or > 2)
            errors.Add("O tipo de origem do orçamento é inválido.");

        return errors;
    }
}
