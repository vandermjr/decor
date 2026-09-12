using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Decor.Core.Entities;

[Table("order_items")]
public class OrderItem
{
    [Key]
    [Column("OrderItemID")]
    public int OrderItemID { get; set; }

    [Column("OrderID")]
    public int OrderID { get; set; }

    [Column("QuoteItemID")]
    public int QuoteItemID { get; set; }

    [Column("ProductID")]
    public int ProductID { get; set; }

    [Column("Quantity")]
    public decimal Quantity { get; set; }

    [Column("UnitPrice")]
    public decimal UnitPrice { get; set; }

    [Column("HasInstallationService")]
    public bool HasInstallationService { get; set; }

    [Column("SentToProductionAt")]
    public DateTime? SentToProductionAt { get; set; }

    [Column("SentToProductionByEmployeeID")]
    public int? SentToProductionByEmployeeID { get; set; }

    [NotMapped]
    public ICollection<OrderItemSpecificationValue> SpecificationValues { get; set; } = new List<OrderItemSpecificationValue>();
}
