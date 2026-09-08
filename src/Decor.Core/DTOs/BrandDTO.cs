using System.ComponentModel.DataAnnotations;

namespace Decor.Core.DTOs;
public record BrandDTO([property: Display(Name = "ID da Marca")] int BrandID,
                       [property: Display(Name = "Nome da Marca")] string? BrandName);

