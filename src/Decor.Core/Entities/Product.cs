using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Decor.Core.Entities;

[Table("products")]
public class Product
{
    public Product() { }

    [Key]
    [Column("ProductID")] // Opcional
    public int ProductID { get; set; }

    [Column("Barcode")] // Opcional
    public string? Barcode { get; set; }

    [Column("IsActive")] // Opcional
    public bool IsActive { get; set; }

    [Column("Description")] // Opcional
    public string? Description { get; set; }

    [Column("ManufacturerRef")] // Opcional
    public string? ManufacturerRef { get; set; }

    [Column("AuxiliaryRef")] // Opcional
    public string? AuxiliaryRef { get; set; }

    [Column("Dimensions")] // Opcional
    public string? Dimensions { get; set; }

    [Column("Observations")] // Opcional
    public string? Observations { get; set; }

    [Column("StockQuantity")] // Opcional
    public decimal StockQuantity { get; set; }

    [Column("MinimumStock")] // Opcional
    public decimal MinimumStock { get; set; }

    // Chaves Estrangeiras
    [Column("BrandID")] // Opcional
    public int BrandID { get; set; }

    [Column("SubgroupID")] // Opcional
    public int SubgroupID { get; set; }

    // Propriedades de Navegação
    [NotMapped]
    public virtual Brand? Brand { get; set; }
    [NotMapped]
    public virtual Subgroup? Subgroup { get; set; }
}
