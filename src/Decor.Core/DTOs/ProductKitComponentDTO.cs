using System.ComponentModel.DataAnnotations;

namespace Decor.Core.DTOs;

public record ProductKitComponentDTO(
    [property: Display(Name = "ID do Componente")] int ComponentID,
    [property: Display(Name = "ID do Kit")] int KitProductID,
    [property: Display(Name = "ID do Componente (Produto)")] int ComponentProductID,
    [property: Display(Name = "Quantidade")] decimal Quantity,
    [property: Display(Name = "Visível ao Cliente")] bool IsVisibleToCustomer,
    [property: Display(Name = "Ordem de Exibição")] int DisplayOrder);
