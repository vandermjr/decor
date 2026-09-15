using Decor.Core.DTOs;

namespace Decor.Core.Validation;

public class AccountsPayableDTOValidator : IDTOValidator<AccountsPayableDTO>
{
    public IEnumerable<string> Validate(AccountsPayableDTO dto)
    {
        var errors = new List<string>();
        if (dto.AccountsPayableID < 0) errors.Add("O ID da conta a pagar não pode ser negativo.");
        if (!Enum.IsDefined(dto.PayeeType)) errors.Add("O tipo do favorecido é inválido.");
        if (dto.PayeeID <= 0) errors.Add("O ID do favorecido deve ser maior que zero.");
        if (string.IsNullOrWhiteSpace(dto.Description)) errors.Add("A descrição deve ser informada.");
        if (dto.Amount <= 0m) errors.Add("O valor deve ser maior que zero.");
        if (!Enum.IsDefined(dto.Status)) errors.Add("O status é inválido.");
        if (dto.SourceType.HasValue != dto.SourceID.HasValue)
            errors.Add("Tipo e ID da origem devem ser informados juntos.");
        if (dto.SourceID is <= 0) errors.Add("O ID da origem deve ser maior que zero.");
        if (dto.CreatedByEmployeeID <= 0) errors.Add("O funcionário criador deve ser informado.");
        return errors;
    }
}