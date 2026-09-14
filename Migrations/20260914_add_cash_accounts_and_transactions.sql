-- Fase P6 Passo 1: contas de caixa e ledger financeiro append-only.
CREATE TABLE IF NOT EXISTS cash_accounts (
    CashAccountID INT NOT NULL AUTO_INCREMENT,
    Name VARCHAR(120) NOT NULL,
    AccountType TINYINT UNSIGNED NOT NULL,
    IsActive BOOLEAN NOT NULL DEFAULT TRUE,
    PRIMARY KEY (CashAccountID),
    UNIQUE KEY UX_cash_accounts_Name (Name)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;

CREATE TABLE IF NOT EXISTS cash_transactions (
    CashTransactionID INT NOT NULL AUTO_INCREMENT,
    CashAccountID INT NOT NULL,
    Amount DECIMAL(15,2) NOT NULL,
    TransactionType TINYINT UNSIGNED NOT NULL,
    TransferID CHAR(36) NULL,
    SourceType VARCHAR(80) NULL,
    SourceID INT NULL,
    TransactionDate DATETIME NOT NULL,
    CreatedByEmployeeID INT NOT NULL,
    CreatedAt DATETIME NOT NULL,
    PRIMARY KEY (CashTransactionID),
    KEY IX_cash_transactions_CashAccountID (CashAccountID),
    KEY IX_cash_transactions_TransferID (TransferID),
    KEY IX_cash_transactions_CreatedByEmployeeID (CreatedByEmployeeID),
    CONSTRAINT FK_cash_transactions_account FOREIGN KEY (CashAccountID) REFERENCES cash_accounts (CashAccountID),
    CONSTRAINT FK_cash_transactions_employee FOREIGN KEY (CreatedByEmployeeID) REFERENCES employees (EmployeeID),
    CONSTRAINT CK_cash_transactions_amount CHECK (Amount > 0),
    CONSTRAINT CK_cash_transactions_type CHECK (TransactionType IN (1, 2, 3))
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;

INSERT INTO permissions (PermissionCode, Description) VALUES
    ('CashAccounts.View', 'Consultar contas de caixa'),
    ('CashAccounts.Create', 'Criar contas de caixa'),
    ('CashAccounts.Update', 'Alterar contas de caixa'),
    ('CashAccounts.Deactivate', 'Inativar contas de caixa'),
    ('CashTransactions.View', 'Consultar lançamentos financeiros'),
    ('CashTransactions.Create', 'Criar lançamentos financeiros')
ON DUPLICATE KEY UPDATE Description = VALUES(Description);

INSERT IGNORE INTO role_permissions (RoleID, PermissionID)
SELECT r.RoleID, p.PermissionID
FROM roles r
CROSS JOIN permissions p
WHERE r.RoleName = 'Administrador'
  AND p.PermissionCode IN ('CashAccounts.View', 'CashAccounts.Create', 'CashAccounts.Update', 'CashAccounts.Deactivate', 'CashTransactions.View', 'CashTransactions.Create');