using System.ComponentModel.DataAnnotations;
using Decor.Application.Mappers;
using Decor.Core.Common;
using Decor.Core.DTOs;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Repositories;
using Decor.Core.Interfaces.Services;

namespace Decor.Application.Services;

public class GoodsReceiptService(
    IGoodsReceiptRepository goodsReceiptRepository,
    IPurchaseOrderItemRepository purchaseOrderItemRepository,
    IStockMovementService stockMovementService,
    IAuthorizationService authorizationService) : IGoodsReceiptService
{
    private readonly IGoodsReceiptRepository _goodsReceiptRepository = goodsReceiptRepository;
    private readonly IPurchaseOrderItemRepository _purchaseOrderItemRepository = purchaseOrderItemRepository;
    private readonly IStockMovementService _stockMovementService = stockMovementService;
    private readonly IAuthorizationService _authorizationService = authorizationService;

    // NOTA: não há transação compartilhada entre o GoodsReceiptRepository
    // e o IStockMovementService. O recebimento é persistido primeiro; se a
    // entrada no estoque falhar, o recebimento pode ficar órfão de estoque.
    // Reconciliação/idempotência ficam fora de escopo (ver Md/, Fase P3).
    public async Task<int> RegisterReceiptAsync(int purchaseOrderItemId, decimal quantityReceived, int receivedByEmployeeId, bool hasDivergence, string? divergenceNotes = null, CancellationToken cancellationToken = default)
    {
        Require(DecorPermissions.GoodsReceiptsRegister);
        if (quantityReceived <= 0)
            throw new ValidationException("A quantidade recebida deve ser maior que zero.");
        if (hasDivergence && string.IsNullOrWhiteSpace(divergenceNotes))
            throw new ValidationException("As observações da divergência são obrigatórias.");

        var purchaseOrderItem = (await _purchaseOrderItemRepository.SearchGetByAsync(purchaseOrderItemId.ToString(), 1, 1, cancellationToken)).FirstOrDefault();
        if (purchaseOrderItem == null)
            throw new KeyNotFoundException($"Item do pedido de compra com ID {purchaseOrderItemId} não encontrado.");

        var goodsReceipt = new GoodsReceipt
        {
            PurchaseOrderItemID = purchaseOrderItemId,
            ReceiptDate = DateTime.UtcNow,
            QuantityReceived = quantityReceived,
            ReceivedByEmployeeID = receivedByEmployeeId,
            HasDivergence = hasDivergence,
            DivergenceNotes = divergenceNotes,
            Status = hasDivergence ? GoodsReceiptStatus.DivergenteDevolvido : GoodsReceiptStatus.Conferido
        };

        var goodsReceiptId = await _goodsReceiptRepository.InsertAsync(goodsReceipt, cancellationToken);
        if (!hasDivergence && purchaseOrderItem.FinalDestination == PurchaseOrderFinalDestination.DepositoEmpresa)
        {
            await _stockMovementService.RegisterEntryAsync(
                purchaseOrderItem.ProductID,
                purchaseOrderItem.StockLocationID!.Value,
                quantityReceived,
                receivedByEmployeeId,
                notes: $"Recebimento do Pedido de Compra #{purchaseOrderItem.PurchaseOrderID}",
                cancellationToken);
        }

        return goodsReceiptId;
    }

    public async Task<GoodsReceiptDTO> GetByIdAsync(int goodsReceiptId, CancellationToken cancellationToken = default)
    {
        Require(DecorPermissions.GoodsReceiptsView);
        var goodsReceipt = await _goodsReceiptRepository.GetByIdAsync(goodsReceiptId, cancellationToken);
        return goodsReceipt == null
            ? throw new KeyNotFoundException($"Recebimento de mercadoria com ID {goodsReceiptId} não encontrado.")
            : goodsReceipt.ToDTO();
    }

    public async Task<IEnumerable<GoodsReceiptDTO>> GetByPurchaseOrderItemIdAsync(int purchaseOrderItemId, CancellationToken cancellationToken = default)
    {
        Require(DecorPermissions.GoodsReceiptsView);
        var goodsReceipts = await _goodsReceiptRepository.GetByPurchaseOrderItemIdAsync(purchaseOrderItemId, cancellationToken);
        return goodsReceipts.ToDTO();
    }

    private void Require(string permission)
    {
        if (!_authorizationService.HasPermission(permission))
            throw new UnauthorizedAccessException("Você não possui permissão para esta operação.");
    }
}