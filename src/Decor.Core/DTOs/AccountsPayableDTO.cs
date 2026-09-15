using System.ComponentModel.DataAnnotations;
using Decor.Core.Entities;

namespace Decor.Core.DTOs;

public record AccountsPayableDTO(
    [property: Display(Name = "ID da conta a pagar")] int AccountsPayableID,
    [property: Display(Name = "Tipo do favorecido")] AccountsPayablePayeeType PayeeType,
    [property: Display(Name = "ID do favorecido")] int PayeeID,
    [property: Display(Name = "Descrição")] string Description,
    [property: Display(Name = "Valor")] decimal Amount,
    [property: Display(Name = "Data de vencimento")] DateTime DueDate,
    [property: Display(Name = "Status")] AccountsPayableStatus Status,
    [property: Display(Name = "Tipo da origem")] AccountsPayableSourceType? SourceType,
    [property: Display(Name = "ID da origem")] int? SourceID,
    [property: Display(Name = "Criado por")] int CreatedByEmployeeID,
    [property: Display(Name = "Criado em")] DateTime CreatedAt,
    [property: Display(Name = "Pago em")] DateTime? PaidAt,
    [property: Display(Name = "Pago por")] int? PaidByEmployeeID,
    [property: Display(Name = "ID da Conta de Caixa de Pagamento")] int? PaidFromCashAccountID
);