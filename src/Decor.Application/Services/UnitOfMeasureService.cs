using System.ComponentModel.DataAnnotations;
using Decor.Application.Mappers;
using Decor.Core.Common;
using Decor.Core.DTOs;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Repositories;
using Decor.Core.Interfaces.Services;
using Decor.Core.Validation;

namespace Decor.Application.Services;

public sealed class UnitOfMeasureService(
    IUnitOfMeasureRepository unitOfMeasureRepository,
    IDTOValidator<UnitOfMeasureDTO> dtoValidator,
    IRepositoryValidator<UnitOfMeasure> repoValidator,
    IAuthorizationService authorizationService) : IUnitOfMeasureService
{
    private readonly IUnitOfMeasureRepository _unitOfMeasureRepository = unitOfMeasureRepository;
    private readonly IDTOValidator<UnitOfMeasureDTO> _dtoValidator = dtoValidator;
    private readonly IRepositoryValidator<UnitOfMeasure> _repoValidator = repoValidator;
    private readonly IAuthorizationService _authorizationService = authorizationService;

    public async Task<IEnumerable<UnitOfMeasureDTO>> GetAllAsync(int page = 1, int pageSize = 100, CancellationToken cancellationToken = default)
    {
        Require(DecorPermissions.UnitsOfMeasureView);
        var items = await _unitOfMeasureRepository.SearchGetByAsync(null, page, pageSize, cancellationToken);
        return items.ToDTO();
    }

    public async Task<IEnumerable<UnitOfMeasureDTO>> SearchAsync(string searchTerm, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default)
    {
        Require(DecorPermissions.UnitsOfMeasureView);
        var items = await _unitOfMeasureRepository.SearchGetByAsync(searchTerm, page, pageSize, cancellationToken);
        return items.ToDTO();
    }

    public async Task<UnitOfMeasureDTO> GetByIdAsync(int unitOfMeasureId, CancellationToken cancellationToken = default)
    {
        Require(DecorPermissions.UnitsOfMeasureView);
        var item = await _unitOfMeasureRepository.GetByIdAsync(unitOfMeasureId, cancellationToken)
            ?? throw new KeyNotFoundException($"Unidade de medida {unitOfMeasureId} não encontrada.");
        return item.ToDTO();
    }

    public async Task SaveUnitOfMeasureAsync(UnitOfMeasureDTO unitOfMeasure, CancellationToken cancellationToken = default)
    {
        Require(unitOfMeasure.UnitOfMeasureID == 0 ? DecorPermissions.UnitsOfMeasureCreate : DecorPermissions.UnitsOfMeasureEdit);

        var dtoErrors = _dtoValidator.Validate(unitOfMeasure);
        if (dtoErrors.Any())
            throw new ValidationException(string.Join("\n", dtoErrors));

        var existing = unitOfMeasure.UnitOfMeasureID == 0 ? null : await _unitOfMeasureRepository.GetByIdAsync(unitOfMeasure.UnitOfMeasureID, cancellationToken);
        var entity = unitOfMeasure.FromDTO();
        if (existing is not null && unitOfMeasure.IsActive != true)
        {
            entity.IsActive = existing.IsActive;
        }

        var repoErrors = _repoValidator.Validate(entity);
        if (repoErrors.Any())
            throw new ValidationException(string.Join("\n", repoErrors));

        var affectedRows = await _unitOfMeasureRepository.SaveAsync(entity, cancellationToken);
        if (affectedRows != 1)
            throw new InvalidOperationException("Não foi possível salvar a unidade de medida.");
    }

    public async Task ActivateAsync(int unitOfMeasureId, CancellationToken cancellationToken = default)
    {
        Require(DecorPermissions.UnitsOfMeasureEdit);
        if (await _unitOfMeasureRepository.ActivateAsync(unitOfMeasureId, cancellationToken) != 1)
            throw new KeyNotFoundException($"Unidade de medida {unitOfMeasureId} não encontrada.");
    }

    public async Task DeactivateAsync(int unitOfMeasureId, CancellationToken cancellationToken = default)
    {
        Require(DecorPermissions.UnitsOfMeasureDeactivate);
        if (await _unitOfMeasureRepository.DeactivateAsync(unitOfMeasureId, cancellationToken) != 1)
            throw new KeyNotFoundException($"Unidade de medida {unitOfMeasureId} não encontrada.");
    }

    private void Require(string permission)
    {
        if (!_authorizationService.HasPermission(permission))
            throw new UnauthorizedAccessException("Você não possui permissão para esta operação.");
    }
}
