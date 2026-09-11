using System.ComponentModel.DataAnnotations;
using Decor.Core.Entities;

namespace Decor.Core.DTOs;

public record PurchaseOrderDTO(
    [property: Display(Name = "ID do Pedido de Compra")] int PurchaseOrderID,
    [property: Display(Name = "ID do Fornecedor")] int SupplierID,
    [property: Display(Name = "Data do Pedido")] DateTime OrderDate,
    [property: Display(Name = "Status")] PurchaseOrderStatus Status,
    [property: Display(Name = "Observações")] string? Notes);