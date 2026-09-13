using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Decor.Core.Entities;

[Table("service_execution_records")]
public class ServiceExecutionRecord
{
    [Key]
    [Column("ExecutionID")] public int ExecutionID { get; set; }
    [Column("AppointmentID")] public int AppointmentID { get; set; }
    [Column("ExecutedAt")] public DateTime ExecutedAt { get; set; }
    [Column("Observations")] public string? Observations { get; set; }
    [Column("CustomerPresent")] public bool CustomerPresent { get; set; }
    [Column("CustomerSignedConfirmation")] public bool? CustomerSignedConfirmation { get; set; }
    [Column("AbsentAuthorizationNote")] public string? AbsentAuthorizationNote { get; set; }
}
