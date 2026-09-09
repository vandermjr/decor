using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Decor.Core.Entities;

public enum StockLocationType
{
    Empresa = 1,
    Parceiro = 2   // depósito alocado a um Partner do tipo Fabricante
}

[Table("stock_locations")]
public class StockLocation
{
    [Key]
    [Column("StockLocationID")]
    public int StockLocationID { get; set; }

    [Column("Name")]
    public string Name { get; set; } = string.Empty;

    [Column("LocationType")]
    public StockLocationType LocationType { get; set; }

    // Preenchido apenas quando LocationType == Parceiro
    [Column("PartnerID")]
    public int? PartnerID { get; set; }

    [Column("IsActive")]
    public bool IsActive { get; set; }

    [NotMapped]
    public virtual Partner? Partner { get; set; }
}