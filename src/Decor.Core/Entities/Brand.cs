using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Decor.Core.Entities;

[Table("brands")]
public class Brand
{
    [Key]
    [Column("BrandID")] // Opcional
    public int BrandID { get; set; }

    [Column("BrandName")] // Opcional
    public string? BrandName { get; set; }
}