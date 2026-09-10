using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Decor.Core.Entities;

public enum GoodsReceiptStatus
{
    Conferido = 1,
    DivergenteDevolvido = 2
}

[Table("goods_receipts")]
public class GoodsReceipt
{
    [Key]
    [Column("GoodsReceiptID")]
    public int GoodsReceiptID { get; set; }

    [Column("PurchaseOrderItemID")]
    public int PurchaseOrderItemID { get; set; }

    [Column("ReceiptDate")]
    public DateTime ReceiptDate { get; set; }

    [Column("QuantityReceived")]
    public decimal QuantityReceived { get; set; }

    [Column("ReceivedByEmployeeID")]
    public int ReceivedByEmployeeID { get; set; }

    [Column("HasDivergence")]
    public bool HasDivergence { get; set; }

    [Column("DivergenceNotes")]
    public string? DivergenceNotes { get; set; }

    [Column("Status")]
    public GoodsReceiptStatus Status { get; set; }
}