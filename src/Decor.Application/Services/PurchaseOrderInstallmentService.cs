using System.ComponentModel.DataAnnotations;
using Decor.Application.Mappers;
using Decor.Core.Common;
using Decor.Core.DTOs;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Repositories;
using Decor.Core.Interfaces.Services;
using Decor.Core.Validation;

namespace Decor.Application.Services;

public class PurchaseOrderInstallmentService(
    IPurchaseOrderInstallmentRepository installmentRepository,
    IPurchaseOrderRepository purchaseOrderRepository,
    IPurchaseOrderItemRepository purchaseOrderItemRepository,
    IPaymentMethodRepository paymentMethodRepository,
    IDTOValidator<PurchaseOrderInstallmentDTO> dtoValidator,
    IRepositoryValidator<PurchaseOrderInstallment> repoValidator,
    IAuthorizationService authorizationService) : IPurchaseOrderInstallmentService
{
    public async Task<PurchaseOrderInstallmentDTO> GetInstallmentByIdAsync(int installmentId, CancellationToken cancellationToken = default)
    {
        Require(DecorPermissions.PurchaseOrderInstallmentsView);
        var installment = await installmentRepository.GetByIdAsync(installmentId, cancellationToken)
            ?? throw new KeyNotFoundException($"Parcela de pedido de compra com ID {installmentId} não encontrada.");
        return installment.ToDTO();
    }

    public async Task<IEnumerable<PurchaseOrderInstallmentDTO>> GetInstallmentsByPurchaseOrderIdAsync(int purchaseOrderId, CancellationToken cancellationToken = default)
    {
        Require(DecorPermissions.PurchaseOrderInstallmentsView);
        return (await installmentRepository.GetByPurchaseOrderIdAsync(purchaseOrderId, cancellationToken)).ToDTO();
    }

    public async Task<IEnumerable<PurchaseOrderInstallmentDTO>> CreateInstallmentPlanAsync(int purchaseOrderId, IEnumerable<InstallmentItemInputDTO> items, CancellationToken cancellationToken = default)
    {
        Require(DecorPermissions.PurchaseOrderInstallmentsCreatePlan);
        var itemList = items?.ToList() ?? [];
        if (itemList.Count == 0) throw new ValidationException("Ao menos uma parcela deve ser informada para criar o plano de pagamento.");

        var purchaseOrder = (await purchaseOrderRepository.SearchGetByAsync(purchaseOrderId.ToString(), 1, 1, cancellationToken)).FirstOrDefault()
            ?? throw new KeyNotFoundException($"Pedido de compra com ID {purchaseOrderId} não encontrado.");
        if (purchaseOrder.Status is PurchaseOrderStatus.Recebido or PurchaseOrderStatus.Cancelado)
            throw new ValidationException("O plano de pagamento só pode ser criado para pedidos de compra abertos ou parcialmente recebidos.");

        if ((await installmentRepository.GetByPurchaseOrderIdAsync(purchaseOrderId, cancellationToken)).Count > 0)
            throw new ValidationException($"Já existe um plano de pagamento cadastrado para o pedido de compra ID {purchaseOrderId}.");

        var purchaseOrderItems = await purchaseOrderItemRepository.GetByPurchaseOrderIdAsync(purchaseOrderId, cancellationToken);
        var purchaseOrderTotal = purchaseOrderItems.Sum(i => i.QuantityOrdered * i.UnitPrice);
        var installmentsTotal = itemList.Sum(i => i.Amount);
        if (Math.Abs(purchaseOrderTotal - installmentsTotal) > 0.01m)
            throw new ValidationException($"A soma do valor das parcelas ({installmentsTotal:C2}) diverge do valor total do pedido de compra ({purchaseOrderTotal:C2}).");

        var created = new List<PurchaseOrderInstallment>();
        var number = 1;
        foreach (var item in itemList)
        {
            if (item.Amount <= 0m) throw new ValidationException("O valor de cada parcela deve ser maior que zero.");
            var paymentMethod = (await paymentMethodRepository.SearchGetByAsync(item.PaymentMethodID.ToString(), 1, 100, cancellationToken))
                .FirstOrDefault(p => p.PaymentMethodID == item.PaymentMethodID);
            if (paymentMethod == null || !paymentMethod.IsActive)
                throw new ValidationException($"Forma de pagamento com ID {item.PaymentMethodID} não encontrada ou inativa.");

            var installment = new PurchaseOrderInstallment
            {
                PurchaseOrderID = purchaseOrderId, PaymentMethodID = item.PaymentMethodID, InstallmentNumber = number++,
                Amount = item.Amount, DueDate = item.DueDate, Status = PurchaseOrderInstallmentStatus.Pending
            };
            var dtoErrors = dtoValidator.Validate(installment.ToDTO());
            if (dtoErrors.Any()) throw new ValidationException(string.Join("\n", dtoErrors));
            var repoErrors = repoValidator.Validate(installment);
            if (repoErrors.Any()) throw new ValidationException(string.Join("\n", repoErrors));
            await installmentRepository.SaveAsync(installment, cancellationToken);
            created.Add(installment);
        }
        return created.ToDTO();
    }

    public Task RegisterPaymentAsync(int installmentId, int paidByEmployeeId, CancellationToken cancellationToken = default)
        => ChangeStatusAsync(installmentId, PurchaseOrderInstallmentStatus.Paid, DecorPermissions.PurchaseOrderInstallmentsRegisterPayment, "Pendente ou Vencida", paidByEmployeeId, cancellationToken);

    public Task MarkOverdueAsync(int installmentId, CancellationToken cancellationToken = default)
        => ChangeStatusAsync(installmentId, PurchaseOrderInstallmentStatus.Overdue, DecorPermissions.PurchaseOrderInstallmentsMarkOverdue, "Pendente", null, cancellationToken);

    public Task CancelInstallmentAsync(int installmentId, CancellationToken cancellationToken = default)
        => ChangeStatusAsync(installmentId, PurchaseOrderInstallmentStatus.Cancelled, DecorPermissions.PurchaseOrderInstallmentsCancel, "Pendente ou Vencida", null, cancellationToken);

    public async Task CancelInstallmentsForPurchaseOrderAsync(int purchaseOrderId, CancellationToken cancellationToken = default)
    {
        foreach (var installment in await installmentRepository.GetByPurchaseOrderIdAsync(purchaseOrderId, cancellationToken))
        {
            if (installment.Status is PurchaseOrderInstallmentStatus.Pending or PurchaseOrderInstallmentStatus.Overdue)
            {
                installment.Status = PurchaseOrderInstallmentStatus.Cancelled;
                await installmentRepository.SaveAsync(installment, cancellationToken);
            }
        }
    }

    private async Task ChangeStatusAsync(int id, PurchaseOrderInstallmentStatus status, string permission, string allowedStatuses, int? paidByEmployeeId, CancellationToken cancellationToken)
    {
        Require(permission);
        var installment = await installmentRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException($"Parcela de pedido de compra com ID {id} não encontrada.");
        var allowed = status == PurchaseOrderInstallmentStatus.Overdue
            ? installment.Status == PurchaseOrderInstallmentStatus.Pending
            : installment.Status is PurchaseOrderInstallmentStatus.Pending or PurchaseOrderInstallmentStatus.Overdue;
        if (!allowed) throw new ValidationException($"Apenas parcelas com status {allowedStatuses} podem sofrer esta operação.");
        installment.Status = status;
        if (status == PurchaseOrderInstallmentStatus.Paid)
        {
            installment.PaidAt = DateTime.UtcNow;
            installment.PaidByEmployeeID = paidByEmployeeId;
        }
        await installmentRepository.SaveAsync(installment, cancellationToken);
    }

    private void Require(string permission)
    {
        if (!authorizationService.HasPermission(permission)) throw new UnauthorizedAccessException("Você não possui permissão para esta operação.");
    }
}