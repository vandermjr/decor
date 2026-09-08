using System.ComponentModel.DataAnnotations.Schema;

namespace Decor.Core.Entities;

// Chave composta (UserID, RoleID) na tabela, mas nenhuma coluna é autogerada:
// ambas devem ser fornecidas em cada INSERT, por isso não são marcadas [Key] aqui
// ([Key] neste projeto indica coluna autogerada, a ser excluída de INSERT/UPDATE).
[Table("user_roles")]
public class UserRole
{
    [Column("UserID")]
    public int UserID { get; set; }

    [Column("RoleID")]
    public int RoleID { get; set; }
}
