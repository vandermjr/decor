using System.ComponentModel.DataAnnotations;

namespace Decor.Core.DTOs;

public record InstallmentItemInputDTO(
    [property: Display(Name = "ID da Forma de Pagamento")] int PaymentMethodID,
    [property: Display(Name = "Valor")] decimal Amount,
    [property: Display(Name = "Data de Vencimento")] DateTime DueDate
);
