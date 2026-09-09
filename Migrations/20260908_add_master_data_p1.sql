-- Fase P1 — dados mestres (Customer, Supplier, Employee, Partner, StockLocation)
-- Referência: Md/DECOR_FASE_A1_DECISOES_BLOQUEADORAS.md (D1-D6)

CREATE TABLE IF NOT EXISTS customers (
    CustomerID INT NOT NULL AUTO_INCREMENT,
    Name VARCHAR(150) NOT NULL,
    Document VARCHAR(20) NULL,
    Phone VARCHAR(20) NULL,
    Email VARCHAR(150) NULL,
    Address VARCHAR(255) NULL,
    IsActive TINYINT(1) NOT NULL DEFAULT 1,
    PRIMARY KEY (CustomerID)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;

CREATE TABLE IF NOT EXISTS suppliers (
    SupplierID INT NOT NULL AUTO_INCREMENT,
    CorporateName VARCHAR(150) NOT NULL,
    Document VARCHAR(20) NULL,
    Phone VARCHAR(20) NULL,
    Email VARCHAR(150) NULL,
    IsActive TINYINT(1) NOT NULL DEFAULT 1,
    PRIMARY KEY (SupplierID)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;

CREATE TABLE IF NOT EXISTS employees (
    EmployeeID INT NOT NULL AUTO_INCREMENT,
    Name VARCHAR(150) NOT NULL,
    Document VARCHAR(20) NULL,
    Phone VARCHAR(20) NULL,
    IsActive TINYINT(1) NOT NULL DEFAULT 1,
    UserID INT NULL,
    PRIMARY KEY (EmployeeID),
    KEY IX_employees_UserID (UserID),
    CONSTRAINT FK_employees_users FOREIGN KEY (UserID) REFERENCES users (UserID)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;

-- PartnerType: 1 = Fabricante, 2 = Instalador (ver Product.cs / Partner.cs)
CREATE TABLE IF NOT EXISTS partners (
    PartnerID INT NOT NULL AUTO_INCREMENT,
    Name VARCHAR(150) NOT NULL,
    Document VARCHAR(20) NULL,
    Phone VARCHAR(20) NULL,
    PartnerType TINYINT UNSIGNED NOT NULL,
    IsActive TINYINT(1) NOT NULL DEFAULT 1,
    PRIMARY KEY (PartnerID)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;

-- LocationType: 1 = Empresa, 2 = Parceiro
CREATE TABLE IF NOT EXISTS stock_locations (
    StockLocationID INT NOT NULL AUTO_INCREMENT,
    Name VARCHAR(150) NOT NULL,
    LocationType TINYINT UNSIGNED NOT NULL,
    PartnerID INT NULL,
    IsActive TINYINT(1) NOT NULL DEFAULT 1,
    PRIMARY KEY (StockLocationID),
    KEY IX_stock_locations_PartnerID (PartnerID),
    CONSTRAINT FK_stock_locations_partners FOREIGN KEY (PartnerID) REFERENCES partners (PartnerID)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;

-- Origin: 1 = Compra, 2 = Manufatura
-- AcquisitionMode: 1 = Estocado, 2 = SobEncomenda
ALTER TABLE products
    ADD COLUMN Origin TINYINT UNSIGNED NOT NULL DEFAULT 1 AFTER StockQuantity,
    ADD COLUMN AcquisitionMode TINYINT UNSIGNED NOT NULL DEFAULT 1 AFTER Origin;

-- Depósito padrão da empresa, para já existir um destino válido de estoque
INSERT INTO stock_locations (Name, LocationType, PartnerID, IsActive)
SELECT 'Depósito Empresa', 1, NULL, 1
WHERE NOT EXISTS (SELECT 1 FROM stock_locations WHERE LocationType = 1);