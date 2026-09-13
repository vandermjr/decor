-- Fase P5, Passo 3 — PartnerPriceTable (Tabela de Preço por m² do Parceiro) + MeasurementRole em ProductSpecificationAttribute
-- Referência: Md/DECOR_FASE_P5_SERVIÇOS.md

-- MeasurementRole em ProductSpecificationAttribute: 0 = None, 1 = Width, 2 = Height
ALTER TABLE product_specification_attributes
    ADD COLUMN MeasurementRole TINYINT UNSIGNED NOT NULL DEFAULT 0;

CREATE TABLE IF NOT EXISTS partner_price_tables (
    PriceTableID INT NOT NULL AUTO_INCREMENT,
    PartnerID INT NOT NULL,
    GroupID INT NOT NULL,
    PricePerSquareMeter DECIMAL(10,2) NOT NULL,
    IsActive TINYINT(1) NOT NULL DEFAULT 1,
    PRIMARY KEY (PriceTableID),
    KEY IX_partner_price_tables_PartnerID (PartnerID),
    KEY IX_partner_price_tables_GroupID (GroupID),
    CONSTRAINT FK_partner_price_tables_partner FOREIGN KEY (PartnerID) REFERENCES partners (PartnerID),
    CONSTRAINT FK_partner_price_tables_group FOREIGN KEY (GroupID) REFERENCES groups (GroupID)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;

INSERT INTO permissions (PermissionCode, Description) VALUES
    ('PartnerPriceTables.View', 'Consultar tabelas de preços de parceiros'),
    ('PartnerPriceTables.Create', 'Cadastrar tabelas de preços de parceiros'),
    ('PartnerPriceTables.Edit', 'Editar tabelas de preços de parceiros'),
    ('PartnerPriceTables.Delete', 'Excluir tabelas de preços de parceiros')
ON DUPLICATE KEY UPDATE Description = VALUES(Description);

INSERT IGNORE INTO role_permissions (RoleID, PermissionID)
SELECT r.RoleID, p.PermissionID
FROM roles r
CROSS JOIN permissions p
WHERE r.RoleName = 'Administrador'
  AND p.PermissionCode IN ('PartnerPriceTables.View', 'PartnerPriceTables.Create', 'PartnerPriceTables.Edit', 'PartnerPriceTables.Delete');
