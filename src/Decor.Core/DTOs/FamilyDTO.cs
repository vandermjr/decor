using System.ComponentModel.DataAnnotations;

namespace Decor.Core.DTOs;

public record FamilyDTO(
    [property: Display(Name = "ID da Família")] int FamilyID,
    [property: Display(Name = "Nome da Família")] string? FamilyName
);