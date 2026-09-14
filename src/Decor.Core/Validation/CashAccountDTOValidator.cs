using Decor.Core.DTOs;

namespace Decor.Core.Validation;

public class CashAccountDTOValidator : IDTOValidator<CashAccountDTO>
{
    public IEnumerable<string> Validate(CashAccountDTO dto)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(dto.Name)) errors.Add("O nome da conta é obrigatório.");
        if (!Enum.IsDefined(dto.AccountType)) errors.Add("O tipo da conta é inválido.");
        return errors;
    }
}