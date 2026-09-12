using System.ComponentModel.DataAnnotations;
using Decor.Core.Entities;

namespace Decor.Core.DTOs;

public record OrderInstallmentDTO(
    [property: Display(Name = "ID da Parcela")] int InstallmentID,
    [property: Display(Name = "ID do Pedido")] int OrderID,
    [property: Display(Name = "ID da Forma de Pagamento")] int PaymentMethodID,
    [property: Display(Name = "Número da Parcela")] int InstallmentNumber,
    [property: Display(Name = "Valor")] decimal Amount,
    [property: Display(Name = "Data de Vencimento")] DateTime DueDate,
    [property: Display(Name = "Status")] OrderInstallmentStatus Status,
    [property: Display(Name = "Data do Pagamento")] DateTime? PaidAt,
    [property: Display(Name = "ID do Funcionário Recebedor")] int? ReceivedByEmployeeID
);
