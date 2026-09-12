using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Decor.Core.Entities;

public enum OrderType
{
    Catalog = 1,
    Custom = 2
}

public enum OrderStatus
{
    PendingApproval = 1,
    Approved = 2,
    InProduction = 3,
    ReadyForDelivery = 4,
    PartiallyDelivered = 5,
    Delivered = 6,
    Cancelled = 7
}

[Table("orders")]
public class Order
{
    [Key]
    [Column("OrderID")]
    public int OrderID { get; set; }

    [Column("QuoteSectionID")]
    public int QuoteSectionID { get; set; }

    [Column("CustomerID")]
    public int CustomerID { get; set; }

    [Column("OrderType")]
    public OrderType OrderType { get; set; }

    [Column("Status")]
    public OrderStatus Status { get; set; }

    [Column("RequiresDownPayment")]
    public bool? RequiresDownPayment { get; set; }

    [Column("ManufacturingDeadline")]
    public DateTime? ManufacturingDeadline { get; set; }

    [Column("InstallationDeadline")]
    public DateTime? InstallationDeadline { get; set; }

    [Column("CreatedAt")]
    public DateTime CreatedAt { get; set; }

    [NotMapped]
    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
}
