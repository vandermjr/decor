-- Authentication and authorization schema for Decor.
-- The initial admin password is stored only as a PBKDF2 hash.

CREATE TABLE IF NOT EXISTS users (
    UserID INT NOT NULL AUTO_INCREMENT,
    Username VARCHAR(50) NOT NULL,
    DisplayName VARCHAR(100) NOT NULL,
    PasswordHash VARCHAR(255) NOT NULL,
    IsActive TINYINT(1) NOT NULL DEFAULT 1,
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt DATETIME NULL DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (UserID),
    UNIQUE KEY UX_users_Username (Username)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;

CREATE TABLE IF NOT EXISTS roles (
    RoleID INT NOT NULL AUTO_INCREMENT,
    RoleName VARCHAR(50) NOT NULL,
    Description VARCHAR(255) NULL,
    PRIMARY KEY (RoleID),
    UNIQUE KEY UX_roles_RoleName (RoleName)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;

CREATE TABLE IF NOT EXISTS permissions (
    PermissionID INT NOT NULL AUTO_INCREMENT,
    PermissionCode VARCHAR(100) NOT NULL,
    Description VARCHAR(255) NULL,
    PRIMARY KEY (PermissionID),
    UNIQUE KEY UX_permissions_PermissionCode (PermissionCode)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;

CREATE TABLE IF NOT EXISTS user_roles (
    UserID INT NOT NULL,
    RoleID INT NOT NULL,
    PRIMARY KEY (UserID, RoleID),
    KEY IX_user_roles_RoleID (RoleID),
    CONSTRAINT FK_user_roles_users FOREIGN KEY (UserID) REFERENCES users (UserID) ON DELETE CASCADE,
    CONSTRAINT FK_user_roles_roles FOREIGN KEY (RoleID) REFERENCES roles (RoleID) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;

CREATE TABLE IF NOT EXISTS role_permissions (
    RoleID INT NOT NULL,
    PermissionID INT NOT NULL,
    PRIMARY KEY (RoleID, PermissionID),
    KEY IX_role_permissions_PermissionID (PermissionID),
    CONSTRAINT FK_role_permissions_roles FOREIGN KEY (RoleID) REFERENCES roles (RoleID) ON DELETE CASCADE,
    CONSTRAINT FK_role_permissions_permissions FOREIGN KEY (PermissionID) REFERENCES permissions (PermissionID) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;

INSERT INTO roles (RoleName, Description)
VALUES ('Administrador', 'Acesso completo às funcionalidades atuais do Decor')
ON DUPLICATE KEY UPDATE Description = VALUES(Description);

INSERT INTO permissions (PermissionCode, Description) VALUES
    ('Products.View', 'Consultar produtos'),
    ('Products.Create', 'Cadastrar produtos'),
    ('Products.Edit', 'Editar produtos'),
    ('Brands.View', 'Consultar marcas'),
    ('Brands.Create', 'Cadastrar marcas'),
    ('Brands.Edit', 'Editar marcas'),
    ('Brands.Delete', 'Excluir marcas'),
    ('Classifications.View', 'Consultar classificações'),
    ('TermDelivery.View', 'Consultar termo de entrega')
ON DUPLICATE KEY UPDATE Description = VALUES(Description);

INSERT INTO users (Username, DisplayName, PasswordHash, IsActive)
VALUES ('admin', 'Administrador', 'PBKDF2-SHA256$210000$jvVoQcXYELRUbyk8pMrW9w==$SIOahYSewK5diBBU9Dl8CIjTAJSnaDgn0aFHSUhZijs=', 1)
ON DUPLICATE KEY UPDATE DisplayName = VALUES(DisplayName), IsActive = VALUES(IsActive);

INSERT IGNORE INTO user_roles (UserID, RoleID)
SELECT u.UserID, r.RoleID
FROM users u
INNER JOIN roles r ON r.RoleName = 'Administrador'
WHERE u.Username = 'admin';

INSERT IGNORE INTO role_permissions (RoleID, PermissionID)
SELECT r.RoleID, p.PermissionID
FROM roles r
CROSS JOIN permissions p
WHERE r.RoleName = 'Administrador';
