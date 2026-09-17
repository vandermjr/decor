using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Decor.Core.Entities;

[Table("unit_of_measures")]
public class UnitOfMeasure
{
    [Key]
    [Column("UnitOfMeasureID")]
    public int UnitOfMeasureID { get; set; }

    [Column("Code")]
    public string Code { get; set; } = string.Empty;

    [Column("Description")]
    public string Description { get; set; } = string.Empty;

    [Column("AllowsFraction")]
    public bool AllowsFraction { get; set; }

    [Column("IsActive")]
    public bool IsActive { get; set; } = true;
}
