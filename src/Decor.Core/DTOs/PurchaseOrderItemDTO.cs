using System.ComponentModel.DataAnnotations;
using Decor.Core.Entities;

namespace Decor.Core.DTOs;

public record PurchaseOrderItemDTO(
    [property: Display(Name = "ID do Item do Pedido de Compra")] int PurchaseOrderItemID,
    [property: Display(Name = "ID do Pedido de Compra")] int PurchaseOrderID,
    [property: Display(Name = "ID do Produto")] int ProductID,
    [property: Display(Name = "Quantidade Solicitada")] decimal QuantityOrdered,
    [property: Display(Name = "Preço Unitário")] decimal UnitPrice,
    [property: Display(Name = "Modalidade de Recebimento")] PurchaseOrderReceivingMethod ReceivingMethod,
    [property: Display(Name = "Destino Final")] PurchaseOrderFinalDestination FinalDestination,
    [property: Display(Name = "ID do Depósito")] int? StockLocationID,
    [property: Display(Name = "ID do Cliente")] int? CustomerID);