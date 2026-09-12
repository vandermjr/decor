using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Decor.Core.Entities;

public enum QuoteSectionType
{
    Catalog = 1,
    Custom = 2
}

public enum QuoteSectionStatus
{
    Draft = 1,
    AwaitingQuotation = 2,
    Sent = 3,
    Approved = 4,
    Rejected = 5,
    ConvertedToOrder = 6
}

[Table("quote_sections")]
public class QuoteSection
{
    [Key]
    [Column("QuoteSectionID")]
    public int QuoteSectionID { get; set; }

    [Column("QuoteID")]
    public int QuoteID { get; set; }

    [Column("SectionType")]
    public QuoteSectionType SectionType { get; set; }

    [Column("Status")]
    public QuoteSectionStatus Status { get; set; }

    [Column("SentToCustomerAt")]
    public DateTime? SentToCustomerAt { get; set; }

    [Column("ApprovedAt")]
    public DateTime? ApprovedAt { get; set; }

    [Column("CreatedAt")]
    public DateTime CreatedAt { get; set; }

    [NotMapped]
    public ICollection<QuoteItem> Items { get; set; } = new List<QuoteItem>();
}
