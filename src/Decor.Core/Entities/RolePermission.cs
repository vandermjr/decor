using System.ComponentModel.DataAnnotations.Schema;

namespace Decor.Core.Entities;

// Chave composta (RoleID, PermissionID) na tabela, mas nenhuma coluna é autogerada:
// ambas devem ser fornecidas em cada INSERT, por isso não são marcadas [Key] aqui
// ([Key] neste projeto indica coluna autogerada, a ser excluída de INSERT/UPDATE).
[Table("role_permissions")]
public class RolePermission
{
    [Column("RoleID")]
    public int RoleID { get; set; }

    [Column("PermissionID")]
    public int PermissionID { get; set; }
}
