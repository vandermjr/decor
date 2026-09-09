using System.ComponentModel.DataAnnotations;
using Decor.Core.Entities;

namespace Decor.Core.DTOs;
public record StockLocationDTO([property: Display(Name = "ID do Depósito")] int StockLocationID,
                               [property: Display(Name = "Nome")] string? Name,
                               [property: Display(Name = "Tipo de Local")] StockLocationType LocationType,
                               [property: Display(Name = "ID do Parceiro")] int? PartnerID,
                               [property: Display(Name = "Ativo")] bool IsActive);
