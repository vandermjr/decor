using System.ComponentModel.DataAnnotations;

namespace Decor.Core.DTOs;

public record QuoteDTO(
    [property: Display(Name = "ID do orçamento")] int QuoteID,
    [property: Display(Name = "Cliente")] int CustomerID,
    [property: Display(Name = "Criado por")] int CreatedByEmployeeID,
    [property: Display(Name = "Parceiro de origem")] int? SourcePartnerID,
    [property: Display(Name = "Origem")] int SourceType,
    [property: Display(Name = "Data de criação")] DateTime CreatedAt,
    [property: Display(Name = "Observações")] string? Notes,
    [property: Display(Name = "Seções")] IReadOnlyList<QuoteSectionDTO>? Sections = null,
    [property: Display(Name = "Usuário criador")] int? CreatedByUserID = null,
    [property: Display(Name = "Desconto")] decimal DiscountAmount = 0m,
    [property: Display(Name = "Cliente")] string? CustomerName = null,
    [property: Display(Name = "Criado por")] string? CreatedByEmployeeName = null,
    [property: Display(Name = "Estado")] string? ListStatus = null,
    [property: Display(Name = "Valor total")] decimal? ListTotal = null
);
