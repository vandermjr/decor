using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Decor.Core.Entities;

// BOM (Bill of Materials) de um Product composto/fabricado (Kit).
[Table("product_kit_components")]
public class ProductKitComponent
{
    [Key]
    [Column("ComponentID")]
    public int ComponentID { get; set; }

    [Column("KitProductID")]
    public int KitProductID { get; set; }

    [Column("ComponentProductID")]
    public int ComponentProductID { get; set; }

    [Column("Quantity")]
    public decimal Quantity { get; set; }

    [Column("IsVisibleToCustomer")]
    public bool IsVisibleToCustomer { get; set; }

    [Column("DisplayOrder")]
    public int DisplayOrder { get; set; }
}
