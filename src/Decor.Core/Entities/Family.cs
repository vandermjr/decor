using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Decor.Core.Entities;

[Table("families")]
public class Family
{
    public Family()
    {
        Class = new();
    }

    [Key]
    [Column("FamilyID")] // Opcional
    public int FamilyID { get; set; }

    [Column("FamilyName")] // Opcional
    public string? FamilyName { get; set; }

    [Column("ClassID")] // Opcional
    public int ClassID { get; set; }

    public virtual Class Class { get; set; }
    public virtual ICollection<Group> Groups { get; set; } = [];
}