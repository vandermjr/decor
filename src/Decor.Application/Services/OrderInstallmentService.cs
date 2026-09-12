using System.ComponentModel.DataAnnotations;
using Decor.Application.Mappers;
using Decor.Core.Common;
using Decor.Core.DTOs;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Repositories;
using Decor.Core.Interfaces.Services;
using Decor.Core.Validation;

namespace Decor.Application.Services;

public class OrderInstallmentService(
    IOrderInstallmentRepository orderInstallmentRepository,
    IOrderRepository orderRepository,
    IPaymentMethodRepository paymentMethodRepository,
    IDTOValidator<OrderInstallmentDTO> dtoValidator,
    IRepositoryValidator<OrderInstallment> repoValidator,
    IAuthorizationService authorizationService) : IOrderInstallmentService
{
    private readonly IOrderInstallmentRepository _orderInstallmentRepository = orderInstallmentRepository;
    private readonly IOrderRepository _orderRepository = orderRepository;
    private readonly IPaymentMethodRepository _paymentMethodRepository = paymentMethodRepository;
    private readonly IDTOValidator<OrderInstallmentDTO> _dtoValidator = dtoValidator;
    private readonly IRepositoryValidator<OrderInstallment> _repoValidator = repoValidator;
    private readonly IAuthorizationService _authorizationService = authorizationService;

    public async Task<OrderInstallmentDTO> GetInstallmentByIdAsync(int installmentId, CancellationToken cancellationToken = default)
    {
        Require(DecorPermissions.OrderInstallmentsView);

        var installment = await _orderInstallmentRepository.GetByIdAsync(installmentId, cancellationToken)
            ?? throw new KeyNotFoundException($"Parcela com ID {installmentId} não encontrada.");

        return installment.ToDTO();
    }

    public async Task<IEnumerable<OrderInstallmentDTO>> GetInstallmentsByOrderIdAsync(int orderId, CancellationToken cancellationToken = default)
    {
        Require(DecorPermissions.OrderInstallmentsView);

        var installments = await _orderInstallmentRepository.GetByOrderIdAsync(orderId, cancellationToken);
        return installments.ToDTO();
    }

    public async Task<IEnumerable<OrderInstallmentDTO>> CreateInstallmentPlanAsync(int orderId, IEnumerable<InstallmentItemInputDTO> items, CancellationToken cancellationToken = default)
    {
        Require(DecorPermissions.OrderInstallmentsCreatePlan);

        var itemList = items?.ToList() ?? new List<InstallmentItemInputDTO>();
        if (itemList.Count == 0)
            throw new ValidationException("Ao menos uma parcela deve ser informada para criar o plano de pagamento.");

        var order = await _orderRepository.GetCompleteOrderAsync(orderId, cancellationToken)
            ?? throw new KeyNotFoundException($"Pedido com ID {orderId} não encontrado.");

        if (order.Status != OrderStatus.Approved)
            throw new ValidationException("O plano de pagamento só pode ser criado para pedidos com status Aprovado (Approved).");

        var existingInstallments = await _orderInstallmentRepository.GetByOrderIdAsync(orderId, cancellationToken);
        if (existingInstallments.Count > 0)
            throw new ValidationException($"Já existe um plano de pagamento cadastrado para o pedido ID {orderId}.");

        var orderTotal = order.Items.Sum(i => i.Quantity * i.UnitPrice);
        var installmentsTotal = itemList.Sum(i => i.Amount);

        if (Math.Abs(orderTotal - installmentsTotal) > 0.01m)
            throw new ValidationException($"A soma do valor das parcelas ({installmentsTotal:C2}) diverge do valor total do pedido ({orderTotal:C2}).");

        var createdInstallments = new List<OrderInstallment>();
        int installmentNumber = 1;

        foreach (var item in itemList)
        {
            if (item.Amount <= 0m)
                throw new ValidationException("O valor de cada parcela deve ser maior que zero.");

            var paymentMethods = await _paymentMethodRepository.SearchGetByAsync(item.PaymentMethodID.ToString(), 1, 100, cancellationToken);
            var paymentMethod = paymentMethods.FirstOrDefault(p => p.PaymentMethodID == item.PaymentMethodID);

            if (paymentMethod == null || !paymentMethod.IsActive)
                throw new ValidationException($"Forma de pagamento com ID {item.PaymentMethodID} não encontrada ou inativa.");

            var installment = new OrderInstallment
            {
                OrderID = orderId,
                PaymentMethodID = item.PaymentMethodID,
                InstallmentNumber = installmentNumber++,
                Amount = item.Amount,
                DueDate = item.DueDate,
                Status = OrderInstallmentStatus.Pending,
                PaidAt = null,
                ReceivedByEmployeeID = null
            };

            var dtoErrors = _dtoValidator.Validate(installment.ToDTO());
            if (dtoErrors.Any())
                throw new ValidationException(string.Join("\n", dtoErrors));

            var repoErrors = _repoValidator.Validate(installment);
            if (repoErrors.Any())
                throw new ValidationException(string.Join("\n", repoErrors));

            await _orderInstallmentRepository.SaveAsync(installment, cancellationToken);
            createdInstallments.Add(installment);
        }

        return createdInstallments.ToDTO();
    }

    public async Task RegisterPaymentAsync(int installmentId, int receivedByEmployeeId, CancellationToken cancellationToken = default)
    {
        Require(DecorPermissions.OrderInstallmentsRegisterPayment);

        var installment = await _orderInstallmentRepository.GetByIdAsync(installmentId, cancellationToken)
            ?? throw new KeyNotFoundException($"Parcela com ID {installmentId} não encontrada.");

        if (installment.Status != OrderInstallmentStatus.Pending && installment.Status != OrderInstallmentStatus.Overdue)
            throw new ValidationException("Apenas parcelas com status Pendente ou Vencida podem ter o pagamento registrado.");

        installment.PaidAt = DateTime.UtcNow;
        installment.ReceivedByEmployeeID = receivedByEmployeeId;
        installment.Status = OrderInstallmentStatus.Paid;

        await _orderInstallmentRepository.SaveAsync(installment, cancellationToken);
    }

    public async Task MarkOverdueAsync(int installmentId, CancellationToken cancellationToken = default)
    {
        Require(DecorPermissions.OrderInstallmentsMarkOverdue);

        var installment = await _orderInstallmentRepository.GetByIdAsync(installmentId, cancellationToken)
            ?? throw new KeyNotFoundException($"Parcela com ID {installmentId} não encontrada.");

        if (installment.Status != OrderInstallmentStatus.Pending)
            throw new ValidationException("Apenas parcelas com status Pendente podem ser marcadas como vencidas.");

        installment.Status = OrderInstallmentStatus.Overdue;

        await _orderInstallmentRepository.SaveAsync(installment, cancellationToken);
    }

    public async Task CancelInstallmentAsync(int installmentId, CancellationToken cancellationToken = default)
    {
        Require(DecorPermissions.OrderInstallmentsCancel);

        var installment = await _orderInstallmentRepository.GetByIdAsync(installmentId, cancellationToken)
            ?? throw new KeyNotFoundException($"Parcela com ID {installmentId} não encontrada.");

        if (installment.Status != OrderInstallmentStatus.Pending && installment.Status != OrderInstallmentStatus.Overdue)
            throw new ValidationException("Apenas parcelas com status Pendente ou Vencida podem ser canceladas.");

        installment.Status = OrderInstallmentStatus.Cancelled;

        await _orderInstallmentRepository.SaveAsync(installment, cancellationToken);
    }

    public async Task CancelInstallmentsForOrderAsync(int orderId, CancellationToken cancellationToken = default)
    {
        // Usado internamente no cancelamento de pedido
        var installments = await _orderInstallmentRepository.GetByOrderIdAsync(orderId, cancellationToken);
        foreach (var installment in installments)
        {
            if (installment.Status == OrderInstallmentStatus.Pending || installment.Status == OrderInstallmentStatus.Overdue)
            {
                installment.Status = OrderInstallmentStatus.Cancelled;
                await _orderInstallmentRepository.SaveAsync(installment, cancellationToken);
            }
        }
    }

    private void Require(string permission)
    {
        if (!_authorizationService.HasPermission(permission))
            throw new UnauthorizedAccessException("Você não possui permissão para esta operação.");
    }
}
