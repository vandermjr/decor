using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Decor.Core.Entities;

public enum ProductSpecificationDataType
{
    Text = 1,
    Number = 2,
    Boolean = 3,
    Enum = 4
}

public enum MeasurementRole
{
    None = 0,
    Width = 1,
    Height = 2
}

[Table("product_specification_attributes")]
public class ProductSpecificationAttribute
{
    [Key]
    [Column("AttributeID")]
    public int AttributeID { get; set; }

    // FK para o subgrupo, categoria/tipo de produto já usada por Product.SubgroupID
    [Column("ProductCategoryID")]
    public int ProductCategoryID { get; set; }

    [Column("Name")]
    public string? Name { get; set; }

    [Column("DataType")]
    public ProductSpecificationDataType DataType { get; set; }

    [Column("Unit")]
    public string? Unit { get; set; }

    [Column("EnumOptions")]
    public string? EnumOptions { get; set; }

    [Column("IsRequired")]
    public bool IsRequired { get; set; }

    [Column("DisplayOrder")]
    public int DisplayOrder { get; set; }

    [Column("MeasurementRole")]
    public MeasurementRole MeasurementRole { get; set; } = MeasurementRole.None;

    [NotMapped]
    public virtual Subgroup? ProductCategory { get; set; }
}
