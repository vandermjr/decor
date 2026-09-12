using System.ComponentModel.DataAnnotations;
using Decor.Core.Common;
using Decor.Application.Mappers;
using Decor.Core.DTOs;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Repositories;
using Decor.Core.Interfaces.Services;
using Decor.Core.Validation;

namespace Decor.Application.Services;

public class ProductSpecificationAttributeService(
        IProductSpecificationAttributeRepository attributeRepository,
        IDTOValidator<ProductSpecificationAttributeDTO> dtoValidator,
        IRepositoryValidator<ProductSpecificationAttribute> repoValidator,
        IAuthorizationService authorizationService) : IProductSpecificationAttributeService
{
    private readonly IProductSpecificationAttributeRepository _attributeRepository = attributeRepository;
    private readonly IDTOValidator<ProductSpecificationAttributeDTO> _dtoValidator = dtoValidator;
    private readonly IRepositoryValidator<ProductSpecificationAttribute> _repoValidator = repoValidator;
    private readonly IAuthorizationService _authorizationService = authorizationService;

    public async Task<IEnumerable<ProductSpecificationAttributeDTO>> SearchProductSpecificationAttributesAsync(string searchTerm, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default)
    {
        var attributes = await _attributeRepository.SearchGetByAsync(searchTerm, page, pageSize, cancellationToken);
        return attributes.ToDTO();
    }

    public async Task<IEnumerable<ProductSpecificationAttributeDTO>> GetAllProductSpecificationAttributesAsync(int page = 1, int pageSize = 100, CancellationToken cancellationToken = default)
    {
        var attributes = await _attributeRepository.SearchGetByAsync(null, page, pageSize, cancellationToken);
        return attributes.ToDTO();
    }

    public async Task<ProductSpecificationAttributeDTO> GetProductSpecificationAttributeByIdAsync(int attributeId, CancellationToken cancellationToken = default)
    {
        var attribute = (await _attributeRepository.SearchGetByAsync(attributeId.ToString(), 1, 1, cancellationToken)).FirstOrDefault();
        return attribute == null
            ? throw new KeyNotFoundException($"Atributo de especificação com ID {attributeId} não encontrado.")
            : attribute.ToDTO();
    }

    public async Task SaveProductSpecificationAttributeAsync(ProductSpecificationAttributeDTO attributeDto, CancellationToken cancellationToken = default)
    {
        Require(attributeDto.AttributeID == 0 ? DecorPermissions.ProductSpecificationAttributesCreate : DecorPermissions.ProductSpecificationAttributesEdit);

        // 1. Validação do DTO (regras de negócio que não dependem do banco)
        var dtoErrors = _dtoValidator.Validate(attributeDto);
        if (dtoErrors.Any())
            throw new ValidationException(string.Join("\n", dtoErrors));

        // 2. Mapeamento de DTO para Entidade de Domínio
        var attributeEntity = attributeDto.FromDTO();

        // 3. Validação de Repositório (regras que dependem do banco)
        var repoErrors = _repoValidator.Validate(attributeEntity);
        if (repoErrors.Any())
            throw new ValidationException(string.Join("\n", repoErrors));

        // 4. Persistência
        var affectedRows = await _attributeRepository.SaveAsync(attributeEntity, cancellationToken);
        if (affectedRows != 1)
            throw new InvalidOperationException("Não foi possível salvar o atributo de especificação.");
    }

    public async Task DeleteProductSpecificationAttributeAsync(int attributeId, CancellationToken cancellationToken = default)
    {
        Require(DecorPermissions.ProductSpecificationAttributesDelete);
        var affectedRows = await _attributeRepository.DeleteAsync(attributeId, cancellationToken);
        if (affectedRows != 1)
            throw new InvalidOperationException("O atributo de especificação não foi encontrado ou não pôde ser excluído.");
    }

    private void Require(string permission)
    {
        if (!_authorizationService.HasPermission(permission))
            throw new UnauthorizedAccessException("Você não possui permissão para esta operação.");
    }
}
