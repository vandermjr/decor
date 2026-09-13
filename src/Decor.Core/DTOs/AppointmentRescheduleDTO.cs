using System.ComponentModel.DataAnnotations;

namespace Decor.Core.DTOs;

public record AppointmentRescheduleDTO(
    [property: Display(Name = "ID do Reagendamento")] int RescheduleID,
    [property: Display(Name = "ID do Agendamento")] int AppointmentID,
    [property: Display(Name = "Data anterior")] DateTime PreviousDate,
    [property: Display(Name = "Nova data")] DateTime NewDate,
    [property: Display(Name = "Motivo")] string Reason,
    [property: Display(Name = "Registrado por")] int RegisteredByEmployeeID,
    [property: Display(Name = "Registrado em")] DateTime RegisteredAt
);