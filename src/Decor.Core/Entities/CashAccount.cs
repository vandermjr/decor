using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Decor.Core.Entities;

public enum CashAccountType
{
    Cash = 1,
    Bank = 2,
    CardMachine = 3,
    Other = 4
}

[Table("cash_accounts")]
public class CashAccount
{
    [Key]
    [Column("CashAccountID")]
    public int CashAccountID { get; set; }

    [Column("Name")]
    public string Name { get; set; } = string.Empty;

    [Column("AccountType")]
    public CashAccountType AccountType { get; set; }

    [Column("IsActive")]
    public bool IsActive { get; set; } = true;
}