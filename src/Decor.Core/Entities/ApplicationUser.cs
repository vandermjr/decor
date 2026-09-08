using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Decor.Core.Entities;

[Table("users")]
public sealed class ApplicationUser
{
    [Key]
    [Column("UserID")] // Opcional
    public int UserID { get; init; }

    [Column("Username")] // Opcional
    public string Username { get; init; } = string.Empty;

    [Column("DisplayName")] // Opcional
    public string DisplayName { get; init; } = string.Empty;

    [Column("IsActive")] // Opcional
    public bool IsActive { get; init; }

    [Column("MustChangePassword")] // Opcional
    public bool MustChangePassword { get; init; }

    [NotMapped]
    public IReadOnlyCollection<string> Roles { get; init; } = [];

    [NotMapped]
    public IReadOnlyCollection<string> Permissions { get; init; } = [];
}
