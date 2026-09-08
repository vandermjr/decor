using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Decor.Core.Entities;

[Table("classes")]
public class Class
{
    [Key]
    [Column("ClassID")] // Opcional
    public int ClassID { get; set; }

    [Column("ClassName")] // Opcional
    public string? ClassName { get; set; }

    public virtual ICollection<Family> Families { get; set; } = [];
}