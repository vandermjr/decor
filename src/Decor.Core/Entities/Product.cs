using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Decor.Core.Entities;

public enum ProductOrigin
{
    Compra = 1,       // comprado pronto de fornecedor
    Manufatura = 2    // produzido via Ordem de Fabricação (ex: cortina)
}

public enum ProductAcquisitionMode
{
    Estocado = 1,      // mantém saldo em depósito
    SobEncomenda = 2   // comprado especificamente por pedido aprovado
}

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

    [Column("Origin")]
    public ProductOrigin Origin { get; set; }

    [Column("AcquisitionMode")]
    public ProductAcquisitionMode AcquisitionMode { get; set; }
    
    // Propriedades de Navegação
    [NotMapped]
    public virtual Brand? Brand { get; set; }
    [NotMapped]
    public virtual Subgroup? Subgroup { get; set; }
}
