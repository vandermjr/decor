using System.ComponentModel.DataAnnotations;
using Decor.Core.Common.Attributes;

namespace Decor.Core.DTOs;

public record SubgroupDTO(
    [property: KeyProperty, Display(Name = "ID do Subgrupo")] int SubgroupID,
    [property: Display(Name = "Nome do Subgrupo")] string? SubgroupName
);