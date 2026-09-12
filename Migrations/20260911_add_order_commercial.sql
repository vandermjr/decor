-- Fase P4, Passo 4 — Order (pedido comercial)
-- Entrega do agregado: Order, OrderItem, OrderItemSpecificationValue

CREATE TABLE IF NOT EXISTS orders (
    OrderID INT NOT NULL AUTO_INCREMENT,
    QuoteSectionID INT NOT NULL,
    CustomerID INT NOT NULL,
    OrderType TINYINT UNSIGNED NOT NULL,
    Status TINYINT UNSIGNED NOT NULL,
    RequiresDownPayment TINYINT(1) NULL,
    ManufacturingDeadline DATE NULL,
    InstallationDeadline DATE NULL,
    CreatedAt DATETIME NOT NULL,
    PRIMARY KEY (OrderID),
    UNIQUE KEY UQ_orders_QuoteSectionID (QuoteSectionID),
    KEY IX_orders_CustomerID (CustomerID),
    CONSTRAINT FK_orders_quote_sections FOREIGN KEY (QuoteSectionID) REFERENCES quote_sections (QuoteSectionID),
    CONSTRAINT FK_orders_customers FOREIGN KEY (CustomerID) REFERENCES customers (CustomerID)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;

CREATE TABLE IF NOT EXISTS order_items (
    OrderItemID INT NOT NULL AUTO_INCREMENT,
    OrderID INT NOT NULL,
    QuoteItemID INT NOT NULL,
    ProductID INT NOT NULL,
    Quantity DECIMAL(12,3) NOT NULL DEFAULT 0,
    UnitPrice DECIMAL(12,2) NOT NULL DEFAULT 0,
    HasInstallationService TINYINT(1) NOT NULL DEFAULT 0,
    SentToProductionAt DATETIME NULL,
    SentToProductionByEmployeeID INT NULL,
    PRIMARY KEY (OrderItemID),
    KEY IX_order_items_OrderID (OrderID),
    KEY IX_order_items_QuoteItemID (QuoteItemID),
    KEY IX_order_items_ProductID (ProductID),
    KEY IX_order_items_SentToProductionByEmployeeID (SentToProductionByEmployeeID),
    CONSTRAINT FK_order_items_orders FOREIGN KEY (OrderID) REFERENCES orders (OrderID),
    CONSTRAINT FK_order_items_quote_items FOREIGN KEY (QuoteItemID) REFERENCES quote_items (QuoteItemID),
    CONSTRAINT FK_order_items_products FOREIGN KEY (ProductID) REFERENCES products (ProductID),
    CONSTRAINT FK_order_items_employees FOREIGN KEY (SentToProductionByEmployeeID) REFERENCES employees (EmployeeID)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;

CREATE TABLE IF NOT EXISTS order_item_specification_values (
    ValueID INT NOT NULL AUTO_INCREMENT,
    OrderItemID INT NOT NULL,
    AttributeID INT NOT NULL,
    Value VARCHAR(255) NOT NULL,
    PRIMARY KEY (ValueID),
    KEY IX_order_item_spec_values_OrderItemID (OrderItemID),
    KEY IX_order_item_spec_values_AttributeID (AttributeID),
    CONSTRAINT FK_order_item_spec_values_order_items FOREIGN KEY (OrderItemID) REFERENCES order_items (OrderItemID),
    CONSTRAINT FK_order_item_spec_values_attributes FOREIGN KEY (AttributeID) REFERENCES product_specification_attributes (AttributeID)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;

INSERT INTO permissions (PermissionCode, Description) VALUES
    ('Orders.View', 'Consultar pedidos'),
    ('Orders.ConvertFromQuote', 'Converter seção de orçamento em pedido'),
    ('Orders.Approve', 'Aprovar pedido'),
    ('Orders.Cancel', 'Cancelar pedido'),
    ('Orders.SendToProduction', 'Enviar item do pedido para produção')
ON DUPLICATE KEY UPDATE Description = VALUES(Description);

INSERT IGNORE INTO role_permissions (RoleID, PermissionID)
SELECT r.RoleID, p.PermissionID
FROM roles r
CROSS JOIN permissions p
WHERE r.RoleName = 'Administrador'
  AND p.PermissionCode IN (
      'Orders.View',
      'Orders.ConvertFromQuote',
      'Orders.Approve',
      'Orders.Cancel',
      'Orders.SendToProduction'
  );
