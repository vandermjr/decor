using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Decor.Core.Entities;

[Table("roles")]
public class Role
{
    [Key]
    [Column("RoleID")]
    public int RoleID { get; set; }

    [Column("RoleName")]
    public string? RoleName { get; set; }

    [Column("Description")]
    public string? Description { get; set; }

    [Column("HierarchyLevel")]
    public int HierarchyLevel { get; set; }

    [Column("IsSystemProtected")]
    public bool IsSystemProtected { get; set; }
}
