using System.ComponentModel.DataAnnotations;
using Decor.Application.Mappers;
using Decor.Core.Common;
using Decor.Core.DTOs;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Repositories;
using Decor.Core.Interfaces.Services;
using Decor.Core.Validation;

namespace Decor.Application.Services;

public class InstallationAppointmentService(IInstallationAppointmentRepository repository, IDTOValidator<InstallationAppointmentDTO> dtoValidator, IRepositoryValidator<InstallationAppointment> repositoryValidator, IAuthorizationService authorizationService) : IInstallationAppointmentService
{
    public async Task<InstallationAppointmentDTO> CreateAppointmentAsync(int orderItemId, DateTime scheduledDate, TimeSpan? scheduledTime, int? executorEmployeeId, int? executorPartnerId, int createdByEmployeeId, CancellationToken cancellationToken = default)
    {
        Require(DecorPermissions.InstallationAppointmentsCreate);
        EnsureSingleExecutor(executorEmployeeId, executorPartnerId);
        if (!await repository.ServiceOrderItemExistsAsync(orderItemId, cancellationToken)) throw new ValidationException("O item informado não é um item de serviço válido.");
        if (await repository.HasActiveConflictAsync(executorEmployeeId, executorPartnerId, scheduledDate, cancellationToken: cancellationToken)) throw new ValidationException("O executor já possui um agendamento ativo para a data informada.");
        var appointment = new InstallationAppointment { OrderItemID = orderItemId, ScheduledDate = scheduledDate.Date, ScheduledTime = scheduledTime, ExecutorEmployeeID = executorEmployeeId, ExecutorPartnerID = executorPartnerId, Status = InstallationAppointmentStatus.Scheduled, CreatedByEmployeeID = createdByEmployeeId, CreatedAt = DateTime.UtcNow };
        Validate(appointment);
        if (await repository.SaveAsync(appointment, cancellationToken) != 1) throw new InvalidOperationException("Não foi possível criar o agendamento.");
        return appointment.ToDTO();
    }

    public async Task<InstallationAppointmentDTO> RescheduleAsync(int appointmentId, DateTime newDate, string reason, int registeredByEmployeeId, CancellationToken cancellationToken = default)
    {
        Require(DecorPermissions.InstallationAppointmentsReschedule);
        if (string.IsNullOrWhiteSpace(reason)) throw new ValidationException("O motivo do reagendamento é obrigatório.");
        var appointment = await repository.GetByIdAsync(appointmentId, cancellationToken) ?? throw new KeyNotFoundException($"Agendamento com ID {appointmentId} não encontrado.");
        if (appointment.Status is not (InstallationAppointmentStatus.Scheduled or InstallationAppointmentStatus.Rescheduled)) throw new ValidationException("Apenas agendamentos programados podem ser reagendados.");
        if (await repository.HasActiveConflictAsync(appointment.ExecutorEmployeeID, appointment.ExecutorPartnerID, newDate, appointmentId, cancellationToken)) throw new ValidationException("O executor já possui um agendamento ativo para a nova data informada.");
        var previousDate = appointment.ScheduledDate;
        appointment.ScheduledDate = newDate.Date;
        appointment.Status = InstallationAppointmentStatus.Rescheduled;
        var reschedule = new AppointmentReschedule { AppointmentID = appointmentId, PreviousDate = previousDate.Date, NewDate = newDate.Date, Reason = reason.Trim(), RegisteredByEmployeeID = registeredByEmployeeId, RegisteredAt = DateTime.UtcNow };
        if (await repository.RescheduleAsync(appointment, reschedule, cancellationToken) != 1) throw new InvalidOperationException("Não foi possível reagendar o atendimento.");
        return appointment.ToDTO();
    }

    public async Task CancelAsync(int appointmentId, CancellationToken cancellationToken = default)
    {
        Require(DecorPermissions.InstallationAppointmentsCancel);
        var appointment = await repository.GetByIdAsync(appointmentId, cancellationToken) ?? throw new KeyNotFoundException($"Agendamento com ID {appointmentId} não encontrado.");
        if (appointment.Status is not (InstallationAppointmentStatus.Scheduled or InstallationAppointmentStatus.Rescheduled)) throw new ValidationException("Apenas agendamentos programados podem ser cancelados.");
        appointment.Status = InstallationAppointmentStatus.Cancelled;
        if (await repository.SaveAsync(appointment, cancellationToken) != 1) throw new InvalidOperationException("Não foi possível cancelar o agendamento.");
    }

    public async Task<IReadOnlyList<InstallationAppointmentDTO>> GetScheduleForExecutorAsync(int? executorEmployeeId, int? executorPartnerId, DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        Require(DecorPermissions.InstallationAppointmentsView);
        EnsureSingleExecutor(executorEmployeeId, executorPartnerId);
        if (from.Date > to.Date) throw new ValidationException("O início do período não pode ser posterior ao fim.");
        return (await repository.GetScheduleForExecutorAsync(executorEmployeeId, executorPartnerId, from, to, cancellationToken)).ToDTO().ToList();
    }

    public async Task<IReadOnlyList<AppointmentRescheduleDTO>> GetRescheduleHistoryAsync(int appointmentId, CancellationToken cancellationToken = default)
    {
        Require(DecorPermissions.InstallationAppointmentsView);
        return (await repository.GetReschedulesAsync(appointmentId, cancellationToken)).ToDTO().ToList();
    }

    private void Validate(InstallationAppointment appointment)
    {
        var dtoErrors = dtoValidator.Validate(appointment.ToDTO());
        if (dtoErrors.Any()) throw new ValidationException(string.Join("\n", dtoErrors));
        var repositoryErrors = repositoryValidator.Validate(appointment);
        if (repositoryErrors.Any()) throw new ValidationException(string.Join("\n", repositoryErrors));
    }

    private static void EnsureSingleExecutor(int? employeeId, int? partnerId)
    {
        if ((employeeId.HasValue ? 1 : 0) + (partnerId.HasValue ? 1 : 0) != 1) throw new ValidationException("Informe exatamente um executor: funcionário ou parceiro.");
    }

    private void Require(string permission)
    {
        if (!authorizationService.HasPermission(permission)) throw new UnauthorizedAccessException("Você não possui permissão para esta operação.");
    }
}