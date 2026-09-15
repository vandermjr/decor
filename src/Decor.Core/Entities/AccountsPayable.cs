using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Decor.Core.Entities;

public enum AccountsPayablePayeeType
{
    Partner = 1,
    Employee = 2
}

public enum AccountsPayableStatus
{
    Pending = 1,
    Paid = 2,
    Cancelled = 3
}

public enum AccountsPayableSourceType
{
    TailorQuotationRevision = 1,
    ServiceExecutionRecord = 2
}

[Table("accounts_payable")]
public class AccountsPayable
{
    [Key]
    [Column("AccountsPayableID")] public int AccountsPayableID { get; set; }
    [Column("PayeeType")] public AccountsPayablePayeeType PayeeType { get; set; }
    [Column("PayeeID")] public int PayeeID { get; set; }
    [Column("Description")] public string Description { get; set; } = string.Empty;
    [Column("Amount")] public decimal Amount { get; set; }
    [Column("DueDate")] public DateTime DueDate { get; set; }
    [Column("Status")] public AccountsPayableStatus Status { get; set; }
    [Column("SourceType")] public AccountsPayableSourceType? SourceType { get; set; }
    [Column("SourceID")] public int? SourceID { get; set; }
    [Column("CreatedByEmployeeID")] public int CreatedByEmployeeID { get; set; }
    [Column("CreatedAt")] public DateTime CreatedAt { get; set; }
    [Column("PaidAt")] public DateTime? PaidAt { get; set; }
    [Column("PaidByEmployeeID")] public int? PaidByEmployeeID { get; set; }
    [Column("PaidFromCashAccountID")] public int? PaidFromCashAccountID { get; set; }
}