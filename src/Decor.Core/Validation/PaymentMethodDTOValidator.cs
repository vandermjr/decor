using Decor.Core.DTOs;

namespace Decor.Core.Validation;

public class PaymentMethodDTOValidator : IDTOValidator<PaymentMethodDTO>
{
    public IEnumerable<string> Validate(PaymentMethodDTO dto)
    {
        var errors = new List<string>();

        if (dto.PaymentMethodID < 0)
        {
            errors.Add("O ID da Forma de Pagamento não pode ser um número negativo.");
        }

        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            errors.Add("O nome da forma de pagamento é obrigatório.");
        }
        else
        {
            if (dto.Name.Length < 2)
                errors.Add("O nome da forma de pagamento deve ter no mínimo 2 caracteres.");

            if (dto.Name.Length > 100)
                errors.Add("O nome da forma de pagamento não pode exceder 100 caracteres.");
        }

        if (!Enum.IsDefined(dto.Timing))
        {
            errors.Add("O timing da forma de pagamento é inválido.");
        }

        return errors;
    }
}
