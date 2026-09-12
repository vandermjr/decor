using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Decor.Core.Entities;

public enum StockMovementType
{
    Entrada = 1,
    Saida = 2,
    Ajuste = 3,
    Transferencia = 4
}

public enum StockAdjustmentReason
{
    BalancoGeral = 1,
    Quebra = 2,
    Furto = 3,
    Perda = 4,
    Outro = 5
}

public enum StockMovementReviewStatus
{
    PendenteDeCiencia = 1,
    Ciente = 2,
    Contestado = 3
}

[Table("stock_movements")]
public class StockMovement
{
    [Key]
    [Column("StockMovementID")]
    public int StockMovementID { get; set; }

    [Column("ProductID")]
    public int ProductID { get; set; }

    [Column("StockLocationID")]
    public int StockLocationID { get; set; }

    [Column("Quantity")]
    public decimal Quantity { get; set; }

    [Column("MovementType")]
    public StockMovementType MovementType { get; set; }

    [Column("TransferID")]
    public Guid? TransferID { get; set; }

    [Column("Reason")]
    public StockAdjustmentReason? Reason { get; set; }

    [Column("Justification")]
    public string? Justification { get; set; }

    [Column("AuthorizedByEmployeeID")]
    public int? AuthorizedByEmployeeID { get; set; }

    [Column("PerformedByEmployeeID")]
    public int PerformedByEmployeeID { get; set; }

    [Column("ReviewStatus")]
    public StockMovementReviewStatus? ReviewStatus { get; set; }

    [Column("ReviewedByEmployeeID")]
    public int? ReviewedByEmployeeID { get; set; }

    [Column("ReviewedAt")]
    public DateTime? ReviewedAt { get; set; }

    [Column("MovementDate")]
    public DateTime MovementDate { get; set; }

    [Column("Notes")]
    public string? Notes { get; set; }

    [Column("OrderItemID")]
    public int? OrderItemID { get; set; }
}