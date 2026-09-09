using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Decor.Core.Entities;

[Table("customers")]
public class Customer
{
    [Key]
    [Column("CustomerID")]
    public int CustomerID { get; set; }

    [Column("Name")]
    public string Name { get; set; } = string.Empty;

    [Column("Document")]
    public string? Document { get; set; } // CPF ou CNPJ

    [Column("Phone")]
    public string? Phone { get; set; }

    [Column("Email")]
    public string? Email { get; set; }

    [Column("Address")]
    public string? Address { get; set; }

    [Column("IsActive")]
    public bool IsActive { get; set; }
}