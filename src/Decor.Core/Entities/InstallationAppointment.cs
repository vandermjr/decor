using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Decor.Core.Entities;

public enum InstallationAppointmentStatus
{
    Scheduled = 1,
    Rescheduled = 2,
    Completed = 3,
    Cancelled = 4
}

[Table("installation_appointments")]
public class InstallationAppointment
{
    [Key]
    [Column("AppointmentID")] public int AppointmentID { get; set; }
    [Column("OrderItemID")] public int OrderItemID { get; set; }
    [Column("ScheduledDate")] public DateTime ScheduledDate { get; set; }
    [Column("ScheduledTime")] public TimeSpan? ScheduledTime { get; set; }
    [Column("ExecutorEmployeeID")] public int? ExecutorEmployeeID { get; set; }
    [Column("ExecutorPartnerID")] public int? ExecutorPartnerID { get; set; }
    [Column("Status")] public InstallationAppointmentStatus Status { get; set; }
    [Column("CreatedByEmployeeID")] public int CreatedByEmployeeID { get; set; }
    [Column("CreatedAt")] public DateTime CreatedAt { get; set; }
}