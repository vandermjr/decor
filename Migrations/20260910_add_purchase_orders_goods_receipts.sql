-- Fase P3 — compras e recebimento (PurchaseOrder, PurchaseOrderItem, GoodsReceipt)
-- Referência: Md/DECOR_FASE_P3_COMPRAS_RECEBIMENTO.md

-- Status: 1 = Aberto, 2 = ParcialmenteRecebido, 3 = Recebido, 4 = Cancelado
CREATE TABLE IF NOT EXISTS purchase_orders (
    PurchaseOrderID INT NOT NULL AUTO_INCREMENT,
    SupplierID INT NOT NULL,
    OrderDate DATETIME NOT NULL,
    Status TINYINT UNSIGNED NOT NULL,
    Notes VARCHAR(255) NULL,
    PRIMARY KEY (PurchaseOrderID),
    KEY IX_purchase_orders_SupplierID (SupplierID),
    CONSTRAINT FK_purchase_orders_suppliers FOREIGN KEY (SupplierID) REFERENCES suppliers (SupplierID)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;

-- ReceivingMethod: 1 = Transportadora, 2 = RetiradaPropria
-- FinalDestination: 1 = DepositoEmpresa, 2 = DiretoCliente
CREATE TABLE IF NOT EXISTS purchase_order_items (
    PurchaseOrderItemID INT NOT NULL AUTO_INCREMENT,
    PurchaseOrderID INT NOT NULL,
    ProductID INT NOT NULL,
    QuantityOrdered DECIMAL(7,3) NOT NULL,
    UnitPrice DECIMAL(10,2) NOT NULL,
    ReceivingMethod TINYINT UNSIGNED NOT NULL,
    FinalDestination TINYINT UNSIGNED NOT NULL,
    StockLocationID INT NULL,
    CustomerID INT NULL,
    PRIMARY KEY (PurchaseOrderItemID),
    KEY IX_purchase_order_items_PurchaseOrderID (PurchaseOrderID),
    KEY IX_purchase_order_items_ProductID (ProductID),
    KEY IX_purchase_order_items_StockLocationID (StockLocationID),
    KEY IX_purchase_order_items_CustomerID (CustomerID),
    CONSTRAINT FK_purchase_order_items_orders FOREIGN KEY (PurchaseOrderID) REFERENCES purchase_orders (PurchaseOrderID),
    CONSTRAINT FK_purchase_order_items_products FOREIGN KEY (ProductID) REFERENCES products (ProductID),
    CONSTRAINT FK_purchase_order_items_stock_locations FOREIGN KEY (StockLocationID) REFERENCES stock_locations (StockLocationID),
    CONSTRAINT FK_purchase_order_items_customers FOREIGN KEY (CustomerID) REFERENCES customers (CustomerID)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;

-- Status: 1 = Conferido, 2 = DivergenteDevolvido
CREATE TABLE IF NOT EXISTS goods_receipts (
    GoodsReceiptID INT NOT NULL AUTO_INCREMENT,
    PurchaseOrderItemID INT NOT NULL,
    ReceiptDate DATETIME NOT NULL,
    QuantityReceived DECIMAL(7,3) NOT NULL,
    ReceivedByEmployeeID INT NOT NULL,
    HasDivergence TINYINT(1) NOT NULL DEFAULT 0,
    DivergenceNotes VARCHAR(255) NULL,
    Status TINYINT UNSIGNED NOT NULL,
    PRIMARY KEY (GoodsReceiptID),
    KEY IX_goods_receipts_PurchaseOrderItemID (PurchaseOrderItemID),
    KEY IX_goods_receipts_ReceivedByEmployeeID (ReceivedByEmployeeID),
    CONSTRAINT FK_goods_receipts_order_items FOREIGN KEY (PurchaseOrderItemID) REFERENCES purchase_order_items (PurchaseOrderItemID),
    CONSTRAINT FK_goods_receipts_employees FOREIGN KEY (ReceivedByEmployeeID) REFERENCES employees (EmployeeID)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;