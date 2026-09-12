using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Decor.Core.Entities;

public enum PaymentTiming
{
    Cash = 1,
    Term = 2
}

[Table("payment_methods")]
public class PaymentMethod
{
    [Key]
    [Column("PaymentMethodID")]
    public int PaymentMethodID { get; set; }

    [Column("Name")]
    public string Name { get; set; } = string.Empty;

    [Column("Timing")]
    public PaymentTiming Timing { get; set; }

    [Column("IsActive")]
    public bool IsActive { get; set; } = true;
}
