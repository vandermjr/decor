using System.ComponentModel.DataAnnotations;

namespace Decor.Core.DTOs;

public record OrderItemSpecificationValueDTO(
    [property: Display(Name = "ID da especificação")] int ValueID,
    [property: Display(Name = "Item do pedido")] int OrderItemID,
    [property: Display(Name = "Atributo")] int AttributeID,
    [property: Display(Name = "Valor")] string Value
);
