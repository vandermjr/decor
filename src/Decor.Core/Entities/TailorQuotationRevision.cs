using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Decor.Core.Entities;

[Table("tailor_quotation_revisions")]
public class TailorQuotationRevision
{
    [Key]
    [Column("RevisionID")]
    public int RevisionID { get; set; }

    [Column("RequestID")]
    public int RequestID { get; set; }

    [Column("RevisionNumber")]
    public int RevisionNumber { get; set; }

    [Column("Price")]
    public decimal Price { get; set; }

    [Column("ChangeReason")]
    public string? ChangeReason { get; set; }

    [Column("RespondedAt")]
    public DateTime RespondedAt { get; set; }

    [Column("RegisteredByEmployeeID")]
    public int RegisteredByEmployeeID { get; set; }
}
