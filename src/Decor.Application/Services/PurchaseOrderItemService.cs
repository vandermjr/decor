using System.ComponentModel.DataAnnotations;
using Decor.Application.Mappers;
using Decor.Core.Common;
using Decor.Core.DTOs;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Repositories;
using Decor.Core.Interfaces.Services;
using Decor.Core.Validation;

namespace Decor.Application.Services;

public class PurchaseOrderItemService(
    IPurchaseOrderItemRepository purchaseOrderItemRepository,
    IRepositoryValidator<PurchaseOrderItem> repositoryValidator,
    IAuthorizationService authorizationService) : IPurchaseOrderItemService
{
    private readonly IPurchaseOrderItemRepository _purchaseOrderItemRepository = purchaseOrderItemRepository;
    private readonly IRepositoryValidator<PurchaseOrderItem> _repositoryValidator = repositoryValidator;
    private readonly IAuthorizationService _authorizationService = authorizationService;

    public async Task<IEnumerable<PurchaseOrderItemDTO>> SearchPurchaseOrderItemsAsync(string searchTerm, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default)
    {
        Require(DecorPermissions.PurchaseOrderItemsView);
        var purchaseOrderItems = await _purchaseOrderItemRepository.SearchGetByAsync(searchTerm, page, pageSize, cancellationToken);
        return purchaseOrderItems.ToDTO();
    }

    public async Task<IEnumerable<PurchaseOrderItemDTO>> GetAllPurchaseOrderItemsAsync(int page = 1, int pageSize = 100, CancellationToken cancellationToken = default)
    {
        Require(DecorPermissions.PurchaseOrderItemsView);
        var purchaseOrderItems = await _purchaseOrderItemRepository.SearchGetByAsync(null, page, pageSize, cancellationToken);
        return purchaseOrderItems.ToDTO();
    }

    public async Task<PurchaseOrderItemDTO> GetPurchaseOrderItemByIdAsync(int purchaseOrderItemId, CancellationToken cancellationToken = default)
    {
        Require(DecorPermissions.PurchaseOrderItemsView);
        var purchaseOrderItem = (await _purchaseOrderItemRepository.SearchGetByAsync(purchaseOrderItemId.ToString(), 1, 1, cancellationToken)).FirstOrDefault();
        return purchaseOrderItem == null
            ? throw new KeyNotFoundException($"Item do pedido de compra com ID {purchaseOrderItemId} não encontrado.")
            : purchaseOrderItem.ToDTO();
    }

    public async Task SavePurchaseOrderItemAsync(PurchaseOrderItemDTO purchaseOrderItemDto, CancellationToken cancellationToken = default)
    {
        Require(purchaseOrderItemDto.PurchaseOrderItemID == 0 ? DecorPermissions.PurchaseOrderItemsCreate : DecorPermissions.PurchaseOrderItemsEdit);
        var purchaseOrderItem = purchaseOrderItemDto.FromDTO();
        var validationErrors = _repositoryValidator.Validate(purchaseOrderItem);
        if (validationErrors.Any())
            throw new ValidationException(string.Join("\n", validationErrors));

        var affectedRows = await _purchaseOrderItemRepository.SaveAsync(purchaseOrderItem, cancellationToken);
        if (affectedRows != 1)
            throw new InvalidOperationException("Não foi possível salvar o item do pedido de compra.");
    }

    public async Task DeletePurchaseOrderItemAsync(int purchaseOrderItemId, CancellationToken cancellationToken = default)
    {
        Require(DecorPermissions.PurchaseOrderItemsDelete);
        var affectedRows = await _purchaseOrderItemRepository.DeleteAsync(purchaseOrderItemId, cancellationToken);
        if (affectedRows != 1)
            throw new InvalidOperationException("O item do pedido de compra não foi encontrado ou não pôde ser excluído.");
    }

    private void Require(string permission)
    {
        if (!_authorizationService.HasPermission(permission))
            throw new UnauthorizedAccessException("Você não possui permissão para esta operação.");
    }
}