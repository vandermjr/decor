-- Incremental administration model: explicit role hierarchy and per-user permission overrides.
ALTER TABLE roles
    ADD COLUMN HierarchyLevel INT NOT NULL DEFAULT 0 AFTER Description,
    ADD COLUMN IsSystemProtected TINYINT(1) NOT NULL DEFAULT 0 AFTER HierarchyLevel;

CREATE TABLE IF NOT EXISTS user_permission_overrides (
    UserID INT NOT NULL,
    PermissionID INT NOT NULL,
    IsGranted TINYINT(1) NOT NULL,
    PRIMARY KEY (UserID, PermissionID),
    CONSTRAINT FK_user_permission_overrides_users FOREIGN KEY (UserID) REFERENCES users (UserID) ON DELETE CASCADE,
    CONSTRAINT FK_user_permission_overrides_permissions FOREIGN KEY (PermissionID) REFERENCES permissions (PermissionID) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;

INSERT INTO roles (RoleName, Description, HierarchyLevel, IsSystemProtected)
VALUES ('Supervisor', 'Administração operacional sem acesso à role Administrador', 100, 1)
ON DUPLICATE KEY UPDATE Description = VALUES(Description), HierarchyLevel = VALUES(HierarchyLevel), IsSystemProtected = VALUES(IsSystemProtected);

UPDATE roles SET HierarchyLevel = 200, IsSystemProtected = 1 WHERE RoleName = 'Administrador';

INSERT INTO permissions (PermissionCode, Description) VALUES
 ('Users.View','Consultar usuários'), ('Users.Create','Criar usuários'), ('Users.Edit','Editar usuários'),
 ('Users.Activate','Ativar usuários'), ('Users.Deactivate','Desativar usuários'),
 ('Users.AssignRoles','Atribuir roles'), ('Users.ManagePermissions','Gerenciar permissões individuais'),
 ('Users.ResetPassword','Redefinir senha'), ('Users.RestorePermissions','Restaurar permissões do usuário'),
 ('Roles.View','Consultar roles'), ('Roles.Edit','Editar roles'), ('Roles.ManagePermissions','Gerenciar permissões de roles'),
 ('Roles.RestoreDefaults','Restaurar padrão de roles')
ON DUPLICATE KEY UPDATE Description = VALUES(Description);

INSERT IGNORE INTO role_permissions (RoleID, PermissionID)
SELECT r.RoleID, p.PermissionID FROM roles r CROSS JOIN permissions p WHERE r.RoleName = 'Administrador';

INSERT IGNORE INTO role_permissions (RoleID, PermissionID)
SELECT r.RoleID, p.PermissionID FROM roles r INNER JOIN permissions p ON p.PermissionCode IN
 ('Users.View','Users.Create','Users.Edit','Users.Activate','Users.Deactivate','Users.AssignRoles','Users.ManagePermissions','Users.ResetPassword','Roles.View','Roles.Edit','Roles.ManagePermissions')
WHERE r.RoleName = 'Supervisor';
