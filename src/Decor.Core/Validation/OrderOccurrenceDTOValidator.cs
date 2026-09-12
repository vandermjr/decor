using Decor.Core.DTOs;

namespace Decor.Core.Validation;

public class OrderOccurrenceDTOValidator : IDTOValidator<OrderOccurrenceDTO>
{
    public IEnumerable<string> Validate(OrderOccurrenceDTO dto)
    {
        var errors = new List<string>();

        if (dto.OccurrenceID < 0)
            errors.Add("O ID da Ocorrência não pode ser negativo.");
        if (dto.OrderID <= 0)
            errors.Add("O pedido da ocorrência é obrigatório.");
        if (dto.ReasonID <= 0)
            errors.Add("O motivo da ocorrência é obrigatório.");
        if (dto.RegisteredByEmployeeID <= 0)
            errors.Add("O funcionário responsável pelo registro é obrigatório.");
        if (string.IsNullOrWhiteSpace(dto.Observation))
            errors.Add("A observação da ocorrência é obrigatória.");

        return errors;
    }
}