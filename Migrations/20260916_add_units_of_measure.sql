-- Fase UM, Passo 1 — Unit Of Measure

CREATE TABLE IF NOT EXISTS unit_of_measures (
    UnitOfMeasureID INT NOT NULL AUTO_INCREMENT,
    Code VARCHAR(10) NOT NULL,
    Description VARCHAR(100) NOT NULL,
    AllowsFraction TINYINT(1) NOT NULL DEFAULT 0,
    IsActive TINYINT(1) NOT NULL DEFAULT 1,
    PRIMARY KEY (UnitOfMeasureID),
    UNIQUE KEY UQ_unit_of_measures_Code (Code)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;

INSERT INTO unit_of_measures (Code, Description, AllowsFraction, IsActive) VALUES
    ('UN', 'Unidade', 0, 1),
    ('M', 'Metro', 1, 1),
    ('M2', 'Metro Quadrado', 1, 1),
    ('KG', 'Quilograma', 1, 1),
    ('LT', 'Lata', 0, 1),
    ('BD', 'Balde', 0, 1),
    ('BRR', 'Barra', 0, 1),
    ('CX', 'Caixa', 0, 1),
    ('RL', 'Rolo', 0, 1),
    ('GL', 'Galão', 0, 1),
    ('PC', 'Pacote', 0, 1),
    ('SC', 'Saco', 0, 1),
    ('KIT', 'Kit', 0, 1)
ON DUPLICATE KEY UPDATE
    Description = VALUES(Description),
    AllowsFraction = VALUES(AllowsFraction),
    IsActive = VALUES(IsActive);

INSERT INTO permissions (PermissionCode, Description) VALUES
    ('UnitsOfMeasure.View', 'Consultar unidades de medida'),
    ('UnitsOfMeasure.Create', 'Cadastrar unidades de medida'),
    ('UnitsOfMeasure.Edit', 'Editar unidades de medida'),
    ('UnitsOfMeasure.Deactivate', 'Inativar unidades de medida')
ON DUPLICATE KEY UPDATE Description = VALUES(Description);

INSERT IGNORE INTO role_permissions (RoleID, PermissionID)
SELECT r.RoleID, p.PermissionID
FROM roles r
CROSS JOIN permissions p
WHERE r.RoleName = 'Administrador'
  AND p.PermissionCode IN (
    'UnitsOfMeasure.View',
    'UnitsOfMeasure.Create',
    'UnitsOfMeasure.Edit',
    'UnitsOfMeasure.Deactivate'
  );
