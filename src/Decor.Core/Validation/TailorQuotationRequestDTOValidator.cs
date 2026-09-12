using Decor.Core.DTOs;

namespace Decor.Core.Validation;

public class TailorQuotationRequestDTOValidator : IDTOValidator<TailorQuotationRequestDTO>
{
    public IEnumerable<string> Validate(TailorQuotationRequestDTO dto)
    {
        var errors = new List<string>();

        if (dto.RequestID < 0)
            errors.Add("O ID da solicitação não pode ser negativo.");

        if (dto.QuoteItemID <= 0)
            errors.Add("O item do orçamento é obrigatório.");

        if (dto.PartnerID <= 0)
            errors.Add("O parceiro é obrigatório.");

        if (dto.RequestedByEmployeeID <= 0)
            errors.Add("O funcionário solicitante é obrigatório.");

        if (dto.Status < 1 || dto.Status > 3)
            errors.Add("Status da solicitação inválido.");

        return errors;
    }
}
