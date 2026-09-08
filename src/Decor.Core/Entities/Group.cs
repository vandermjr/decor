using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Decor.Core.Entities;

[Table("groups")]
public class Group
{
    public Group()
    {
        Family = new();
    }

    [Key]
    [Column("GroupID")] // Opcional
    public int GroupID { get; set; }

    [Column("GroupName")] // Opcional
    public string? GroupName { get; set; }

    [Column("FamilyID")] // Opcional
    public int FamilyID { get; set; }

    public virtual Family Family { get; set; }
    public virtual ICollection<Subgroup> Subgroups { get; set; } = [];
}