using System.ComponentModel.DataAnnotations;
using Decor.Application.Mappers;
using Decor.Core.Common;
using Decor.Core.DTOs;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Repositories;
using Decor.Core.Interfaces.Services;
using Decor.Core.Validation;

namespace Decor.Application.Services;

public class OrderService(
    IOrderRepository orderRepository,
    IQuoteRepository quoteRepository,
    IProductRepository productRepository,
    IStockReservationService stockReservationService,
    IDTOValidator<OrderDTO> dtoValidator,
    IRepositoryValidator<Order> repoValidator,
    IAuthorizationService authorizationService) : IOrderService
{
    private readonly IOrderRepository _orderRepository = orderRepository;
    private readonly IQuoteRepository _quoteRepository = quoteRepository;
    private readonly IProductRepository _productRepository = productRepository;
    private readonly IStockReservationService _stockReservationService = stockReservationService;
    private readonly IDTOValidator<OrderDTO> _dtoValidator = dtoValidator;
    private readonly IRepositoryValidator<Order> _repoValidator = repoValidator;
    private readonly IAuthorizationService _authorizationService = authorizationService;

    public async Task<IEnumerable<OrderDTO>> SearchOrdersAsync(string searchTerm, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default)
    {
        Require(DecorPermissions.OrdersView);
        var orders = await _orderRepository.SearchGetByAsync(searchTerm, page, pageSize, cancellationToken);
        return orders.ToDTO();
    }

    public async Task<IEnumerable<OrderDTO>> GetAllOrdersAsync(int page = 1, int pageSize = 100, CancellationToken cancellationToken = default)
    {
        Require(DecorPermissions.OrdersView);
        var orders = await _orderRepository.SearchGetByAsync(null, page, pageSize, cancellationToken);
        return orders.ToDTO();
    }

    public async Task<OrderDTO> GetOrderByIdAsync(int orderId, CancellationToken cancellationToken = default)
    {
        Require(DecorPermissions.OrdersView);
        var order = await _orderRepository.GetCompleteOrderAsync(orderId, cancellationToken);
        return order == null
            ? throw new KeyNotFoundException($"Pedido com ID {orderId} não encontrado.")
            : order.ToDTO();
    }

    public async Task<OrderDTO> ConvertFromQuoteAsync(int quoteSectionId, CancellationToken cancellationToken = default)
    {
        Require(DecorPermissions.OrdersConvertFromQuote);

        var section = await _quoteRepository.GetSectionByIdAsync(quoteSectionId, cancellationToken)
            ?? throw new KeyNotFoundException($"Seção do orçamento com ID {quoteSectionId} não encontrada.");

        if (section.Status != QuoteSectionStatus.Approved)
            throw new ValidationException("A seção do orçamento precisa estar com status 'Aprovado' (Approved) para ser convertida em pedido.");

        var existingOrder = await _orderRepository.GetByQuoteSectionIdAsync(quoteSectionId, cancellationToken);
        if (existingOrder != null)
            throw new ValidationException($"Já existe um pedido criado para a seção de orçamento ID {quoteSectionId}.");

        var quote = await _quoteRepository.GetCompleteQuoteAsync(section.QuoteID, cancellationToken)
            ?? throw new KeyNotFoundException($"Orçamento pai ID {section.QuoteID} não encontrado.");

        var fullSection = quote.Sections.FirstOrDefault(s => s.QuoteSectionID == quoteSectionId) ?? section;

        var order = new Order
        {
            QuoteSectionID = quoteSectionId,
            CustomerID = quote.CustomerID,
            OrderType = section.SectionType == QuoteSectionType.Catalog ? OrderType.Catalog : OrderType.Custom,
            Status = OrderStatus.PendingApproval,
            RequiresDownPayment = null,
            ManufacturingDeadline = null,
            InstallationDeadline = null,
            CreatedAt = DateTime.UtcNow,
            Items = new List<OrderItem>()
        };

        var repoErrors = _repoValidator.Validate(order);
        if (repoErrors.Any())
            throw new ValidationException(string.Join("\n", repoErrors));

        await _orderRepository.SaveAsync(order, cancellationToken);

        foreach (var quoteItem in fullSection.Items)
        {
            var orderItem = new OrderItem
            {
                OrderID = order.OrderID,
                QuoteItemID = quoteItem.QuoteItemID,
                ProductID = quoteItem.ProductID,
                Quantity = quoteItem.Quantity,
                UnitPrice = quoteItem.UnitPrice ?? 0m,
                HasInstallationService = quoteItem.HasInstallationService,
                SentToProductionAt = null,
                SentToProductionByEmployeeID = null,
                SpecificationValues = new List<OrderItemSpecificationValue>()
            };

            await _orderRepository.SaveOrderItemAsync(orderItem, cancellationToken);

            foreach (var specValue in quoteItem.SpecificationValues)
            {
                var orderSpecValue = new OrderItemSpecificationValue
                {
                    OrderItemID = orderItem.OrderItemID,
                    AttributeID = specValue.AttributeID,
                    Value = specValue.Value
                };

                await _orderRepository.SaveSpecificationValueAsync(orderSpecValue, cancellationToken);
                orderItem.SpecificationValues.Add(orderSpecValue);
            }

            order.Items.Add(orderItem);
        }

        section.Status = QuoteSectionStatus.ConvertedToOrder;
        await _quoteRepository.SaveSectionAsync(section, cancellationToken);

        return order.ToDTO();
    }

    public async Task ApproveOrderAsync(int orderId, bool requiresDownPayment, DateTime? manufacturingDeadline = null, DateTime? installationDeadline = null, CancellationToken cancellationToken = default)
    {
        Require(DecorPermissions.OrdersApprove);

        var order = await _orderRepository.GetCompleteOrderAsync(orderId, cancellationToken)
            ?? throw new KeyNotFoundException($"Pedido com ID {orderId} não encontrado.");

        if (order.Status != OrderStatus.PendingApproval)
            throw new ValidationException("O pedido só pode ser aprovado quando estiver com status 'Pendente de Aprovação' (PendingApproval).");

        order.RequiresDownPayment = requiresDownPayment;
        order.ManufacturingDeadline = manufacturingDeadline;
        order.InstallationDeadline = installationDeadline;
        order.Status = OrderStatus.Approved;

        await _orderRepository.SaveAsync(order, cancellationToken);
    }

    public async Task CancelOrderAsync(int orderId, CancellationToken cancellationToken = default)
    {
        Require(DecorPermissions.OrdersCancel);

        var order = await _orderRepository.GetCompleteOrderAsync(orderId, cancellationToken)
            ?? throw new KeyNotFoundException($"Pedido com ID {orderId} não encontrado.");

        if (order.Status != OrderStatus.PendingApproval && order.Status != OrderStatus.Approved)
            throw new ValidationException("O pedido só pode ser cancelado se estiver com status Pendente de Aprovação ou Aprovado.");

        order.Status = OrderStatus.Cancelled;

        await _orderRepository.SaveAsync(order, cancellationToken);
        await _stockReservationService.ReleaseActiveReservationsForOrderAsync(orderId, cancellationToken);
    }

    public async Task SendItemToProductionAsync(int orderItemId, int sentToProductionByEmployeeID, CancellationToken cancellationToken = default)
    {
        Require(DecorPermissions.OrdersSendToProduction);

        var item = await _orderRepository.GetOrderItemByIdAsync(orderItemId, cancellationToken)
            ?? throw new KeyNotFoundException($"Item do pedido com ID {orderItemId} não encontrado.");

        var order = await _orderRepository.GetCompleteOrderAsync(item.OrderID, cancellationToken)
            ?? throw new KeyNotFoundException($"Pedido com ID {item.OrderID} não encontrado.");

        if (order.Status != OrderStatus.Approved)
            throw new ValidationException("Itens só podem ser enviados para produção se o pedido estiver Aprovado (Approved).");

        if (order.OrderType != OrderType.Custom)
            throw new ValidationException("Envio para produção só é permitido para pedidos do tipo sob encomenda (Custom).");

        var products = await _productRepository.SearchGetByAsync(item.ProductID.ToString(), 1, 1, cancellationToken);
        var product = products.FirstOrDefault(p => p.ProductID == item.ProductID)
            ?? (await _productRepository.SearchGetByAsync(null, 1, 1000, cancellationToken)).FirstOrDefault(p => p.ProductID == item.ProductID);

        if (product != null && product.ProductType == ProductType.Service)
            throw new ValidationException("Itens do tipo Serviço (Service) não podem ser enviados para produção.");

        item.SentToProductionAt = DateTime.UtcNow;
        item.SentToProductionByEmployeeID = sentToProductionByEmployeeID;

        await _orderRepository.SaveOrderItemAsync(item, cancellationToken);
    }

    private void Require(string permission)
    {
        if (!_authorizationService.HasPermission(permission))
            throw new UnauthorizedAccessException("Você não possui permissão para esta operação.");
    }
}
