using System.ComponentModel.DataAnnotations;

namespace Decor.Core.DTOs;

public record OrderItemDTO(
    [property: Display(Name = "ID do item do pedido")] int OrderItemID,
    [property: Display(Name = "Pedido")] int OrderID,
    [property: Display(Name = "Item do orçamento")] int QuoteItemID,
    [property: Display(Name = "Produto")] int ProductID,
    [property: Display(Name = "Quantidade")] decimal Quantity,
    [property: Display(Name = "Preço unitário")] decimal UnitPrice,
    [property: Display(Name = "Serviço de instalação")] bool HasInstallationService,
    [property: Display(Name = "Enviado para produção em")] DateTime? SentToProductionAt,
    [property: Display(Name = "Enviado para produção por")] int? SentToProductionByEmployeeID,
    [property: Display(Name = "Especificações")] IReadOnlyList<OrderItemSpecificationValueDTO>? SpecificationValues = null
);
