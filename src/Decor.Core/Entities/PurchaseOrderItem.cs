using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Decor.Core.Entities;

public enum PurchaseOrderReceivingMethod
{
    Transportadora = 1,
    RetiradaPropria = 2
}

public enum PurchaseOrderFinalDestination
{
    DepositoEmpresa = 1,
    DiretoCliente = 2
}

[Table("purchase_order_items")]
public class PurchaseOrderItem
{
    [Key]
    [Column("PurchaseOrderItemID")]
    public int PurchaseOrderItemID { get; set; }

    [Column("PurchaseOrderID")]
    public int PurchaseOrderID { get; set; }

    [Column("ProductID")]
    public int ProductID { get; set; }

    [Column("QuantityOrdered")]
    public decimal QuantityOrdered { get; set; }

    [Column("UnitPrice")]
    public decimal UnitPrice { get; set; }

    [Column("ReceivingMethod")]
    public PurchaseOrderReceivingMethod ReceivingMethod { get; set; }

    [Column("FinalDestination")]
    public PurchaseOrderFinalDestination FinalDestination { get; set; }

    [Column("StockLocationID")]
    public int? StockLocationID { get; set; }

    [Column("CustomerID")]
    public int? CustomerID { get; set; }
}