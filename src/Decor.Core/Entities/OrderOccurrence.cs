using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Decor.Core.Entities;

[Table("order_occurrences")]
public class OrderOccurrence
{
    [Key]
    [Column("OccurrenceID")]
    public int OccurrenceID { get; set; }

    [Column("OrderID")]
    public int OrderID { get; set; }

    [Column("ReasonID")]
    public int ReasonID { get; set; }

    [Column("RegisteredByEmployeeID")]
    public int RegisteredByEmployeeID { get; set; }

    [Column("RegisteredAt")]
    public DateTime RegisteredAt { get; set; }

    [Column("Observation")]
    public string Observation { get; set; } = string.Empty;

    [Column("NewManufacturingDeadline")]
    public DateTime? NewManufacturingDeadline { get; set; }

    [Column("NewInstallationDeadline")]
    public DateTime? NewInstallationDeadline { get; set; }
}