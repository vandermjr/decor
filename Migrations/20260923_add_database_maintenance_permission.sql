INSERT INTO permissions (PermissionCode, Description) VALUES
 ('DatabaseMaintenance.View', 'Consultar manutenção do banco de dados')
ON DUPLICATE KEY UPDATE Description = VALUES(Description);

INSERT IGNORE INTO role_permissions (RoleID, PermissionID)
SELECT r.RoleID, p.PermissionID
FROM roles r
CROSS JOIN permissions p
WHERE r.RoleName IN ('Administrador', 'Supervisor')
  AND p.PermissionCode = 'DatabaseMaintenance.View';