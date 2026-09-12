using System.ComponentModel.DataAnnotations;

namespace Decor.Core.DTOs;

public record TailorQuotationRequestDTO(
    [property: Display(Name = "ID da solicitação")] int RequestID,
    [property: Display(Name = "Item do orçamento")] int QuoteItemID,
    [property: Display(Name = "Parceiro")] int PartnerID,
    [property: Display(Name = "Solicitado por")] int RequestedByEmployeeID,
    [property: Display(Name = "Data de solicitação")] DateTime RequestedAt,
    [property: Display(Name = "Prazo limite")] DateTime? Deadline,
    [property: Display(Name = "Status")] int Status
);
