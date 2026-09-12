using System.ComponentModel.DataAnnotations;

namespace Decor.Core.DTOs;

public record TailorQuotationRevisionDTO(
    [property: Display(Name = "ID da revisão")] int RevisionID,
    [property: Display(Name = "Solicitação")] int RequestID,
    [property: Display(Name = "Número da revisão")] int RevisionNumber,
    [property: Display(Name = "Preço")] decimal Price,
    [property: Display(Name = "Motivo da alteração")] string? ChangeReason,
    [property: Display(Name = "Data da resposta")] DateTime RespondedAt,
    [property: Display(Name = "Registrado por")] int RegisteredByEmployeeID
);
