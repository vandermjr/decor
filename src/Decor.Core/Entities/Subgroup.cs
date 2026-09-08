using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Decor.Core.Entities;

[Table("subgroups")]
public class Subgroup
{
    public Subgroup()
    {
        Group = new();
    }

    [Key]
    [Column("SubgroupID")] // Opcional
    public int SubgroupID { get; set; }

    [Column("SubgroupName")] // Opcional
    public string? SubgroupName { get; set; }

    [Column("GroupID")] // Opcional
    public int GroupID { get; set; }

    public virtual Group Group { get; set; }
}