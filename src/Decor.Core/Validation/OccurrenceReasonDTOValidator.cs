using Decor.Core.DTOs;

namespace Decor.Core.Validation;

public class OccurrenceReasonDTOValidator : IDTOValidator<OccurrenceReasonDTO>
{
    public IEnumerable<string> Validate(OccurrenceReasonDTO dto)
    {
        var errors = new List<string>();

        if (dto.ReasonID < 0)
            errors.Add("O ID do Motivo da Ocorrência não pode ser negativo.");

        if (string.IsNullOrWhiteSpace(dto.Description))
            errors.Add("A descrição do motivo da ocorrência é obrigatória.");
        else if (dto.Description.Length > 150)
            errors.Add("A descrição do motivo da ocorrência não pode exceder 150 caracteres.");

        return errors;
    }
}