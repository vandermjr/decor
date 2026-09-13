using System.ComponentModel.DataAnnotations;

namespace Decor.Core.DTOs;

public record ServiceExecutionRecordDTO(
    [property: Display(Name = "ID da execução")] int ExecutionID,
    [property: Display(Name = "ID do agendamento")] int AppointmentID,
    [property: Display(Name = "Executado em")] DateTime ExecutedAt,
    [property: Display(Name = "Observações")] string? Observations,
    [property: Display(Name = "Cliente presente")] bool CustomerPresent,
    [property: Display(Name = "Confirmação assinada pelo cliente")] bool? CustomerSignedConfirmation,
    [property: Display(Name = "Autorização de ausência")] string? AbsentAuthorizationNote
);
