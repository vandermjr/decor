INSERT IGNORE INTO permissions (PermissionCode, Description)
VALUES ('DatabaseMaintenance.Restore', 'Restaurar banco de dados a partir de backup Decor');

INSERT IGNORE INTO role_permissions (RoleID, PermissionID)
SELECT r.RoleID, p.PermissionID
FROM roles r CROSS JOIN permissions p
WHERE r.IsSystemProtected = 1
  AND r.RoleName IN ('Administrador', 'Administradores')
  AND p.PermissionCode = 'DatabaseMaintenance.Restore'
  AND ROW_COUNT() = 1;