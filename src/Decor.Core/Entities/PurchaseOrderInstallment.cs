using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Decor.Core.Entities;

public enum PurchaseOrderInstallmentStatus
{
    Pending = 1,
    Paid = 2,
    Overdue = 3,
    Cancelled = 4
}

[Table("purchase_order_installments")]
public class PurchaseOrderInstallment
{
    [Key]
    [Column("InstallmentID")]
    public int InstallmentID { get; set; }

    [Column("PurchaseOrderID")]
    public int PurchaseOrderID { get; set; }

    [Column("PaymentMethodID")]
    public int PaymentMethodID { get; set; }

    [Column("InstallmentNumber")]
    public int InstallmentNumber { get; set; }

    [Column("Amount")]
    public decimal Amount { get; set; }

    [Column("DueDate")]
    public DateTime DueDate { get; set; }

    [Column("Status")]
    public PurchaseOrderInstallmentStatus Status { get; set; }

    [Column("PaidAt")]
    public DateTime? PaidAt { get; set; }

    [Column("PaidByEmployeeID")]
    public int? PaidByEmployeeID { get; set; }

    [Column("PaidFromCashAccountID")]
    public int? PaidFromCashAccountID { get; set; }
}