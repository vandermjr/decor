using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Decor.Core.Entities;

[Table("partner_price_tables")]
public class PartnerPriceTable
{
    [Key]
    [Column("PriceTableID")]
    public int PriceTableID { get; set; }

    [Column("PartnerID")]
    public int PartnerID { get; set; }

    [Column("GroupID")]
    public int GroupID { get; set; }

    [Column("PricePerSquareMeter")]
    public decimal PricePerSquareMeter { get; set; }

    [Column("IsActive")]
    public bool IsActive { get; set; } = true;

    [NotMapped]
    public virtual Partner? Partner { get; set; }

    [NotMapped]
    public virtual Group? Group { get; set; }
}
