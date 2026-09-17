using System.ComponentModel.DataAnnotations;

namespace Decor.Core.DTOs;

public record UnitOfMeasureDTO(
    [property: Display(Name = "ID da Unidade de Medida")] int UnitOfMeasureID,
    [property: Display(Name = "Código")] string Code,
    [property: Display(Name = "Descrição")] string Description,
    [property: Display(Name = "Permite fração")] bool AllowsFraction,
    [property: Display(Name = "Ativa")] bool IsActive
);
