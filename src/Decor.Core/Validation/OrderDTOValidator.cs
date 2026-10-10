using Decor.Core.Common;
using Decor.Core.DTOs;

namespace Decor.Core.Validation;

public class OrderDTOValidator : IDTOValidator<OrderDTO>
{
    public IEnumerable<string> Validate(OrderDTO dto)
    {
        var errors = new List<string>();

        if (dto.Items?.Any(item => !CommercialItemReference.IsValid(item.ProductID, item.ServiceID)) == true)
            errors.Add(CommercialItemReference.ValidationMessage);

        if (dto.CustomerID <= 0)
            errors.Add("O cliente do pedido é obrigatório.");

        if (dto.QuoteSectionID <= 0)
            errors.Add("A seção de orçamento do pedido é obrigatória.");

        if (dto.OrderType is < 1 or > 2)
            errors.Add("O tipo de pedido é inválido.");

        if (dto.Status is < 1 or > 7)
            errors.Add("O status do pedido é inválido.");

        if (dto.Status == (int)Entities.OrderStatus.Cancelled && string.IsNullOrWhiteSpace(dto.CancellationReason))
            errors.Add("Informe o motivo do cancelamento do pedido.");
        if (dto.CancellationReason?.Length > 500)
            errors.Add("O motivo do cancelamento deve ter no máximo 500 caracteres.");

        return errors;
    }
}
