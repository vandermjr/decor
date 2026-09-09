using System.ComponentModel.DataAnnotations;
using Decor.Core.Common;
using Decor.Application.Mappers;
using Decor.Core.DTOs;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Repositories;
using Decor.Core.Interfaces.Services;
using Decor.Core.Validation;

namespace Decor.Application.Services;

public class PartnerService(
        IPartnerRepository partnerRepository,
        IDTOValidator<PartnerDTO> dtoValidator,
        IRepositoryValidator<Partner> repoValidator,
        IAuthorizationService authorizationService) : IPartnerService
{
    private readonly IPartnerRepository _partnerRepository = partnerRepository;
    private readonly IDTOValidator<PartnerDTO> _dtoValidator = dtoValidator;
    private readonly IRepositoryValidator<Partner> _repoValidator = repoValidator;
    private readonly IAuthorizationService _authorizationService = authorizationService;

    public async Task<IEnumerable<PartnerDTO>> SearchPartnersAsync(string searchTerm, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default)
    {
        // Delega a busca para o repositório e mapeia o resultado para DTO
        var partners = await _partnerRepository.SearchGetByAsync(searchTerm, page, pageSize, cancellationToken);
        return partners.ToDTO();
    }

    public async Task<IEnumerable<PartnerDTO>> GetAllPartnersAsync(int page = 1, int pageSize = 100, CancellationToken cancellationToken = default)
    {
        // Para obter todos, chamamos a busca com um argumento nulo
        var partners = await _partnerRepository.SearchGetByAsync(null, page, pageSize, cancellationToken);
        return partners.ToDTO();
    }

    public async Task<PartnerDTO> GetPartnerByIdAsync(int partnerID, CancellationToken cancellationToken = default)
    {
        var partner = (await _partnerRepository.SearchGetByAsync(partnerID.ToString(), 1, 1, cancellationToken)).FirstOrDefault();
        return partner == null
            ? throw new KeyNotFoundException($"Parceiro com ID {partnerID} não encontrado.")
            : partner.ToDTO();
    }

    public async Task SavePartnerAsync(PartnerDTO partnerDto, CancellationToken cancellationToken = default)
    {
        Require(partnerDto.PartnerID == 0 ? DecorPermissions.PartnersCreate : DecorPermissions.PartnersEdit);
        // 1. Validação do DTO (regras de negócio que não dependem do banco)
        var dtoErrors = _dtoValidator.Validate(partnerDto);
        if (dtoErrors.Any())
            throw new ValidationException(string.Join("\n", dtoErrors));

        // 2. Mapeamento de DTO para Entidade de Domínio
        var partnerEntity = partnerDto.FromDTO();

        // 3. Validação de Repositório (regras que dependem do banco, ex: duplicidade)
        var repoErrors = _repoValidator.Validate(partnerEntity);
        if (repoErrors.Any())
            throw new ValidationException(string.Join("\n", repoErrors));

        // 4. Persistência
        var affectedRows = await _partnerRepository.SaveAsync(partnerEntity, cancellationToken);
        if (affectedRows != 1)
            throw new InvalidOperationException("Não foi possível salvar o parceiro.");
    }

    public async Task DeletePartnerAsync(int partnerId, CancellationToken cancellationToken = default)
    {
        Require(DecorPermissions.PartnersDelete);
        var affectedRows = await _partnerRepository.DeleteAsync(partnerId, cancellationToken);
        if (affectedRows != 1)
            throw new InvalidOperationException("O parceiro não foi encontrado ou não pôde ser excluído.");
    }

    private void Require(string permission)
    {
        if (!_authorizationService.HasPermission(permission))
            throw new UnauthorizedAccessException("Você não possui permissão para esta operação.");
    }
}
