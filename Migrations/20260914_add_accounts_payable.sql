-- Fase P6, contas a pagar avulsas

CREATE TABLE IF NOT EXISTS accounts_payable (
    AccountsPayableID INT NOT NULL AUTO_INCREMENT,
    PayeeType TINYINT UNSIGNED NOT NULL,
    PayeeID INT NOT NULL,
    Description TEXT NOT NULL,
    Amount DECIMAL(12,2) NOT NULL,
    DueDate DATE NOT NULL,
    Status TINYINT UNSIGNED NOT NULL,
    SourceType TINYINT UNSIGNED NULL,
    SourceID INT NULL,
    CreatedByEmployeeID INT NOT NULL,
    CreatedAt DATETIME NOT NULL,
    PaidAt DATETIME NULL,
    PaidByEmployeeID INT NULL,
    PRIMARY KEY (AccountsPayableID),
    KEY IX_accounts_payable_Payee (PayeeType, PayeeID),
    KEY IX_accounts_payable_Source (SourceType, SourceID),
    KEY IX_accounts_payable_CreatedByEmployeeID (CreatedByEmployeeID),
    KEY IX_accounts_payable_PaidByEmployeeID (PaidByEmployeeID),
    CONSTRAINT FK_accounts_payable_created_by FOREIGN KEY (CreatedByEmployeeID) REFERENCES employees (EmployeeID),
    CONSTRAINT FK_accounts_payable_paid_by FOREIGN KEY (PaidByEmployeeID) REFERENCES employees (EmployeeID),
    CONSTRAINT CK_accounts_payable_payee CHECK (PayeeType IN (1, 2)),
    CONSTRAINT CK_accounts_payable_amount CHECK (Amount > 0),
    CONSTRAINT CK_accounts_payable_source CHECK ((SourceType IS NULL AND SourceID IS NULL) OR (SourceType IS NOT NULL AND SourceID IS NOT NULL)),
    CONSTRAINT CK_accounts_payable_status CHECK (Status IN (1, 2, 3))
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;

INSERT INTO permissions (PermissionCode, Description) VALUES
    ('AccountsPayable.View', 'Consultar contas a pagar'),
    ('AccountsPayable.Create', 'Criar contas a pagar'),
    ('AccountsPayable.RegisterPayment', 'Registrar pagamento de conta a pagar'),
    ('AccountsPayable.Cancel', 'Cancelar conta a pagar')
ON DUPLICATE KEY UPDATE Description = VALUES(Description);

INSERT IGNORE INTO role_permissions (RoleID, PermissionID)
SELECT r.RoleID, p.PermissionID
FROM roles r
CROSS JOIN permissions p
WHERE r.RoleName = 'Administrador'
  AND p.PermissionCode IN ('AccountsPayable.View', 'AccountsPayable.Create', 'AccountsPayable.RegisterPayment', 'AccountsPayable.Cancel');