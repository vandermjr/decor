using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Decor.Core.Entities;

[Table("occurrence_reasons")]
public class OccurrenceReason
{
    [Key]
    [Column("ReasonID")]
    public int ReasonID { get; set; }

    [Column("Description")]
    public string Description { get; set; } = string.Empty;

    [Column("IsActive")]
    public bool IsActive { get; set; } = true;
}