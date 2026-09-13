using System.ComponentModel.DataAnnotations;
using Decor.Core.Entities;

namespace Decor.Core.DTOs;

public record InstallationAppointmentDTO(
    [property: Display(Name = "ID do Agendamento")] int AppointmentID,
    [property: Display(Name = "ID do Item do Pedido")] int OrderItemID,
    [property: Display(Name = "Data agendada")] DateTime ScheduledDate,
    [property: Display(Name = "Horário agendado")] TimeSpan? ScheduledTime,
    [property: Display(Name = "Funcionário executor")] int? ExecutorEmployeeID,
    [property: Display(Name = "Parceiro executor")] int? ExecutorPartnerID,
    [property: Display(Name = "Status")] InstallationAppointmentStatus Status,
    [property: Display(Name = "Criado por")] int CreatedByEmployeeID,
    [property: Display(Name = "Criado em")] DateTime CreatedAt
);