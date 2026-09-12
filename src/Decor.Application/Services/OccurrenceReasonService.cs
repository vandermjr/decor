using System.ComponentModel.DataAnnotations;
using Decor.Application.Mappers;
using Decor.Core.Common;
using Decor.Core.DTOs;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Repositories;
using Decor.Core.Interfaces.Services;
using Decor.Core.Validation;

namespace Decor.Application.Services;

public class OccurrenceReasonService(
    IOccurrenceReasonRepository occurrenceReasonRepository,
    IDTOValidator<OccurrenceReasonDTO> dtoValidator,
    IRepositoryValidator<OccurrenceReason> repoValidator,
    IAuthorizationService authorizationService) : IOccurrenceReasonService
{
    public async Task<IEnumerable<OccurrenceReasonDTO>> SearchOccurrenceReasonsAsync(string searchTerm, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default)
    {
        Require(DecorPermissions.OccurrenceReasonsView);
        return (await occurrenceReasonRepository.SearchGetByAsync(searchTerm, page, pageSize, cancellationToken)).ToDTO();
    }

    public async Task<IEnumerable<OccurrenceReasonDTO>> GetAllOccurrenceReasonsAsync(int page = 1, int pageSize = 100, CancellationToken cancellationToken = default)
    {
        Require(DecorPermissions.OccurrenceReasonsView);
        return (await occurrenceReasonRepository.SearchGetByAsync(null, page, pageSize, cancellationToken)).ToDTO();
    }

    public async Task<OccurrenceReasonDTO> GetOccurrenceReasonByIdAsync(int reasonId, CancellationToken cancellationToken = default)
    {
        Require(DecorPermissions.OccurrenceReasonsView);
        var reason = await occurrenceReasonRepository.GetByIdAsync(reasonId, cancellationToken);
        return reason == null
            ? throw new KeyNotFoundException($"Motivo de ocorrência com ID {reasonId} não encontrado.")
            : reason.ToDTO();
    }

    public async Task SaveOccurrenceReasonAsync(OccurrenceReasonDTO reasonDto, CancellationToken cancellationToken = default)
    {
        Require(reasonDto.ReasonID == 0 ? DecorPermissions.OccurrenceReasonsCreate : DecorPermissions.OccurrenceReasonsEdit);

        var dtoErrors = dtoValidator.Validate(reasonDto);
        if (dtoErrors.Any())
            throw new ValidationException(string.Join("\n", dtoErrors));

        var reason = reasonDto.FromDTO();
        var repoErrors = repoValidator.Validate(reason);
        if (repoErrors.Any())
            throw new ValidationException(string.Join("\n", repoErrors));

        if (await occurrenceReasonRepository.SaveAsync(reason, cancellationToken) != 1)
            throw new InvalidOperationException("Não foi possível salvar o motivo da ocorrência.");
    }

    public async Task DeleteOccurrenceReasonAsync(int reasonId, CancellationToken cancellationToken = default)
    {
        Require(DecorPermissions.OccurrenceReasonsDelete);

        if (await occurrenceReasonRepository.IsInUseAsync(reasonId, cancellationToken))
            throw new ValidationException("Não é possível excluir o motivo da ocorrência pois ele está referenciado no histórico de pedidos.");

        if (await occurrenceReasonRepository.DeleteAsync(reasonId, cancellationToken) != 1)
            throw new InvalidOperationException("O motivo da ocorrência não foi encontrado ou não pôde ser excluído.");
    }

    private void Require(string permission)
    {
        if (!authorizationService.HasPermission(permission))
            throw new UnauthorizedAccessException("Você não possui permissão para esta operação.");
    }
}