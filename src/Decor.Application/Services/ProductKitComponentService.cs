using System.ComponentModel.DataAnnotations;
using Decor.Application.Mappers;
using Decor.Core.Common;
using Decor.Core.DTOs;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Repositories;
using Decor.Core.Interfaces.Services;
using Decor.Core.Validation;

namespace Decor.Application.Services;

public class ProductKitComponentService(
    IProductKitComponentRepository productKitComponentRepository,
    IDTOValidator<ProductKitComponentDTO> dtoValidator,
    IRepositoryValidator<ProductKitComponent> repoValidator,
    IAuthorizationService authorizationService) : IProductKitComponentService
{
    private readonly IProductKitComponentRepository _productKitComponentRepository = productKitComponentRepository;
    private readonly IDTOValidator<ProductKitComponentDTO> _dtoValidator = dtoValidator;
    private readonly IRepositoryValidator<ProductKitComponent> _repoValidator = repoValidator;
    private readonly IAuthorizationService _authorizationService = authorizationService;

    public async Task<IEnumerable<ProductKitComponentDTO>> SearchProductKitComponentsAsync(string searchTerm, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default)
    {
        Require(DecorPermissions.ProductKitComponentsView);
        var components = await _productKitComponentRepository.SearchGetByAsync(searchTerm, page, pageSize, cancellationToken);
        return components.ToDTO();
    }

    public async Task<IEnumerable<ProductKitComponentDTO>> GetAllProductKitComponentsAsync(int page = 1, int pageSize = 100, CancellationToken cancellationToken = default)
    {
        Require(DecorPermissions.ProductKitComponentsView);
        var components = await _productKitComponentRepository.SearchGetByAsync(null, page, pageSize, cancellationToken);
        return components.ToDTO();
    }

    public async Task<ProductKitComponentDTO> GetProductKitComponentByIdAsync(int componentId, CancellationToken cancellationToken = default)
    {
        Require(DecorPermissions.ProductKitComponentsView);
        var component = (await _productKitComponentRepository.SearchGetByAsync(componentId.ToString(), 1, 1, cancellationToken)).FirstOrDefault();
        return component == null
            ? throw new KeyNotFoundException($"Componente de Kit com ID {componentId} não encontrado.")
            : component.ToDTO();
    }

    public async Task<IEnumerable<ProductKitComponentDTO>> GetByKitProductIdAsync(int kitProductId, CancellationToken cancellationToken = default)
    {
        Require(DecorPermissions.ProductKitComponentsView);
        var components = await _productKitComponentRepository.GetByKitProductIdAsync(kitProductId, cancellationToken);
        return components.ToDTO();
    }

    public async Task SaveProductKitComponentAsync(ProductKitComponentDTO productKitComponentDto, CancellationToken cancellationToken = default)
    {
        Require(productKitComponentDto.ComponentID == 0 ? DecorPermissions.ProductKitComponentsCreate : DecorPermissions.ProductKitComponentsEdit);
        var dtoErrors = _dtoValidator.Validate(productKitComponentDto);
        if (dtoErrors.Any())
            throw new ValidationException(string.Join("\n", dtoErrors));

        var componentEntity = productKitComponentDto.FromDTO();

        var repoErrors = _repoValidator.Validate(componentEntity);
        if (repoErrors.Any())
            throw new ValidationException(string.Join("\n", repoErrors));

        var affectedRows = await _productKitComponentRepository.SaveAsync(componentEntity, cancellationToken);
        if (affectedRows != 1)
            throw new InvalidOperationException("Não foi possível salvar o componente do kit.");
    }

    public async Task DeleteProductKitComponentAsync(int componentId, CancellationToken cancellationToken = default)
    {
        Require(DecorPermissions.ProductKitComponentsDelete);
        var affectedRows = await _productKitComponentRepository.DeleteAsync(componentId, cancellationToken);
        if (affectedRows != 1)
            throw new InvalidOperationException("O componente do kit não foi encontrado ou não pôde ser excluído.");
    }

    public async Task<decimal?> GetSuggestedKitPriceAsync(int kitProductId, CancellationToken cancellationToken = default)
    {
        Require(DecorPermissions.ProductKitComponentsView);
        var pricingItems = (await _productKitComponentRepository.GetPricingByKitProductIdAsync(kitProductId, cancellationToken)).ToList();

        if (pricingItems.Count == 0)
            return null;

        if (pricingItems.Any(i => i.SalePrice is null))
            return null;

        return pricingItems.Sum(i => i.SalePrice!.Value * i.Quantity);
    }

    private void Require(string permission)
    {
        if (!_authorizationService.HasPermission(permission))
            throw new UnauthorizedAccessException("Você não possui permissão para esta operação.");
    }
}
