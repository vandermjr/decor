-- Fase P5, Passo 2 — ProductKitComponent (BOM / Lista de Materiais de um Kit)
-- Referência: Md/DECOR_FASE_P5_SERVIÇOS.md
--
-- Não existe flag "IsKit" em Product: um Product só "é um Kit" quando tem 1+
-- linhas em product_kit_components como KitProductID.

CREATE TABLE IF NOT EXISTS product_kit_components (
    ComponentID INT NOT NULL AUTO_INCREMENT,
    KitProductID INT NOT NULL,
    ComponentProductID INT NOT NULL,
    Quantity DECIMAL(10,3) NOT NULL,
    IsVisibleToCustomer TINYINT(1) NOT NULL DEFAULT 1,
    DisplayOrder INT NOT NULL DEFAULT 0,
    PRIMARY KEY (ComponentID),
    KEY IX_product_kit_components_KitProductID (KitProductID),
    KEY IX_product_kit_components_ComponentProductID (ComponentProductID),
    CONSTRAINT FK_product_kit_components_kit_product FOREIGN KEY (KitProductID) REFERENCES products (ProductID),
    CONSTRAINT FK_product_kit_components_component_product FOREIGN KEY (ComponentProductID) REFERENCES products (ProductID)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;

INSERT INTO permissions (PermissionCode, Description) VALUES
    ('ProductKitComponents.View', 'Consultar componentes de kit de produto'),
    ('ProductKitComponents.Create', 'Cadastrar componentes de kit de produto'),
    ('ProductKitComponents.Edit', 'Editar componentes de kit de produto'),
    ('ProductKitComponents.Delete', 'Excluir componentes de kit de produto')
ON DUPLICATE KEY UPDATE Description = VALUES(Description);

INSERT IGNORE INTO role_permissions (RoleID, PermissionID)
SELECT r.RoleID, p.PermissionID
FROM roles r
CROSS JOIN permissions p
WHERE r.RoleName = 'Administrador'
  AND p.PermissionCode IN ('ProductKitComponents.View', 'ProductKitComponents.Create', 'ProductKitComponents.Edit', 'ProductKitComponents.Delete');
