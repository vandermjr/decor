using System.ComponentModel.DataAnnotations.Schema;

namespace Decor.Core.Entities;

// Chave composta (UserID, PermissionID) na tabela, mas nenhuma coluna é autogerada:
// ambas devem ser fornecidas em cada INSERT, por isso não são marcadas [Key] aqui
// ([Key] neste projeto indica coluna autogerada, a ser excluída de INSERT/UPDATE).
[Table("user_permission_overrides")]
public class UserPermissionOverride
{
    [Column("UserID")]
    public int UserID { get; set; }

    [Column("PermissionID")]
    public int PermissionID { get; set; }

    [Column("IsGranted")]
    public bool IsGranted { get; set; }
}
