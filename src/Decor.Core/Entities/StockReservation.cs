using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Decor.Core.Entities;

public enum StockReservationStatus
{
    Active = 1,
    Released = 2,
    Consumed = 3
}

[Table("stock_reservations")]
public class StockReservation
{
    [Key]
    [Column("ReservationID")]
    public int ReservationID { get; set; }

    [Column("OrderItemID")]
    public int OrderItemID { get; set; }

    [Column("ProductID")]
    public int ProductID { get; set; }

    [Column("StockLocationID")]
    public int StockLocationID { get; set; }

    [Column("Quantity")]
    public decimal Quantity { get; set; }

    [Column("Status")]
    public StockReservationStatus Status { get; set; }

    [Column("CreatedByEmployeeID")]
    public int CreatedByEmployeeID { get; set; }

    [Column("CreatedAt")]
    public DateTime CreatedAt { get; set; }

    [Column("ReleasedAt")]
    public DateTime? ReleasedAt { get; set; }
}
