using Decor.Core.DTOs;

namespace Decor.Core.Validation;

public class OrderDTOValidator : IDTOValidator<OrderDTO>
{
    public IEnumerable<string> Validate(OrderDTO dto)
    {
        var errors = new List<string>();

        if (dto.CustomerID <= 0)
            errors.Add("O cliente do pedido é obrigatório.");

        if (dto.QuoteSectionID <= 0)
            errors.Add("A seção de orçamento do pedido é obrigatória.");

        if (dto.OrderType is < 1 or > 2)
            errors.Add("O tipo de pedido é inválido.");

        if (dto.Status is < 1 or > 7)
            errors.Add("O status do pedido é inválido.");

        return errors;
    }
}
