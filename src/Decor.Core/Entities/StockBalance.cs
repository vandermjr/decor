using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Decor.Core.Entities;

[Table("stock_balances")]
public class StockBalance
{
    [Key]
    [Column("StockBalanceID")]
    public int StockBalanceID { get; set; }

    [Column("ProductID")]
    public int ProductID { get; set; }

    [Column("StockLocationID")]
    public int StockLocationID { get; set; }

    [Column("Quantity")]
    public decimal Quantity { get; set; }

    [Column("UpdatedAt")]
    public DateTime UpdatedAt { get; set; }
}