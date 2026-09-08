using System.ComponentModel.DataAnnotations;

namespace Decor.Core.DTOs;

public record ClassDTO(
    [property: Display(Name = "ID da Classe")] int ClassID,
    [property: Display(Name = "Nome da Classe")] string? ClassName
);