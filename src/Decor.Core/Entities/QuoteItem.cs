using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Decor.Core.Entities;

[Table("quote_items")]
public class QuoteItem
{
    [Key]
    [Column("QuoteItemID")]
    public int QuoteItemID { get; set; }

    [Column("QuoteSectionID")]
    public int QuoteSectionID { get; set; }

    [Column("ProductID")]
    public int ProductID { get; set; }

    [Column("Quantity")]
    public decimal Quantity { get; set; }

    [Column("UnitPrice")]
    public decimal? UnitPrice { get; set; }

    [Column("HasInstallationService")]
    public bool HasInstallationService { get; set; }

    [NotMapped]
    public ICollection<QuoteItemSpecificationValue> SpecificationValues { get; set; } = new List<QuoteItemSpecificationValue>();
}
