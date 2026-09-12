using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Decor.Core.Entities;

public enum TailorQuotationRequestStatus
{
    Open = 1,
    Answered = 2,
    Closed = 3
}

[Table("tailor_quotation_requests")]
public class TailorQuotationRequest
{
    [Key]
    [Column("RequestID")]
    public int RequestID { get; set; }

    [Column("QuoteItemID")]
    public int QuoteItemID { get; set; }

    [Column("PartnerID")]
    public int PartnerID { get; set; }

    [Column("RequestedByEmployeeID")]
    public int RequestedByEmployeeID { get; set; }

    [Column("RequestedAt")]
    public DateTime RequestedAt { get; set; }

    [Column("Deadline")]
    public DateTime? Deadline { get; set; }

    [Column("Status")]
    public TailorQuotationRequestStatus Status { get; set; }

    [NotMapped]
    public ICollection<TailorQuotationRevision> Revisions { get; set; } = new List<TailorQuotationRevision>();
}
