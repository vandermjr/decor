using Decor.Core.DTOs;

namespace Decor.Core.Validation;

public class OrderInstallmentDTOValidator : IDTOValidator<OrderInstallmentDTO>
{
    public IEnumerable<string> Validate(OrderInstallmentDTO dto)
    {
        var errors = new List<string>();

        if (dto.InstallmentID < 0)
            errors.Add("O ID da Parcela não pode ser um número negativo.");

        if (dto.OrderID <= 0)
            errors.Add("O ID do Pedido deve ser maior que zero.");

        if (dto.PaymentMethodID <= 0)
            errors.Add("A Forma de Pagamento deve ser informada.");

        if (dto.InstallmentNumber <= 0)
            errors.Add("O Número da Parcela deve ser maior que zero.");

        if (dto.Amount <= 0m)
            errors.Add("O Valor da parcela deve ser maior que zero.");

        if (!Enum.IsDefined(dto.Status))
            errors.Add("O Status da parcela é inválido.");

        return errors;
    }
}
