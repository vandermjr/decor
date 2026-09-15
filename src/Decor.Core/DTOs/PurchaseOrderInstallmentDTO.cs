using System.ComponentModel.DataAnnotations;
using Decor.Core.Entities;

namespace Decor.Core.DTOs;

public record PurchaseOrderInstallmentDTO(
    [property: Display(Name = "ID da Parcela")] int InstallmentID,
    [property: Display(Name = "ID do Pedido de Compra")] int PurchaseOrderID,
    [property: Display(Name = "ID da Forma de Pagamento")] int PaymentMethodID,
    [property: Display(Name = "Número da Parcela")] int InstallmentNumber,
    [property: Display(Name = "Valor")] decimal Amount,
    [property: Display(Name = "Data de Vencimento")] DateTime DueDate,
    [property: Display(Name = "Status")] PurchaseOrderInstallmentStatus Status,
    [property: Display(Name = "Data do Pagamento")] DateTime? PaidAt,
    [property: Display(Name = "ID do Funcionário Pagador")] int? PaidByEmployeeID,
    [property: Display(Name = "ID da Conta de Caixa de Pagamento")] int? PaidFromCashAccountID
);