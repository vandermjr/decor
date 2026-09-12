using System.Collections.Generic;
using Decor.Core.DTOs;

namespace Decor.Core.Validation;

public class StockReservationDTOValidator : IDTOValidator<StockReservationDTO>
{
    public IEnumerable<string> Validate(StockReservationDTO dto)
    {
        var errors = new List<string>();

        if (dto.ReservationID < 0)
            errors.Add("O ID da Reserva não pode ser um número negativo.");

        if (dto.OrderItemID <= 0)
            errors.Add("O ID do item do pedido é obrigatório.");

        if (dto.ProductID <= 0)
            errors.Add("O ID do produto é obrigatório.");

        if (dto.StockLocationID <= 0)
            errors.Add("O ID do depósito é obrigatório.");

        if (dto.Quantity <= 0)
            errors.Add("A quantidade da reserva deve ser maior que zero.");

        if (dto.CreatedByEmployeeID <= 0)
            errors.Add("O funcionário criador da reserva é obrigatório.");

        return errors;
    }
}
