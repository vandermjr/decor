using Decor.Application.Mappers;
using Decor.Core.Common;
using Decor.Core.DTOs;
using Decor.Core.Interfaces.Repositories;
using Decor.Core.Interfaces.Services;

namespace Decor.Application.Services;

public class PurchaseOrderService(IPurchaseOrderRepository purchaseOrderRepository, IAuthorizationService authorizationService) : IPurchaseOrderService
{
    private readonly IPurchaseOrderRepository _purchaseOrderRepository = purchaseOrderRepository;
    private readonly IAuthorizationService _authorizationService = authorizationService;

    public async Task<IEnumerable<PurchaseOrderDTO>> SearchPurchaseOrdersAsync(string searchTerm, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default)
    {
        Require(DecorPermissions.PurchaseOrdersView);
        var purchaseOrders = await _purchaseOrderRepository.SearchGetByAsync(searchTerm, page, pageSize, cancellationToken);
        return purchaseOrders.ToDTO();
    }

    public async Task<IEnumerable<PurchaseOrderDTO>> GetAllPurchaseOrdersAsync(int page = 1, int pageSize = 100, CancellationToken cancellationToken = default)
    {
        Require(DecorPermissions.PurchaseOrdersView);
        var purchaseOrders = await _purchaseOrderRepository.SearchGetByAsync(null, page, pageSize, cancellationToken);
        return purchaseOrders.ToDTO();
    }

    public async Task<PurchaseOrderDTO> GetPurchaseOrderByIdAsync(int purchaseOrderId, CancellationToken cancellationToken = default)
    {
        Require(DecorPermissions.PurchaseOrdersView);
        var purchaseOrder = (await _purchaseOrderRepository.SearchGetByAsync(purchaseOrderId.ToString(), 1, 1, cancellationToken)).FirstOrDefault();
        return purchaseOrder == null
            ? throw new KeyNotFoundException($"Pedido de compra com ID {purchaseOrderId} não encontrado.")
            : purchaseOrder.ToDTO();
    }

    public async Task SavePurchaseOrderAsync(PurchaseOrderDTO purchaseOrderDto, CancellationToken cancellationToken = default)
    {
        Require(purchaseOrderDto.PurchaseOrderID == 0 ? DecorPermissions.PurchaseOrdersCreate : DecorPermissions.PurchaseOrdersEdit);
        var purchaseOrder = purchaseOrderDto.FromDTO();
        var affectedRows = await _purchaseOrderRepository.SaveAsync(purchaseOrder, cancellationToken);
        if (affectedRows != 1)
            throw new InvalidOperationException("Não foi possível salvar o pedido de compra.");
    }

    public async Task DeletePurchaseOrderAsync(int purchaseOrderId, CancellationToken cancellationToken = default)
    {
        Require(DecorPermissions.PurchaseOrdersDelete);
        var affectedRows = await _purchaseOrderRepository.DeleteAsync(purchaseOrderId, cancellationToken);
        if (affectedRows != 1)
            throw new InvalidOperationException("O pedido de compra não foi encontrado ou não pôde ser excluído.");
    }

    private void Require(string permission)
    {
        if (!_authorizationService.HasPermission(permission))
            throw new UnauthorizedAccessException("Você não possui permissão para esta operação.");
    }
}