using System.ComponentModel.DataAnnotations;

namespace Decor.Core.DTOs;

public record QuoteDTO(
    [property: Display(Name = "ID do orçamento")] int QuoteID,
    [property: Display(Name = "Cliente")] int CustomerID,
    [property: Display(Name = "Criado por")] int CreatedByEmployeeID,
    [property: Display(Name = "Parceiro de origem")] int? SourcePartnerID,
    [property: Display(Name = "Origem")] int SourceType,
    [property: Display(Name = "Data de criação")] DateTime CreatedAt,
    [property: Display(Name = "Observações")] string? Notes
);
