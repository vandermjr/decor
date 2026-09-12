namespace Decor.Core.DTOs;

// Projeção usada por ProductKitComponentService.GetSuggestedKitPriceAsync (não persistida).
public record KitComponentPricingDTO(decimal Quantity, decimal? SalePrice);
