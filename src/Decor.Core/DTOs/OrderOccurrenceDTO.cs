using System.ComponentModel.DataAnnotations;

namespace Decor.Core.DTOs;

public record OrderOccurrenceDTO(
    [property: Display(Name = "ID da Ocorrência")] int OccurrenceID,
    [property: Display(Name = "ID do Pedido")] int OrderID,
    [property: Display(Name = "ID do Motivo")] int ReasonID,
    [property: Display(Name = "Registrado por")] int RegisteredByEmployeeID,
    [property: Display(Name = "Registrado em")] DateTime RegisteredAt,
    [property: Display(Name = "Observação")] string Observation,
    [property: Display(Name = "Novo prazo de fabricação")] DateTime? NewManufacturingDeadline,
    [property: Display(Name = "Novo prazo de instalação")] DateTime? NewInstallationDeadline
);