using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Decor.Core.Entities;

[Table("permissions")]
public class Permission
{
    [Key]
    [Column("PermissionID")]
    public int PermissionID { get; set; }

    [Column("PermissionCode")]
    public string? PermissionCode { get; set; }

    [Column("Description")]
    public string? Description { get; set; }
}
