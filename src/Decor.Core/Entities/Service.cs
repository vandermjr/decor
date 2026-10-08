using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Decor.Core.Entities;

[Table("services")]
public class Service
{
    [Key]
    [Column("ServiceID")]
    public int ServiceID { get; set; }

    [Column("Description")]
    public string Description { get; set; } = string.Empty;

    [Column("IsActive")]
    public bool IsActive { get; set; } = true;

    [Column("CostPrice")]
    public decimal? CostPrice { get; set; }

    [Column("SalePrice")]
    public decimal? SalePrice { get; set; }

    [Column("EmployeeCommissionValue")]
    public decimal? EmployeeCommissionValue { get; set; }

    [Column("Observations")]
    public string? Observations { get; set; }
}