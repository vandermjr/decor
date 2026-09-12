-- Fase P4, Passo 5 — Reserva de Estoque e Rastreabilidade de Transferência (MIP)
-- 1) Adiciona vínculo de item de pedido em movimentações de estoque
ALTER TABLE stock_movements
    ADD COLUMN OrderItemID INT NULL AFTER Notes,
    ADD KEY IX_stock_movements_OrderItemID (OrderItemID),
    ADD CONSTRAINT FK_stock_movements_order_items FOREIGN KEY (OrderItemID) REFERENCES order_items (OrderItemID);

-- 2) Cria tabela de reservas de estoque
-- Status: 1 = Active, 2 = Released, 3 = Consumed
CREATE TABLE IF NOT EXISTS stock_reservations (
    ReservationID INT NOT NULL AUTO_INCREMENT,
    OrderItemID INT NOT NULL,
    ProductID INT NOT NULL,
    StockLocationID INT NOT NULL,
    Quantity DECIMAL(12,3) NOT NULL DEFAULT 0,
    Status TINYINT UNSIGNED NOT NULL,
    CreatedByEmployeeID INT NOT NULL,
    CreatedAt DATETIME NOT NULL,
    ReleasedAt DATETIME NULL,
    PRIMARY KEY (ReservationID),
    KEY IX_stock_reservations_OrderItemID (OrderItemID),
    KEY IX_stock_reservations_ProductID (ProductID),
    KEY IX_stock_reservations_StockLocationID (StockLocationID),
    KEY IX_stock_reservations_CreatedByEmployeeID (CreatedByEmployeeID),
    CONSTRAINT FK_stock_reservations_order_items FOREIGN KEY (OrderItemID) REFERENCES order_items (OrderItemID),
    CONSTRAINT FK_stock_reservations_products FOREIGN KEY (ProductID) REFERENCES products (ProductID),
    CONSTRAINT FK_stock_reservations_stock_locations FOREIGN KEY (StockLocationID) REFERENCES stock_locations (StockLocationID),
    CONSTRAINT FK_stock_reservations_employees FOREIGN KEY (CreatedByEmployeeID) REFERENCES employees (EmployeeID)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;

-- 3) Permissões de reservas de estoque vinculadas à role Administrador
INSERT INTO permissions (PermissionCode, Description) VALUES
    ('StockReservations.View', 'Consultar reservas de estoque'),
    ('StockReservations.Create', 'Criar reserva de estoque'),
    ('StockReservations.Release', 'Liberar reserva de estoque')
ON DUPLICATE KEY UPDATE Description = VALUES(Description);

INSERT IGNORE INTO role_permissions (RoleID, PermissionID)
SELECT r.RoleID, p.PermissionID
FROM roles r
CROSS JOIN permissions p
WHERE r.RoleName = 'Administrador'
  AND p.PermissionCode IN (
      'StockReservations.View',
      'StockReservations.Create',
      'StockReservations.Release'
  );
