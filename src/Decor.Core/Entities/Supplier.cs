using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Decor.Core.Entities;

[Table("suppliers")]
public class Supplier
{
    [Key]
    [Column("SupplierID")]
    public int SupplierID { get; set; }

    [Column("CorporateName")]
    public string CorporateName { get; set; } = string.Empty;

    [Column("Document")]
    public string? Document { get; set; } // CNPJ

    [Column("Phone")]
    public string? Phone { get; set; }

    [Column("Email")]
    public string? Email { get; set; }

    [Column("IsActive")]
    public bool IsActive { get; set; }
}