using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Decor.Core.Entities;

public enum QuoteSourceType
{
    DirectCapture = 1,
    ArchitectPartner = 2
}

[Table("quotes")]
public class Quote
{
    [Key]
    [Column("QuoteID")]
    public int QuoteID { get; set; }

    [Column("CustomerID")]
    public int CustomerID { get; set; }

    [Column("CreatedByEmployeeID")]
    public int CreatedByEmployeeID { get; set; }

    [Column("SourcePartnerID")]
    public int? SourcePartnerID { get; set; }

    [Column("SourceType")]
    public QuoteSourceType SourceType { get; set; }

    [Column("CreatedAt")]
    public DateTime CreatedAt { get; set; }

    [Column("Notes")]
    public string? Notes { get; set; }

    [NotMapped]
    public ICollection<QuoteSection> Sections { get; set; } = new List<QuoteSection>();
}
