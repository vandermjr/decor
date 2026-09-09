-- Fase P2 — núcleo de estoque (StockMovement, StockBalance)
-- Referência: Md/DECOR_FASE_P2_NUCLEO_ESTOQUE.md

-- MovementType: 1 = Entrada, 2 = Saida, 3 = Ajuste, 4 = Transferencia
-- Reason: 1 = BalancoGeral, 2 = Quebra, 3 = Furto, 4 = Perda, 5 = Outro
-- ReviewStatus: 1 = PendenteDeCiencia, 2 = Ciente, 3 = Contestado
CREATE TABLE IF NOT EXISTS stock_movements (
    StockMovementID INT NOT NULL AUTO_INCREMENT,
    ProductID INT NOT NULL,
    StockLocationID INT NOT NULL,
    Quantity DECIMAL(7,3) NOT NULL,
    MovementType TINYINT UNSIGNED NOT NULL,
    TransferID CHAR(36) NULL,
    Reason TINYINT UNSIGNED NULL,
    Justification VARCHAR(255) NULL,
    AuthorizedByEmployeeID INT NULL,
    PerformedByEmployeeID INT NOT NULL,
    ReviewStatus TINYINT UNSIGNED NULL,
    ReviewedByEmployeeID INT NULL,
    ReviewedAt DATETIME NULL,
    MovementDate DATETIME NOT NULL,
    Notes VARCHAR(255) NULL,
    PRIMARY KEY (StockMovementID),
    KEY IX_stock_movements_ProductID (ProductID),
    KEY IX_stock_movements_StockLocationID (StockLocationID),
    KEY IX_stock_movements_TransferID (TransferID),
    KEY IX_stock_movements_AuthorizedByEmployeeID (AuthorizedByEmployeeID),
    KEY IX_stock_movements_PerformedByEmployeeID (PerformedByEmployeeID),
    KEY IX_stock_movements_ReviewedByEmployeeID (ReviewedByEmployeeID),
    CONSTRAINT FK_stock_movements_products FOREIGN KEY (ProductID) REFERENCES products (ProductID),
    CONSTRAINT FK_stock_movements_stock_locations FOREIGN KEY (StockLocationID) REFERENCES stock_locations (StockLocationID),
    CONSTRAINT FK_stock_movements_authorized_by FOREIGN KEY (AuthorizedByEmployeeID) REFERENCES employees (EmployeeID),
    CONSTRAINT FK_stock_movements_performed_by FOREIGN KEY (PerformedByEmployeeID) REFERENCES employees (EmployeeID),
    CONSTRAINT FK_stock_movements_reviewed_by FOREIGN KEY (ReviewedByEmployeeID) REFERENCES employees (EmployeeID)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;

CREATE TABLE IF NOT EXISTS stock_balances (
    StockBalanceID INT NOT NULL AUTO_INCREMENT,
    ProductID INT NOT NULL,
    StockLocationID INT NOT NULL,
    Quantity DECIMAL(7,3) NOT NULL DEFAULT 0,
    UpdatedAt DATETIME NOT NULL,
    PRIMARY KEY (StockBalanceID),
    UNIQUE KEY UX_stock_balances_product_location (ProductID, StockLocationID),
    CONSTRAINT FK_stock_balances_products FOREIGN KEY (ProductID) REFERENCES products (ProductID),
    CONSTRAINT FK_stock_balances_stock_locations FOREIGN KEY (StockLocationID) REFERENCES stock_locations (StockLocationID)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;