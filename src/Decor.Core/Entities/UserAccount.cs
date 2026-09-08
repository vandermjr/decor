using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Decor.Core.Entities;

// Entidade completa de persistência para a tabela "users", incluindo PasswordHash.
// Distinta de ApplicationUser (DTO de leitura usado fora da camada de repositório)
// para evitar propagar o hash de senha por chamadores que não precisam dele.
[Table("users")]
public class UserAccount
{
    [Key]
    [Column("UserID")]
    public int UserID { get; set; }

    [Column("Username")]
    public string? Username { get; set; }

    [Column("DisplayName")]
    public string? DisplayName { get; set; }

    [Column("PasswordHash")]
    public string? PasswordHash { get; set; }

    [Column("IsActive")]
    public bool IsActive { get; set; }

    [Column("MustChangePassword")]
    public bool MustChangePassword { get; set; }
}
