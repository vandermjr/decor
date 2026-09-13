using System.ComponentModel.DataAnnotations;
using Decor.Core.Common.Attributes;

namespace Decor.Core.DTOs;

public record PartnerPriceTableDTO(
    [property: KeyProperty, Display(Name = "ID da Tabela de Preço")] int PriceTableID,
    [property: Display(Name = "ID do Parceiro")] int PartnerID,
    [property: Display(Name = "ID do Grupo")] int GroupID,
    [property: Display(Name = "Preço por m²")] decimal PricePerSquareMeter,
    [property: Display(Name = "Ativo")] bool IsActive = true);
