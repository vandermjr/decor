using System.ComponentModel.DataAnnotations;
using Decor.Application.Mappers;
using Decor.Core.Common;
using Decor.Core.DTOs;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Repositories;
using Decor.Core.Interfaces.Services;
using Decor.Core.Validation;

namespace Decor.Application.Services;

public class ServiceExecutionRecordService(
    IServiceExecutionRecordRepository repository,
    IInstallationAppointmentRepository appointmentRepository,
    IInstallationAppointmentService appointmentService,
    IDTOValidator<ServiceExecutionRecordDTO> dtoValidator,
    IRepositoryValidator<ServiceExecutionRecord> repositoryValidator,
    IAuthorizationService authorizationService) : IServiceExecutionRecordService
{
    public async Task<ServiceExecutionRecordDTO> RegisterExecutionAsync(
        int appointmentId,
        DateTime executedAt,
        string? observations,
        bool customerPresent,
        bool? customerSignedConfirmation,
        string? absentAuthorizationNote,
        CancellationToken cancellationToken = default)
    {
        Require(DecorPermissions.ServiceExecutionRecordsCreate);
        var appointment = await appointmentRepository.GetByIdAsync(appointmentId, cancellationToken)
            ?? throw new KeyNotFoundException($"Agendamento com ID {appointmentId} não encontrado.");
        if (appointment.Status is not (InstallationAppointmentStatus.Scheduled or InstallationAppointmentStatus.Rescheduled))
            throw new ValidationException("Apenas agendamentos programados podem ser executados.");
        if (await repository.GetByAppointmentIdAsync(appointmentId, cancellationToken) is not null)
            throw new ValidationException("O agendamento já possui um registro de execução.");
        if (customerPresent)
        {
            if (!customerSignedConfirmation.HasValue)
                throw new ValidationException("Informe explicitamente se o cliente assinou a confirmação.");
            if (!string.IsNullOrWhiteSpace(absentAuthorizationNote))
                throw new ValidationException("A autorização de ausência não se aplica quando o cliente está presente.");
        }
        else
        {
            if (string.IsNullOrWhiteSpace(absentAuthorizationNote))
                throw new ValidationException("A autorização de ausência é obrigatória quando o cliente não está presente.");
            if (customerSignedConfirmation.HasValue)
                throw new ValidationException("A confirmação assinada não se aplica quando o cliente está ausente.");
        }

        var entity = new ServiceExecutionRecord
        {
            AppointmentID = appointmentId,
            ExecutedAt = executedAt,
            Observations = string.IsNullOrWhiteSpace(observations) ? null : observations.Trim(),
            CustomerPresent = customerPresent,
            CustomerSignedConfirmation = customerPresent ? customerSignedConfirmation : null,
            AbsentAuthorizationNote = customerPresent ? null : absentAuthorizationNote!.Trim()
        };
        var dtoErrors = dtoValidator.Validate(entity.ToDTO());
        if (dtoErrors.Any()) throw new ValidationException(string.Join("\n", dtoErrors));
        var repositoryErrors = repositoryValidator.Validate(entity);
        if (repositoryErrors.Any()) throw new ValidationException(string.Join("\n", repositoryErrors));
        if (await repository.SaveAsync(entity, cancellationToken) != 1)
            throw new InvalidOperationException("Não foi possível registrar a execução do serviço.");

        await appointmentService.CompleteAsync(appointmentId, cancellationToken);
        return entity.ToDTO();
    }

    public async Task<ServiceExecutionRecordDTO?> GetExecutionForAppointmentAsync(int appointmentId, CancellationToken cancellationToken = default)
    {
        Require(DecorPermissions.ServiceExecutionRecordsView);
        return (await repository.GetByAppointmentIdAsync(appointmentId, cancellationToken))?.ToDTO();
    }

    private void Require(string permission)
    {
        if (!authorizationService.HasPermission(permission))
            throw new UnauthorizedAccessException("Você não possui permissão para esta operação.");
    }
}
