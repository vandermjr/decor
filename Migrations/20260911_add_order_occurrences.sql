-- Fase P4, Passo 7 - Motivos e ocorrências de pedidos

CREATE TABLE IF NOT EXISTS occurrence_reasons (
    ReasonID INT NOT NULL AUTO_INCREMENT,
    Description VARCHAR(150) NOT NULL,
    IsActive TINYINT(1) NOT NULL DEFAULT 1,
    PRIMARY KEY (ReasonID),
    UNIQUE KEY UQ_occurrence_reasons_Description (Description)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;

CREATE TABLE IF NOT EXISTS order_occurrences (
    OccurrenceID INT NOT NULL AUTO_INCREMENT,
    OrderID INT NOT NULL,
    ReasonID INT NOT NULL,
    RegisteredByEmployeeID INT NOT NULL,
    RegisteredAt DATETIME NOT NULL,
    Observation TEXT NOT NULL,
    NewManufacturingDeadline DATE NULL,
    NewInstallationDeadline DATE NULL,
    PRIMARY KEY (OccurrenceID),
    KEY IX_order_occurrences_OrderID (OrderID),
    KEY IX_order_occurrences_ReasonID (ReasonID),
    KEY IX_order_occurrences_RegisteredByEmployeeID (RegisteredByEmployeeID),
    CONSTRAINT FK_order_occurrences_orders FOREIGN KEY (OrderID) REFERENCES orders (OrderID),
    CONSTRAINT FK_order_occurrences_reasons FOREIGN KEY (ReasonID) REFERENCES occurrence_reasons (ReasonID),
    CONSTRAINT FK_order_occurrences_employees FOREIGN KEY (RegisteredByEmployeeID) REFERENCES employees (EmployeeID)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;

INSERT INTO occurrence_reasons (Description, IsActive) VALUES
    ('Atraso de pagamento', 1),
    ('Atraso de entrega', 1),
    ('Troca', 1),
    ('Renegociação', 1)
ON DUPLICATE KEY UPDATE IsActive = VALUES(IsActive);

INSERT INTO permissions (PermissionCode, Description) VALUES
    ('OccurrenceReasons.View', 'Consultar motivos de ocorrência'),
    ('OccurrenceReasons.Create', 'Cadastrar motivos de ocorrência'),
    ('OccurrenceReasons.Edit', 'Editar motivos de ocorrência'),
    ('OccurrenceReasons.Delete', 'Excluir motivos de ocorrência'),
    ('OrderOccurrences.View', 'Consultar histórico de ocorrências de pedidos'),
    ('OrderOccurrences.Register', 'Registrar ocorrências de pedidos')
ON DUPLICATE KEY UPDATE Description = VALUES(Description);

INSERT IGNORE INTO role_permissions (RoleID, PermissionID)
SELECT r.RoleID, p.PermissionID
FROM roles r
CROSS JOIN permissions p
WHERE r.RoleName = 'Administrador'
  AND p.PermissionCode IN (
    'OccurrenceReasons.View',
    'OccurrenceReasons.Create',
    'OccurrenceReasons.Edit',
    'OccurrenceReasons.Delete',
    'OrderOccurrences.View',
    'OrderOccurrences.Register'
  );