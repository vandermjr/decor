using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Decor.Core.Entities;

[Table("employees")]
public class Employee
{
    [Key]
    [Column("EmployeeID")]
    public int EmployeeID { get; set; }

    [Column("Name")]
    public string Name { get; set; } = string.Empty;

    [Column("Document")]
    public string? Document { get; set; } // CPF

    [Column("Phone")]
    public string? Phone { get; set; }

    [Column("IsActive")]
    public bool IsActive { get; set; }

    // Nulo quando o funcionário não tem login no sistema (D2)
    [Column("UserID")]
    public int? UserID { get; set; }

    [NotMapped]
    public virtual ApplicationUser? User { get; set; }
}