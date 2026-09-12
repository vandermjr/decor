using System.ComponentModel.DataAnnotations;

namespace Decor.Core.DTOs;

public record QuoteItemSpecificationValueDTO(
    [property: Display(Name = "ID do valor")] int ValueID,
    [property: Display(Name = "Item")] int QuoteItemID,
    [property: Display(Name = "Atributo")] int AttributeID,
    [property: Display(Name = "Valor")] string Value
);
