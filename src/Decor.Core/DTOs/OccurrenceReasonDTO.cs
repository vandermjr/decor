using System.ComponentModel.DataAnnotations;

namespace Decor.Core.DTOs;

public record OccurrenceReasonDTO(
    [property: Display(Name = "ID do Motivo")] int ReasonID,
    [property: Display(Name = "Descrição")] string Description,
    [property: Display(Name = "Ativo")] bool IsActive
);