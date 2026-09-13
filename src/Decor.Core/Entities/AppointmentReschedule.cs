using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Decor.Core.Entities;

[Table("appointment_reschedules")]
public class AppointmentReschedule
{
    [Key]
    [Column("RescheduleID")] public int RescheduleID { get; set; }
    [Column("AppointmentID")] public int AppointmentID { get; set; }
    [Column("PreviousDate")] public DateTime PreviousDate { get; set; }
    [Column("NewDate")] public DateTime NewDate { get; set; }
    [Column("Reason")] public string Reason { get; set; } = string.Empty;
    [Column("RegisteredByEmployeeID")] public int RegisteredByEmployeeID { get; set; }
    [Column("RegisteredAt")] public DateTime RegisteredAt { get; set; }
}