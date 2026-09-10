using System.ComponentModel.DataAnnotations;
using Decor.Application.Mappers;
using Decor.Core.Common;
using Decor.Core.DTOs;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Repositories;
using Decor.Core.Interfaces.Services;

namespace Decor.Application.Services;

public class StockMovementService(IStockMovementRepository stockMovementRepository, IAuthorizationService authorizationService) : IStockMovementService
{
    private readonly IStockMovementRepository _stockMovementRepository = stockMovementRepository;
    private readonly IAuthorizationService _authorizationService = authorizationService;

    public async Task<int> RegisterEntryAsync(int productId, int stockLocationId, decimal quantity, int performedByEmployeeId, string? notes = null, CancellationToken cancellationToken = default)
    {
        Require(DecorPermissions.StockMovementsEntry);
        if (quantity <= 0)
            throw new ValidationException("A quantidade de entrada deve ser maior que zero.");

        var movement = new StockMovement
        {
            ProductID = productId,
            StockLocationID = stockLocationId,
            Quantity = quantity,
            MovementType = StockMovementType.Entrada,
            PerformedByEmployeeID = performedByEmployeeId,
            MovementDate = DateTime.UtcNow,
            Notes = notes
        };

        return await _stockMovementRepository.RegisterMovementAsync(movement, cancellationToken);
    }

    public async Task<int> RegisterExitAsync(int productId, int stockLocationId, decimal quantity, int performedByEmployeeId, string? notes = null, CancellationToken cancellationToken = default)
    {
        Require(DecorPermissions.StockMovementsExit);
        if (quantity <= 0)
            throw new ValidationException("A quantidade de saída deve ser maior que zero.");

        var movement = new StockMovement
        {
            ProductID = productId,
            StockLocationID = stockLocationId,
            Quantity = -quantity,
            MovementType = StockMovementType.Saida,
            PerformedByEmployeeID = performedByEmployeeId,
            MovementDate = DateTime.UtcNow,
            Notes = notes
        };

        return await _stockMovementRepository.RegisterMovementAsync(movement, cancellationToken);
    }

    public async Task<int> RegisterAdjustmentAsync(int productId, int stockLocationId, decimal quantityDelta, StockAdjustmentReason reason, string justification, int authorizedByEmployeeId, int performedByEmployeeId, string? notes = null, CancellationToken cancellationToken = default)
    {
        Require(DecorPermissions.StockMovementsAdjust);
        if (quantityDelta == 0)
            throw new ValidationException("A quantidade do ajuste deve ser diferente de zero.");
        if (string.IsNullOrWhiteSpace(justification))
            throw new ValidationException("A justificativa do ajuste é obrigatória.");

        var movement = new StockMovement
        {
            ProductID = productId,
            StockLocationID = stockLocationId,
            Quantity = quantityDelta,
            MovementType = StockMovementType.Ajuste,
            Reason = reason,
            Justification = justification,
            AuthorizedByEmployeeID = authorizedByEmployeeId,
            PerformedByEmployeeID = performedByEmployeeId,
            MovementDate = DateTime.UtcNow,
            Notes = notes
        };

        return await _stockMovementRepository.RegisterMovementAsync(movement, cancellationToken);
    }

    public async Task<Guid> RegisterTransferAsync(int productId, int sourceStockLocationId, int destinationStockLocationId, decimal quantity, int performedByEmployeeId, string? notes = null, CancellationToken cancellationToken = default)
    {
        Require(DecorPermissions.StockMovementsTransfer);
        if (quantity <= 0)
            throw new ValidationException("A quantidade da transferência deve ser maior que zero.");
        if (sourceStockLocationId == destinationStockLocationId)
            throw new ValidationException("O depósito de origem deve ser diferente do depósito de destino.");

        var movementDate = DateTime.UtcNow;
        var outboundMovement = new StockMovement
        {
            ProductID = productId,
            StockLocationID = sourceStockLocationId,
            Quantity = -quantity,
            MovementType = StockMovementType.Transferencia,
            PerformedByEmployeeID = performedByEmployeeId,
            ReviewStatus = StockMovementReviewStatus.PendenteDeCiencia,
            MovementDate = movementDate,
            Notes = notes
        };
        var inboundMovement = new StockMovement
        {
            ProductID = productId,
            StockLocationID = destinationStockLocationId,
            Quantity = quantity,
            MovementType = StockMovementType.Transferencia,
            PerformedByEmployeeID = performedByEmployeeId,
            ReviewStatus = StockMovementReviewStatus.PendenteDeCiencia,
            MovementDate = movementDate,
            Notes = notes
        };

        return await _stockMovementRepository.RegisterTransferAsync(outboundMovement, inboundMovement, cancellationToken);
    }

    public async Task ReviewTransferAsync(Guid transferId, StockMovementReviewStatus reviewStatus, int reviewedByEmployeeId, CancellationToken cancellationToken = default)
    {
        Require(DecorPermissions.StockMovementsReview);
        if (reviewStatus is not (StockMovementReviewStatus.Ciente or StockMovementReviewStatus.Contestado))
            throw new ValidationException("A revisão da transferência deve ser Ciente ou Contestado.");

        var affectedRows = await _stockMovementRepository.UpdateReviewAsync(transferId, reviewStatus, reviewedByEmployeeId, DateTime.UtcNow, cancellationToken);
        if (affectedRows != 2)
            throw new KeyNotFoundException($"Transferência com ID {transferId} não encontrada.");
    }

    public async Task<StockMovementDTO> GetByIdAsync(int stockMovementId, CancellationToken cancellationToken = default)
    {
        Require(DecorPermissions.StockMovementsView);
        var stockMovement = await _stockMovementRepository.GetByIdAsync(stockMovementId, cancellationToken);
        return stockMovement == null
            ? throw new KeyNotFoundException($"Movimento de estoque com ID {stockMovementId} não encontrado.")
            : stockMovement.ToDTO();
    }

    public async Task<IEnumerable<StockMovementDTO>> GetByProductAsync(int productId, CancellationToken cancellationToken = default)
    {
        Require(DecorPermissions.StockMovementsView);
        var stockMovements = await _stockMovementRepository.GetByProductAsync(productId, cancellationToken);
        return stockMovements.ToDTO();
    }

    public async Task<IEnumerable<StockMovementDTO>> GetByTransferIdAsync(Guid transferId, CancellationToken cancellationToken = default)
    {
        Require(DecorPermissions.StockMovementsView);
        var stockMovements = await _stockMovementRepository.GetByTransferIdAsync(transferId, cancellationToken);
        return stockMovements.ToDTO();
    }

    private void Require(string permission)
    {
        if (!_authorizationService.HasPermission(permission))
            throw new UnauthorizedAccessException("Você não possui permissão para esta operação.");
    }
}