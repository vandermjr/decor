using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Decor.Core.Entities;

public enum PartnerType
{
    Fabricante = 1,  // ex: costureira — possui depósito próprio
    Instalador = 2   // ex: instalador de rodapé/piso — sem depósito próprio
}

[Table("partners")]
public class Partner
{
    [Key]
    [Column("PartnerID")]
    public int PartnerID { get; set; }

    [Column("Name")]
    public string Name { get; set; } = string.Empty;

    [Column("Document")]
    public string? Document { get; set; } // CNPJ (MEI)

    [Column("Phone")]
    public string? Phone { get; set; }

    [Column("PartnerType")]
    public PartnerType PartnerType { get; set; }

    [Column("IsActive")]
    public bool IsActive { get; set; }
}