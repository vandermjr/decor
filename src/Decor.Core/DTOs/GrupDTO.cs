using System.ComponentModel.DataAnnotations;

namespace Decor.Core.DTOs;

public record GroupDTO(
    [property: Display(Name = "ID do Grupo")] int GroupID,
    [property: Display(Name = "Nome do Grupo")] string? GroupName
);