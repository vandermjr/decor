using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Decor.Core.Entities;

public enum OrderInstallmentStatus
{
    Pending = 1,
    Paid = 2,
    Overdue = 3,
    Cancelled = 4
}

[Table("order_installments")]
public class OrderInstallment
{
    [Key]
    [Column("InstallmentID")]
    public int InstallmentID { get; set; }

    [Column("OrderID")]
    public int OrderID { get; set; }

    [Column("PaymentMethodID")]
    public int PaymentMethodID { get; set; }

    [Column("InstallmentNumber")]
    public int InstallmentNumber { get; set; }

    [Column("Amount")]
    public decimal Amount { get; set; }

    [Column("DueDate")]
    public DateTime DueDate { get; set; }

    [Column("Status")]
    public OrderInstallmentStatus Status { get; set; }

    [Column("PaidAt")]
    public DateTime? PaidAt { get; set; }

    [Column("ReceivedByEmployeeID")]
    public int? ReceivedByEmployeeID { get; set; }
}
