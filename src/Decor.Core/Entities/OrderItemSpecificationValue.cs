using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Decor.Core.Entities;

[Table("order_item_specification_values")]
public class OrderItemSpecificationValue
{
    [Key]
    [Column("ValueID")]
    public int ValueID { get; set; }

    [Column("OrderItemID")]
    public int OrderItemID { get; set; }

    [Column("AttributeID")]
    public int AttributeID { get; set; }

    [Column("Value")]
    public string Value { get; set; } = string.Empty;
}
