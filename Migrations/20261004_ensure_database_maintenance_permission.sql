INSERT IGNORE INTO permissions (PermissionCode, Description)
VALUES ('DatabaseMaintenance.View', 'Consultar manutenção do banco de dados');

INSERT IGNORE INTO role_permissions (RoleID, PermissionID)
SELECT r.RoleID, p.PermissionID
FROM roles r
CROSS JOIN permissions p
WHERE r.IsSystemProtected = 1
  AND r.RoleName IN ('Administrador', 'Supervisor', 'Administradores', 'Supervisores')
  AND p.PermissionCode = 'DatabaseMaintenance.View'
  AND ROW_COUNT() = 1;