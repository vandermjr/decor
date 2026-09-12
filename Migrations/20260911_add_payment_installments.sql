-- Fase P4, Passo 6 — Payment Methods & Order Installments

CREATE TABLE IF NOT EXISTS payment_methods (
    PaymentMethodID INT NOT NULL AUTO_INCREMENT,
    Name VARCHAR(100) NOT NULL,
    Timing TINYINT UNSIGNED NOT NULL,
    IsActive TINYINT(1) NOT NULL DEFAULT 1,
    PRIMARY KEY (PaymentMethodID),
    UNIQUE KEY UQ_payment_methods_Name (Name)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;

CREATE TABLE IF NOT EXISTS order_installments (
    InstallmentID INT NOT NULL AUTO_INCREMENT,
    OrderID INT NOT NULL,
    PaymentMethodID INT NOT NULL,
    InstallmentNumber INT NOT NULL,
    Amount DECIMAL(12,2) NOT NULL DEFAULT 0.00,
    DueDate DATE NOT NULL,
    Status TINYINT UNSIGNED NOT NULL,
    PaidAt DATETIME NULL,
    ReceivedByEmployeeID INT NULL,
    PRIMARY KEY (InstallmentID),
    KEY IX_order_installments_OrderID (OrderID),
    KEY IX_order_installments_PaymentMethodID (PaymentMethodID),
    KEY IX_order_installments_ReceivedByEmployeeID (ReceivedByEmployeeID),
    CONSTRAINT FK_order_installments_orders FOREIGN KEY (OrderID) REFERENCES orders (OrderID),
    CONSTRAINT FK_order_installments_payment_methods FOREIGN KEY (PaymentMethodID) REFERENCES payment_methods (PaymentMethodID),
    CONSTRAINT FK_order_installments_employees FOREIGN KEY (ReceivedByEmployeeID) REFERENCES employees (EmployeeID)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;

-- Registros iniciais de PaymentMethod
-- Timing: 1 = Cash (À vista), 2 = Term (A prazo)
INSERT INTO payment_methods (Name, Timing, IsActive) VALUES
    ('PIX', 1, 1),
    ('Débito', 1, 1),
    ('Transferência', 1, 1),
    ('Crédito', 2, 1),
    ('Boleto', 2, 1)
ON DUPLICATE KEY UPDATE Timing = VALUES(Timing), IsActive = VALUES(IsActive);

-- Inserir as 9 novas permissões
INSERT INTO permissions (PermissionCode, Description) VALUES
    ('PaymentMethods.View', 'Consultar formas de pagamento'),
    ('PaymentMethods.Create', 'Cadastrar formas de pagamento'),
    ('PaymentMethods.Edit', 'Editar formas de pagamento'),
    ('PaymentMethods.Delete', 'Excluir formas de pagamento'),
    ('OrderInstallments.View', 'Consultar parcelas de pedidos'),
    ('OrderInstallments.CreatePlan', 'Criar plano de pagamento para pedidos'),
    ('OrderInstallments.RegisterPayment', 'Registrar pagamento de parcela'),
    ('OrderInstallments.MarkOverdue', 'Marcar parcela como vencida'),
    ('OrderInstallments.Cancel', 'Cancelar parcela de pedido')
ON DUPLICATE KEY UPDATE Description = VALUES(Description);

-- Vincular as novas permissões à role Administrador
INSERT IGNORE INTO role_permissions (RoleID, PermissionID)
SELECT r.RoleID, p.PermissionID
FROM roles r
CROSS JOIN permissions p
WHERE r.RoleName = 'Administrador'
  AND p.PermissionCode IN (
    'PaymentMethods.View',
    'PaymentMethods.Create',
    'PaymentMethods.Edit',
    'PaymentMethods.Delete',
    'OrderInstallments.View',
    'OrderInstallments.CreatePlan',
    'OrderInstallments.RegisterPayment',
    'OrderInstallments.MarkOverdue',
    'OrderInstallments.Cancel'
  );
