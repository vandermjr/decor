using System.ComponentModel.DataAnnotations;
using Decor.Application.Mappers;
using Decor.Core.Common;
using Decor.Core.DTOs;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Repositories;
using Decor.Core.Interfaces.Services;
using Decor.Core.Validation;

namespace Decor.Application.Services;

public class StockReservationService(
    IStockReservationRepository stockReservationRepository,
    IOrderRepository orderRepository,
    IProductRepository productRepository,
    IStockLocationRepository stockLocationRepository,
    IStockBalanceRepository stockBalanceRepository,
    IDTOValidator<StockReservationDTO> dtoValidator,
    IRepositoryValidator<StockReservation> repoValidator,
    IAuthorizationService authorizationService) : IStockReservationService
{
    private readonly IStockReservationRepository _stockReservationRepository = stockReservationRepository;
    private readonly IOrderRepository _orderRepository = orderRepository;
    private readonly IProductRepository _productRepository = productRepository;
    private readonly IStockLocationRepository _stockLocationRepository = stockLocationRepository;
    private readonly IStockBalanceRepository _stockBalanceRepository = stockBalanceRepository;
    private readonly IDTOValidator<StockReservationDTO> _dtoValidator = dtoValidator;
    private readonly IRepositoryValidator<StockReservation> _repoValidator = repoValidator;
    private readonly IAuthorizationService _authorizationService = authorizationService;

    public async Task<StockReservationDTO> CreateReservationAsync(int orderItemId, int stockLocationId, int createdByEmployeeId, decimal quantity, CancellationToken cancellationToken = default)
    {
        Require(DecorPermissions.StockReservationsCreate);

        if (quantity <= 0)
            throw new ValidationException("A quantidade da reserva deve ser maior que zero.");

        var orderItem = await _orderRepository.GetOrderItemByIdAsync(orderItemId, cancellationToken)
            ?? throw new KeyNotFoundException($"Item de pedido com ID {orderItemId} não encontrado.");

        var order = await _orderRepository.GetByIdAsync(orderItem.OrderID, cancellationToken)
            ?? throw new KeyNotFoundException($"Pedido com ID {orderItem.OrderID} não encontrado.");

        if (order.Status != OrderStatus.Approved)
            throw new ValidationException("O pedido precisa estar com status 'Aprovado' (Approved) para que uma reserva de estoque seja criada.");

        var products = await _productRepository.SearchGetByAsync(orderItem.ProductID.ToString(), 1, 1, cancellationToken);
        var product = products.FirstOrDefault(p => p.ProductID == orderItem.ProductID)
            ?? (await _productRepository.SearchGetByAsync(null, 1, 1000, cancellationToken)).FirstOrDefault(p => p.ProductID == orderItem.ProductID)
            ?? throw new KeyNotFoundException($"Produto com ID {orderItem.ProductID} não encontrado.");

        if (product.ProductType != ProductType.Good)
            throw new ValidationException("Produtos do tipo Serviço (Service) não podem ser reservados.");

        var existingActive = await _stockReservationRepository.GetActiveByOrderItemIdAsync(orderItemId, cancellationToken);
        if (existingActive != null)
            throw new ValidationException($"Já existe uma reserva ativa para o item de pedido ID {orderItemId}.");

        var locations = await _stockLocationRepository.SearchGetByAsync(stockLocationId.ToString(), 1, 1, cancellationToken);
        var location = locations.FirstOrDefault(l => l.StockLocationID == stockLocationId)
            ?? (await _stockLocationRepository.SearchGetByAsync(null, 1, 1000, cancellationToken)).FirstOrDefault(l => l.StockLocationID == stockLocationId)
            ?? throw new KeyNotFoundException($"Depósito com ID {stockLocationId} não encontrado.");

        if (location.PartnerID != null || location.LocationType != StockLocationType.Empresa)
            throw new ValidationException("A reserva só pode ser feita em um depósito próprio da empresa.");

        var balance = await _stockBalanceRepository.GetByProductAndLocationAsync(orderItem.ProductID, stockLocationId, cancellationToken);
        var currentBalance = balance?.Quantity ?? 0m;
        var totalActiveReserved = await _stockReservationRepository.GetTotalActiveReservedQuantityAsync(orderItem.ProductID, stockLocationId, cancellationToken);
        var availableStock = currentBalance - totalActiveReserved;

        if (quantity > availableStock)
            throw new ValidationException($"Saldo insuficiente no depósito selecionado. Saldo disponível: {availableStock}, Quantidade solicitada: {quantity}.");

        var reservation = new StockReservation
        {
            OrderItemID = orderItemId,
            ProductID = orderItem.ProductID,
            StockLocationID = stockLocationId,
            Quantity = quantity,
            Status = StockReservationStatus.Active,
            CreatedByEmployeeID = createdByEmployeeId,
            CreatedAt = DateTime.UtcNow,
            ReleasedAt = null
        };

        var dtoErrors = _dtoValidator.Validate(reservation.ToDTO());
        if (dtoErrors.Any())
            throw new ValidationException(string.Join("\n", dtoErrors));

        var repoErrors = _repoValidator.Validate(reservation);
        if (repoErrors.Any())
            throw new ValidationException(string.Join("\n", repoErrors));

        await _stockReservationRepository.SaveAsync(reservation, cancellationToken);
        return reservation.ToDTO();
    }

    public async Task ReleaseReservationAsync(int reservationId, int? releasedByEmployeeId = null, CancellationToken cancellationToken = default)
    {
        Require(DecorPermissions.StockReservationsRelease);

        var reservation = await _stockReservationRepository.GetByIdAsync(reservationId, cancellationToken)
            ?? throw new KeyNotFoundException($"Reserva com ID {reservationId} não encontrada.");

        if (reservation.Status != StockReservationStatus.Active)
            throw new ValidationException("Apenas reservas ativas podem ser liberadas.");

        reservation.Status = StockReservationStatus.Released;
        reservation.ReleasedAt = DateTime.UtcNow;

        await _stockReservationRepository.SaveAsync(reservation, cancellationToken);
    }

    public async Task ReleaseActiveReservationsForOrderAsync(int orderId, CancellationToken cancellationToken = default)
    {
        var activeReservations = await _stockReservationRepository.GetActiveByOrderIdAsync(orderId, cancellationToken);
        foreach (var reservation in activeReservations)
        {
            reservation.Status = StockReservationStatus.Released;
            reservation.ReleasedAt = DateTime.UtcNow;
            await _stockReservationRepository.SaveAsync(reservation, cancellationToken);
        }
    }

    public async Task<StockReservationDTO> GetByIdAsync(int reservationId, CancellationToken cancellationToken = default)
    {
        Require(DecorPermissions.StockReservationsView);
        var reservation = await _stockReservationRepository.GetByIdAsync(reservationId, cancellationToken)
            ?? throw new KeyNotFoundException($"Reserva com ID {reservationId} não encontrada.");
        return reservation.ToDTO();
    }

    public async Task<IEnumerable<StockReservationDTO>> GetByOrderItemIdAsync(int orderItemId, CancellationToken cancellationToken = default)
    {
        Require(DecorPermissions.StockReservationsView);
        var reservations = await _stockReservationRepository.GetByOrderItemIdAsync(orderItemId, cancellationToken);
        return reservations.ToDTO();
    }

    public async Task<IEnumerable<StockReservationDTO>> GetByOrderIdAsync(int orderId, CancellationToken cancellationToken = default)
    {
        Require(DecorPermissions.StockReservationsView);
        var reservations = await _stockReservationRepository.GetByOrderIdAsync(orderId, cancellationToken);
        return reservations.ToDTO();
    }

    public async Task<IEnumerable<StockReservationDTO>> GetAllAsync(int page = 1, int pageSize = 100, CancellationToken cancellationToken = default)
    {
        Require(DecorPermissions.StockReservationsView);
        var reservations = await _stockReservationRepository.SearchGetByAsync(null, page, pageSize, cancellationToken);
        return reservations.ToDTO();
    }

    public async Task<IEnumerable<StockReservationDTO>> SearchAsync(string? searchTerm, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default)
    {
        Require(DecorPermissions.StockReservationsView);
        var reservations = await _stockReservationRepository.SearchGetByAsync(searchTerm, page, pageSize, cancellationToken);
        return reservations.ToDTO();
    }

    private void Require(string permission)
    {
        if (!_authorizationService.HasPermission(permission))
            throw new UnauthorizedAccessException("Você não possui permissão para esta operação.");
    }
}
