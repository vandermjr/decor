using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Decor.Core.Entities;

public enum PurchaseOrderStatus
{
    Aberto = 1,
    ParcialmenteRecebido = 2,
    Recebido = 3,
    Cancelado = 4
}

[Table("purchase_orders")]
public class PurchaseOrder
{
    [Key]
    [Column("PurchaseOrderID")]
    public int PurchaseOrderID { get; set; }

    [Column("SupplierID")]
    public int SupplierID { get; set; }

    [Column("OrderDate")]
    public DateTime OrderDate { get; set; }

    [Column("Status")]
    public PurchaseOrderStatus Status { get; set; }

    [Column("Notes")]
    public string? Notes { get; set; }
}