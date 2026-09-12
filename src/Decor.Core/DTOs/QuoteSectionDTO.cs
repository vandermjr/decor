using System.ComponentModel.DataAnnotations;

namespace Decor.Core.DTOs;

public record QuoteSectionDTO(
    [property: Display(Name = "ID da seção")] int QuoteSectionID,
    [property: Display(Name = "Orçamento")] int QuoteID,
    [property: Display(Name = "Tipo")] int SectionType,
    [property: Display(Name = "Status")] int Status,
    [property: Display(Name = "Enviado em")] DateTime? SentToCustomerAt,
    [property: Display(Name = "Aprovado em")] DateTime? ApprovedAt,
    [property: Display(Name = "Criado em")] DateTime CreatedAt
);
