using System.ComponentModel.DataAnnotations;
using Decor.Application.Mappers;
using Decor.Core.Common;
using Decor.Core.DTOs;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Repositories;
using Decor.Core.Interfaces.Services;
using Decor.Core.Validation;

namespace Decor.Application.Services;

public class OrderOccurrenceService(
    IOrderOccurrenceRepository occurrenceRepository,
    IOccurrenceReasonRepository reasonRepository,
    IOrderRepository orderRepository,
    IEmployeeRepository employeeRepository,
    IDTOValidator<OrderOccurrenceDTO> dtoValidator,
    IRepositoryValidator<OrderOccurrence> repoValidator,
    IAuthorizationService authorizationService) : IOrderOccurrenceService
{
    public async Task<OrderOccurrenceDTO> RegisterOccurrenceAsync(int orderId, int reasonId, int registeredByEmployeeId, string observation, DateTime? newManufacturingDeadline = null, DateTime? newInstallationDeadline = null, CancellationToken cancellationToken = default)
    {
        Require(DecorPermissions.OrderOccurrencesRegister);

        var order = await orderRepository.GetByIdAsync(orderId, cancellationToken)
            ?? throw new KeyNotFoundException($"Pedido com ID {orderId} não encontrado.");

        if (order.Status == OrderStatus.Cancelled)
            throw new ValidationException("Não é possível registrar ocorrência em um pedido cancelado.");

        var reason = await reasonRepository.GetByIdAsync(reasonId, cancellationToken)
            ?? throw new KeyNotFoundException($"Motivo de ocorrência com ID {reasonId} não encontrado.");

        if (!reason.IsActive)
            throw new ValidationException("O motivo da ocorrência informado está inativo.");

        var employees = await employeeRepository.SearchGetByAsync(registeredByEmployeeId.ToString(), 1, 1, cancellationToken);
        if (!employees.Any(e => e.EmployeeID == registeredByEmployeeId))
            throw new KeyNotFoundException($"Funcionário com ID {registeredByEmployeeId} não encontrado.");

        var occurrence = new OrderOccurrence
        {
            OrderID = orderId,
            ReasonID = reasonId,
            RegisteredByEmployeeID = registeredByEmployeeId,
            RegisteredAt = DateTime.UtcNow,
            Observation = observation,
            NewManufacturingDeadline = newManufacturingDeadline,
            NewInstallationDeadline = newInstallationDeadline
        };

        var occurrenceDto = occurrence.ToDTO();
        var dtoErrors = dtoValidator.Validate(occurrenceDto);
        if (dtoErrors.Any())
            throw new ValidationException(string.Join("\n", dtoErrors));

        var repoErrors = repoValidator.Validate(occurrence);
        if (repoErrors.Any())
            throw new ValidationException(string.Join("\n", repoErrors));

        if (newManufacturingDeadline.HasValue)
            order.ManufacturingDeadline = newManufacturingDeadline;
        if (newInstallationDeadline.HasValue)
            order.InstallationDeadline = newInstallationDeadline;

        if (await occurrenceRepository.RegisterAsync(occurrence, order, cancellationToken) != 1)
            throw new InvalidOperationException("Não foi possível registrar a ocorrência do pedido.");

        return occurrence.ToDTO();
    }

    public async Task<IReadOnlyList<OrderOccurrenceDTO>> GetHistoryForOrderAsync(int orderId, CancellationToken cancellationToken = default)
    {
        Require(DecorPermissions.OrderOccurrencesView);
        var occurrences = await occurrenceRepository.GetHistoryForOrderAsync(orderId, cancellationToken);
        return occurrences.Select(o => o.ToDTO()).ToList();
    }

    private void Require(string permission)
    {
        if (!authorizationService.HasPermission(permission))
            throw new UnauthorizedAccessException("Você não possui permissão para esta operação.");
    }
}