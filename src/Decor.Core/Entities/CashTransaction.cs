using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Decor.Core.Entities;

public enum CashTransactionType
{
    Income = 1,
    Expense = 2,
    Transfer = 3
}

[Table("cash_transactions")]
public class CashTransaction
{
    [Key]
    [Column("CashTransactionID")]
    public int CashTransactionID { get; set; }

    [Column("CashAccountID")]
    public int CashAccountID { get; set; }

    [Column("Amount")]
    public decimal Amount { get; set; }

    [Column("TransactionType")]
    public CashTransactionType TransactionType { get; set; }

    [Column("TransferID")]
    public Guid? TransferID { get; set; }

    [Column("SourceType")]
    public string? SourceType { get; set; }

    [Column("SourceID")]
    public int? SourceID { get; set; }

    [Column("TransactionDate")]
    public DateTime TransactionDate { get; set; }

    [Column("CreatedByEmployeeID")]
    public int CreatedByEmployeeID { get; set; }

    [Column("CreatedAt")]
    public DateTime CreatedAt { get; set; }
}