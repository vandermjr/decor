-- Fase P4, Passo 2 — Quote (orçamento comercial)
-- Entrega do agregado: Quote, QuoteSection, QuoteItem, QuoteItemSpecificationValue

CREATE TABLE IF NOT EXISTS quotes (
    QuoteID INT NOT NULL AUTO_INCREMENT,
    CustomerID INT NOT NULL,
    CreatedByEmployeeID INT NOT NULL,
    SourcePartnerID INT NULL,
    SourceType TINYINT UNSIGNED NOT NULL,
    CreatedAt DATETIME NOT NULL,
    Notes TEXT NULL,
    PRIMARY KEY (QuoteID),
    KEY IX_quotes_CustomerID (CustomerID),
    KEY IX_quotes_CreatedByEmployeeID (CreatedByEmployeeID),
    KEY IX_quotes_SourcePartnerID (SourcePartnerID),
    CONSTRAINT FK_quotes_customers FOREIGN KEY (CustomerID) REFERENCES customers (CustomerID),
    CONSTRAINT FK_quotes_employees FOREIGN KEY (CreatedByEmployeeID) REFERENCES employees (EmployeeID),
    CONSTRAINT FK_quotes_partners FOREIGN KEY (SourcePartnerID) REFERENCES partners (PartnerID)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;

CREATE TABLE IF NOT EXISTS quote_sections (
    QuoteSectionID INT NOT NULL AUTO_INCREMENT,
    QuoteID INT NOT NULL,
    SectionType TINYINT UNSIGNED NOT NULL,
    Status TINYINT UNSIGNED NOT NULL,
    SentToCustomerAt DATETIME NULL,
    ApprovedAt DATETIME NULL,
    CreatedAt DATETIME NOT NULL,
    PRIMARY KEY (QuoteSectionID),
    KEY IX_quote_sections_QuoteID (QuoteID),
    CONSTRAINT FK_quote_sections_quotes FOREIGN KEY (QuoteID) REFERENCES quotes (QuoteID)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;

CREATE TABLE IF NOT EXISTS quote_items (
    QuoteItemID INT NOT NULL AUTO_INCREMENT,
    QuoteSectionID INT NOT NULL,
    ProductID INT NOT NULL,
    Quantity DECIMAL(12,3) NOT NULL DEFAULT 0,
    UnitPrice DECIMAL(12,2) NULL,
    HasInstallationService TINYINT(1) NOT NULL DEFAULT 0,
    PRIMARY KEY (QuoteItemID),
    KEY IX_quote_items_QuoteSectionID (QuoteSectionID),
    KEY IX_quote_items_ProductID (ProductID),
    CONSTRAINT FK_quote_items_quote_sections FOREIGN KEY (QuoteSectionID) REFERENCES quote_sections (QuoteSectionID),
    CONSTRAINT FK_quote_items_products FOREIGN KEY (ProductID) REFERENCES products (ProductID)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;

CREATE TABLE IF NOT EXISTS quote_item_specification_values (
    ValueID INT NOT NULL AUTO_INCREMENT,
    QuoteItemID INT NOT NULL,
    AttributeID INT NOT NULL,
    Value VARCHAR(255) NOT NULL,
    PRIMARY KEY (ValueID),
    KEY IX_quote_item_specification_values_QuoteItemID (QuoteItemID),
    KEY IX_quote_item_specification_values_AttributeID (AttributeID),
    CONSTRAINT FK_quote_item_spec_values_quote_items FOREIGN KEY (QuoteItemID) REFERENCES quote_items (QuoteItemID),
    CONSTRAINT FK_quote_item_spec_values_attributes FOREIGN KEY (AttributeID) REFERENCES product_specification_attributes (AttributeID)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;

INSERT INTO permissions (PermissionCode, Description) VALUES
    ('Quotes.View', 'Consultar orçamentos'),
    ('Quotes.Create', 'Cadastrar orçamentos'),
    ('Quotes.Edit', 'Editar orçamentos'),
    ('Quotes.Delete', 'Excluir orçamentos'),
    ('Quotes.Send', 'Enviar orçamentos para o cliente'),
    ('Quotes.Approve', 'Aprovar ou rejeitar orçamentos')
ON DUPLICATE KEY UPDATE Description = VALUES(Description);

INSERT IGNORE INTO role_permissions (RoleID, PermissionID)
SELECT r.RoleID, p.PermissionID
FROM roles r
CROSS JOIN permissions p
WHERE r.RoleName = 'Administrador';
