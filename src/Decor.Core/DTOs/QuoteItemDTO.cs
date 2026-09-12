using System.ComponentModel.DataAnnotations;

namespace Decor.Core.DTOs;

public record QuoteItemDTO(
    [property: Display(Name = "ID do item")] int QuoteItemID,
    [property: Display(Name = "Seção")] int QuoteSectionID,
    [property: Display(Name = "Produto")] int ProductID,
    [property: Display(Name = "Quantidade")] decimal Quantity,
    [property: Display(Name = "Preço unitário")] decimal? UnitPrice,
    [property: Display(Name = "Instalação")] bool HasInstallationService
);
