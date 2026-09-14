-- Fase P6, parcelas de pedidos de compra

CREATE TABLE IF NOT EXISTS purchase_order_installments (
    InstallmentID INT NOT NULL AUTO_INCREMENT,
    PurchaseOrderID INT NOT NULL,
    PaymentMethodID INT NOT NULL,
    InstallmentNumber INT NOT NULL,
    Amount DECIMAL(12,2) NOT NULL DEFAULT 0.00,
    DueDate DATE NOT NULL,
    Status TINYINT UNSIGNED NOT NULL,
    PaidAt DATETIME NULL,
    PaidByEmployeeID INT NULL,
    PRIMARY KEY (InstallmentID),
    KEY IX_purchase_order_installments_PurchaseOrderID (PurchaseOrderID),
    KEY IX_purchase_order_installments_PaymentMethodID (PaymentMethodID),
    KEY IX_purchase_order_installments_PaidByEmployeeID (PaidByEmployeeID),
    CONSTRAINT FK_purchase_order_installments_purchase_orders FOREIGN KEY (PurchaseOrderID) REFERENCES purchase_orders (PurchaseOrderID),
    CONSTRAINT FK_purchase_order_installments_payment_methods FOREIGN KEY (PaymentMethodID) REFERENCES payment_methods (PaymentMethodID),
    CONSTRAINT FK_purchase_order_installments_employees FOREIGN KEY (PaidByEmployeeID) REFERENCES employees (EmployeeID)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;

INSERT INTO permissions (PermissionCode, Description) VALUES
    ('PurchaseOrderInstallments.View', 'Consultar parcelas de pedidos de compra'),
    ('PurchaseOrderInstallments.CreatePlan', 'Criar plano de pagamento de pedido de compra'),
    ('PurchaseOrderInstallments.RegisterPayment', 'Registrar pagamento de parcela de pedido de compra'),
    ('PurchaseOrderInstallments.MarkOverdue', 'Marcar parcela de pedido de compra como vencida'),
    ('PurchaseOrderInstallments.Cancel', 'Cancelar parcela de pedido de compra')
ON DUPLICATE KEY UPDATE Description = VALUES(Description);

INSERT IGNORE INTO role_permissions (RoleID, PermissionID)
SELECT r.RoleID, p.PermissionID
FROM roles r
CROSS JOIN permissions p
WHERE r.RoleName = 'Administrador'
  AND p.PermissionCode IN (
    'PurchaseOrderInstallments.View',
    'PurchaseOrderInstallments.CreatePlan',
    'PurchaseOrderInstallments.RegisterPayment',
    'PurchaseOrderInstallments.MarkOverdue',
    'PurchaseOrderInstallments.Cancel'
  );