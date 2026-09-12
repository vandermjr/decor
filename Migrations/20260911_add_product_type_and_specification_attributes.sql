-- Fase P4, Passo 1 — ProductType em Product + dado mestre ProductSpecificationAttribute
-- Referência: Md/DECOR_FASE_P4_COMERCIAL_VENDA.md

-- ProductType: 1 = Good, 2 = Service
ALTER TABLE products
    ADD COLUMN ProductType TINYINT UNSIGNED NOT NULL DEFAULT 1 AFTER AcquisitionMode;

-- DataType: 1 = Text, 2 = Number, 3 = Boolean, 4 = Enum
-- ProductCategoryID referencia subgroups (categoria/tipo de produto já usada por Product.SubgroupID)
CREATE TABLE IF NOT EXISTS product_specification_attributes (
    AttributeID INT NOT NULL AUTO_INCREMENT,
    ProductCategoryID INT NOT NULL,
    Name VARCHAR(100) NOT NULL,
    DataType TINYINT UNSIGNED NOT NULL,
    Unit VARCHAR(20) NULL,
    EnumOptions TEXT NULL,
    IsRequired TINYINT(1) NOT NULL DEFAULT 0,
    DisplayOrder INT NOT NULL DEFAULT 0,
    PRIMARY KEY (AttributeID),
    KEY IX_product_specification_attributes_ProductCategoryID (ProductCategoryID),
    CONSTRAINT FK_product_specification_attributes_subgroups FOREIGN KEY (ProductCategoryID) REFERENCES subgroups (SubgroupID)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;

INSERT INTO permissions (PermissionCode, Description) VALUES
    ('ProductSpecificationAttributes.View', 'Consultar atributos de especificação de produto'),
    ('ProductSpecificationAttributes.Create', 'Cadastrar atributos de especificação de produto'),
    ('ProductSpecificationAttributes.Edit', 'Editar atributos de especificação de produto'),
    ('ProductSpecificationAttributes.Delete', 'Excluir atributos de especificação de produto')
ON DUPLICATE KEY UPDATE Description = VALUES(Description);

INSERT IGNORE INTO role_permissions (RoleID, PermissionID)
SELECT r.RoleID, p.PermissionID
FROM roles r
CROSS JOIN permissions p
WHERE r.RoleName = 'Administrador';
