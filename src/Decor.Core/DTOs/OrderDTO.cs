using System.ComponentModel.DataAnnotations;

namespace Decor.Core.DTOs;

public record OrderDTO(
    [property: Display(Name = "ID do pedido")] int OrderID,
    [property: Display(Name = "Seção do orçamento")] int QuoteSectionID,
    [property: Display(Name = "Cliente")] int CustomerID,
    [property: Display(Name = "Tipo")] int OrderType,
    [property: Display(Name = "Status")] int Status,
    [property: Display(Name = "Requer entrada")] bool? RequiresDownPayment,
    [property: Display(Name = "Prazo de fabricação")] DateTime? ManufacturingDeadline,
    [property: Display(Name = "Prazo de instalação")] DateTime? InstallationDeadline,
    [property: Display(Name = "Data de criação")] DateTime CreatedAt,
    [property: Display(Name = "Itens do pedido")] IReadOnlyList<OrderItemDTO>? Items = null
);
