using System.ComponentModel.DataAnnotations;
using Decor.Core.Entities;

namespace Decor.Core.DTOs;

public record GoodsReceiptDTO(
    [property: Display(Name = "ID do Recebimento")] int GoodsReceiptID,
    [property: Display(Name = "ID do Item do Pedido de Compra")] int PurchaseOrderItemID,
    [property: Display(Name = "Data do Recebimento")] DateTime ReceiptDate,
    [property: Display(Name = "Quantidade Recebida")] decimal QuantityReceived,
    [property: Display(Name = "ID do Funcionário que Recebeu")] int ReceivedByEmployeeID,
    [property: Display(Name = "Possui Divergência")] bool HasDivergence,
    [property: Display(Name = "Observações da Divergência")] string? DivergenceNotes,
    [property: Display(Name = "Status")] GoodsReceiptStatus Status);