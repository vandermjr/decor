using Decor.Core.Common;
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

        if (dto.SourceType is < 1 or > 3)
            errors.Add("O tipo de origem do orçamento é inválido.");

        if (dto.DiscountAmount < 0)
            errors.Add("O desconto do orçamento não pode ser negativo.");

        if (!QuoteNotesRules.Fits(dto.Notes))
            errors.Add($"As observações do orçamento não podem exceder {QuoteNotesRules.MaximumBytes} bytes em UTF-8.");

        var items = dto.Sections?.SelectMany(section => section.Items ?? []).ToArray();
        if (items is { Length: > 0 } && dto.DiscountAmount > items.Sum(item => item.Quantity * (item.UnitPrice ?? 0m)))
            errors.Add("O desconto do orçamento não pode exceder o subtotal dos itens.");

        return errors;
    }
}
