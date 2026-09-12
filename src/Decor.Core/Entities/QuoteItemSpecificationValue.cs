using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Decor.Core.Entities;

[Table("quote_item_specification_values")]
public class QuoteItemSpecificationValue
{
    [Key]
    [Column("ValueID")]
    public int ValueID { get; set; }

    [Column("QuoteItemID")]
    public int QuoteItemID { get; set; }

    [Column("AttributeID")]
    public int AttributeID { get; set; }

    [Column("Value")]
    public string Value { get; set; } = string.Empty;
}
