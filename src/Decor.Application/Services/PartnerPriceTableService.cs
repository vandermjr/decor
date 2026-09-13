using System.ComponentModel.DataAnnotations;
using Decor.Application.Mappers;
using Decor.Core.Common;
using Decor.Core.DTOs;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Repositories;
using Decor.Core.Interfaces.Services;
using Decor.Core.Validation;

namespace Decor.Application.Services;

public class PartnerPriceTableService(
    IPartnerPriceTableRepository partnerPriceTableRepository,
    IDTOValidator<PartnerPriceTableDTO> dtoValidator,
    IRepositoryValidator<PartnerPriceTable> repoValidator,
    IAuthorizationService authorizationService) : IPartnerPriceTableService
{
    private readonly IPartnerPriceTableRepository _partnerPriceTableRepository = partnerPriceTableRepository;
    private readonly IDTOValidator<PartnerPriceTableDTO> _dtoValidator = dtoValidator;
    private readonly IRepositoryValidator<PartnerPriceTable> _repoValidator = repoValidator;
    private readonly IAuthorizationService _authorizationService = authorizationService;

    public async Task<IEnumerable<PartnerPriceTableDTO>> SearchPartnerPriceTablesAsync(string searchTerm, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default)
    {
        Require(DecorPermissions.PartnerPriceTablesView);
        var tables = await _partnerPriceTableRepository.SearchGetByAsync(searchTerm, page, pageSize, cancellationToken);
        return tables.ToDTO();
    }

    public async Task<IEnumerable<PartnerPriceTableDTO>> GetAllPartnerPriceTablesAsync(int page = 1, int pageSize = 100, CancellationToken cancellationToken = default)
    {
        Require(DecorPermissions.PartnerPriceTablesView);
        var tables = await _partnerPriceTableRepository.SearchGetByAsync(null, page, pageSize, cancellationToken);
        return tables.ToDTO();
    }

    public async Task<PartnerPriceTableDTO> GetPartnerPriceTableByIdAsync(int priceTableId, CancellationToken cancellationToken = default)
    {
        Require(DecorPermissions.PartnerPriceTablesView);
        var table = await _partnerPriceTableRepository.GetByIdAsync(priceTableId, cancellationToken);
        return table == null
            ? throw new KeyNotFoundException($"Tabela de preço com ID {priceTableId} não encontrada.")
            : table.ToDTO();
    }

    public async Task<IEnumerable<PartnerPriceTableDTO>> GetByPartnerIdAsync(int partnerId, CancellationToken cancellationToken = default)
    {
        Require(DecorPermissions.PartnerPriceTablesView);
        var tables = await _partnerPriceTableRepository.GetByPartnerIdAsync(partnerId, cancellationToken);
        return tables.ToDTO();
    }

    public async Task<PartnerPriceTableDTO?> GetActiveByPartnerAndGroupAsync(int partnerId, int groupId, CancellationToken cancellationToken = default)
    {
        Require(DecorPermissions.PartnerPriceTablesView);
        var table = await _partnerPriceTableRepository.GetActiveByPartnerAndGroupAsync(partnerId, groupId, cancellationToken);
        return table?.ToDTO();
    }

    public async Task SavePartnerPriceTableAsync(PartnerPriceTableDTO dto, CancellationToken cancellationToken = default)
    {
        Require(dto.PriceTableID == 0 ? DecorPermissions.PartnerPriceTablesCreate : DecorPermissions.PartnerPriceTablesEdit);

        var dtoErrors = _dtoValidator.Validate(dto);
        if (dtoErrors.Any())
            throw new ValidationException(string.Join("\n", dtoErrors));

        var tableEntity = dto.FromDTO();

        var repoErrors = _repoValidator.Validate(tableEntity);
        if (repoErrors.Any())
            throw new ValidationException(string.Join("\n", repoErrors));

        var affectedRows = await _partnerPriceTableRepository.SaveAsync(tableEntity, cancellationToken);
        if (affectedRows != 1)
            throw new InvalidOperationException("Não foi possível salvar a tabela de preço do parceiro.");
    }

    public async Task DeletePartnerPriceTableAsync(int priceTableId, CancellationToken cancellationToken = default)
    {
        Require(DecorPermissions.PartnerPriceTablesDelete);
        var affectedRows = await _partnerPriceTableRepository.DeleteAsync(priceTableId, cancellationToken);
        if (affectedRows != 1)
            throw new InvalidOperationException("A tabela de preço do parceiro não foi encontrada ou não pôde ser excluída.");
    }

    private void Require(string permission)
    {
        if (!_authorizationService.HasPermission(permission))
            throw new UnauthorizedAccessException("Você não possui permissão para esta operação.");
    }
}
