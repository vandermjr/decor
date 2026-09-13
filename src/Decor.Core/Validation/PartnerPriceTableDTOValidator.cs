using Decor.Core.DTOs;

namespace Decor.Core.Validation;

public class PartnerPriceTableDTOValidator : IDTOValidator<PartnerPriceTableDTO>
{
    public IEnumerable<string> Validate(PartnerPriceTableDTO dto)
    {
        var errors = new List<string>();

        if (dto.PriceTableID < 0)
            errors.Add("O ID da Tabela de Preço não pode ser um número negativo.");

        if (dto.PartnerID <= 0)
            errors.Add("O parceiro é obrigatório.");

        if (dto.GroupID <= 0)
            errors.Add("O grupo é obrigatório.");

        if (dto.PricePerSquareMeter <= 0)
            errors.Add("O preço por m² deve ser maior que zero.");

        return errors;
    }
}
