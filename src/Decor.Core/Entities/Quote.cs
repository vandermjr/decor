using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Decor.Core.Entities;

public enum QuoteSourceType
{
    Own = 1,
    Store = 2,
    Other = 3,
    DirectCapture = Own,
    ArchitectPartner = Store
}

[Table("quotes")]
public class Quote
{
    [Key]
    [Column("QuoteID")]
    public int QuoteID { get; set; }

    [Column("CustomerID")]
    public int? CustomerID { get; set; }

    [Column("CreatedByEmployeeID")]
    public int? CreatedByEmployeeID { get; set; }

    [Column("CreatedByUserID")]
    public int? CreatedByUserID { get; set; }

    [Column("SourcePartnerID")]
    public int? SourcePartnerID { get; set; }

    [Column("SourceType")]
    public QuoteSourceType SourceType { get; set; }

    [Column("CreatedAt")]
    public DateTime CreatedAt { get; set; }

    [Column("Notes")]
    public string? Notes { get; set; }

    [Column("DiscountAmount")]
    public decimal DiscountAmount { get; set; }

    [NotMapped]
    public ICollection<QuoteSection> Sections { get; set; } = new List<QuoteSection>();

    [NotMapped]
    public string? CustomerName { get; set; }

    [NotMapped]
    public string? CreatedByEmployeeName { get; set; }

    [NotMapped]
    public string? ListStatus { get; set; }

    [NotMapped]
    public decimal? ListTotal { get; set; }
}
