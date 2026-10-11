-- Explicit offline maintenance only. Do not execute from application startup.
-- MariaDB 10.11+: send the entire script as one command (no client DELIMITER).
BEGIN NOT ATOMIC
    DECLARE finished BOOLEAN DEFAULT FALSE;
    DECLARE reference_table VARCHAR(64);
    DECLARE reference_column VARCHAR(64);
    DECLARE foreign_key_name VARCHAR(64);
    DECLARE references_cursor CURSOR FOR
        SELECT DISTINCT TABLE_NAME, COLUMN_NAME FROM information_schema.COLUMNS
        WHERE TABLE_SCHEMA = DATABASE()
          AND COLUMN_NAME IN ('ProductID', 'KitProductID', 'ComponentProductID')
          AND TABLE_NAME NOT IN ('products', 'quote_items', 'order_items')
        UNION
        SELECT DISTINCT TABLE_NAME, COLUMN_NAME FROM information_schema.KEY_COLUMN_USAGE
        WHERE TABLE_SCHEMA = DATABASE() AND REFERENCED_TABLE_NAME = 'products'
          AND NOT (TABLE_NAME IN ('quote_items', 'order_items') AND COLUMN_NAME = 'ProductID')
          AND NOT (TABLE_NAME = 'products' AND COLUMN_NAME = 'DefaultInstallationServiceID');
    DECLARE keys_cursor CURSOR FOR
        SELECT TABLE_NAME, CONSTRAINT_NAME FROM information_schema.KEY_COLUMN_USAGE
        WHERE TABLE_SCHEMA = DATABASE()
          AND ((TABLE_NAME IN ('quote_items', 'order_items') AND COLUMN_NAME = 'ProductID'
                AND REFERENCED_TABLE_NAME = 'products')
            OR (TABLE_NAME = 'products' AND COLUMN_NAME = 'DefaultInstallationServiceID'
                AND REFERENCED_TABLE_NAME = 'products'));
    DECLARE CONTINUE HANDLER FOR NOT FOUND SET finished = TRUE;
    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        DROP TEMPORARY TABLE IF EXISTS decor_service_reference_conflicts;
        RESIGNAL;
    END;

    CREATE TABLE IF NOT EXISTS services (
        ServiceID INT NOT NULL AUTO_INCREMENT,
        Description VARCHAR(255) NOT NULL,
        IsActive TINYINT(1) NOT NULL DEFAULT 1,
        CostPrice DECIMAL(10,2) NULL,
        SalePrice DECIMAL(10,2) NULL,
        EmployeeCommissionValue DECIMAL(10,2) NULL,
        Observations TEXT NULL,
        PRIMARY KEY (ServiceID)
    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

    IF EXISTS (SELECT 1 FROM products WHERE ProductType NOT IN (1, 2)
               OR (ProductType = 2 AND (Description IS NULL OR StockQuantity <> 0 OR MinimumStock <> 0
                   OR DefaultInstallationServiceID IS NOT NULL))) THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Invalid legacy product/service fields; reconcile before separation';
    END IF;
    IF EXISTS (SELECT 1 FROM products p JOIN services s ON s.ServiceID = p.ProductID
               WHERE p.ProductType = 2 AND NOT (BINARY s.Description <=> BINARY p.Description
                 AND s.IsActive <=> p.IsActive AND s.CostPrice <=> p.CostPrice
                 AND s.SalePrice <=> p.SalePrice AND s.EmployeeCommissionValue <=> p.EmployeeCommissionValue
                 AND BINARY s.Observations <=> BINARY p.Observations)) THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Service ID collision; refusing to overwrite existing catalog';
    END IF;
    IF EXISTS (SELECT 1 FROM products p LEFT JOIN products legacy
                 ON legacy.ProductID = p.DefaultInstallationServiceID AND legacy.ProductType = 2
               LEFT JOIN services s ON s.ServiceID = p.DefaultInstallationServiceID
               WHERE p.DefaultInstallationServiceID IS NOT NULL AND legacy.ProductID IS NULL AND s.ServiceID IS NULL) THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'DefaultInstallationServiceID does not identify a service';
    END IF;

    CREATE TEMPORARY TABLE decor_service_reference_conflicts (ReferenceName VARCHAR(140) NOT NULL);
    OPEN references_cursor;
    reference_loop: LOOP
        FETCH references_cursor INTO reference_table, reference_column;
        IF finished THEN LEAVE reference_loop; END IF;
        EXECUTE IMMEDIATE CONCAT('INSERT INTO decor_service_reference_conflicts SELECT ',
            QUOTE(CONCAT(reference_table, '.', reference_column)), ' FROM `', REPLACE(reference_table, '`', '``'),
            '` history JOIN products p ON history.`', REPLACE(reference_column, '`', '``'),
            '` = p.ProductID WHERE p.ProductType = 2 LIMIT 1');
    END LOOP;
    CLOSE references_cursor;
    IF EXISTS (SELECT 1 FROM decor_service_reference_conflicts) THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Service referenced by stock/purchase/reservation/kit or unknown history; reconcile first';
    END IF;
    DROP TEMPORARY TABLE decor_service_reference_conflicts;

    SET finished = FALSE;
    OPEN keys_cursor;
    key_loop: LOOP
        FETCH keys_cursor INTO reference_table, foreign_key_name;
        IF finished THEN LEAVE key_loop; END IF;
        EXECUTE IMMEDIATE CONCAT('ALTER TABLE `', REPLACE(reference_table, '`', '``'),
            '` DROP FOREIGN KEY `', REPLACE(foreign_key_name, '`', '``'), '`');
    END LOOP;
    CLOSE keys_cursor;

    ALTER TABLE quote_items ADD COLUMN IF NOT EXISTS ServiceID INT NULL;
    ALTER TABLE order_items ADD COLUMN IF NOT EXISTS ServiceID INT NULL;
    ALTER TABLE quote_items MODIFY COLUMN ProductID INT NULL;
    ALTER TABLE order_items MODIFY COLUMN ProductID INT NULL;

    IF EXISTS (SELECT 1 FROM quote_items WHERE ProductID IS NOT NULL AND ServiceID IS NOT NULL)
       OR EXISTS (SELECT 1 FROM order_items WHERE ProductID IS NOT NULL AND ServiceID IS NOT NULL) THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Commercial item references both catalogs; reconcile first';
    END IF;
    IF EXISTS (SELECT 1 FROM quote_items item LEFT JOIN products p ON p.ProductID = item.ProductID
               LEFT JOIN services s ON s.ServiceID = item.ServiceID
               WHERE (item.ProductID IS NULL AND item.ServiceID IS NULL)
                  OR (item.ProductID IS NOT NULL AND p.ProductID IS NULL)
                  OR (item.ServiceID IS NOT NULL AND s.ServiceID IS NULL))
       OR EXISTS (SELECT 1 FROM order_items item LEFT JOIN products p ON p.ProductID = item.ProductID
               LEFT JOIN services s ON s.ServiceID = item.ServiceID
               WHERE (item.ProductID IS NULL AND item.ServiceID IS NULL)
                  OR (item.ProductID IS NOT NULL AND p.ProductID IS NULL)
                  OR (item.ServiceID IS NOT NULL AND s.ServiceID IS NULL)) THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Commercial item has missing catalog reference; reconcile first';
    END IF;

    START TRANSACTION;
    INSERT INTO services (ServiceID, Description, IsActive, CostPrice, SalePrice, EmployeeCommissionValue, Observations)
    SELECT p.ProductID, p.Description, p.IsActive, p.CostPrice, p.SalePrice, p.EmployeeCommissionValue, p.Observations
    FROM products p LEFT JOIN services s ON s.ServiceID = p.ProductID
    WHERE p.ProductType = 2 AND s.ServiceID IS NULL;

    UPDATE quote_items item JOIN products p ON p.ProductID = item.ProductID AND p.ProductType = 2
    SET item.ServiceID = item.ProductID, item.ProductID = NULL;
    UPDATE order_items item JOIN products p ON p.ProductID = item.ProductID AND p.ProductType = 2
    SET item.ServiceID = item.ProductID, item.ProductID = NULL;
    DELETE FROM products WHERE ProductType = 2;
    COMMIT;

    IF NOT EXISTS (SELECT 1 FROM information_schema.TABLE_CONSTRAINTS WHERE CONSTRAINT_SCHEMA = DATABASE()
                   AND TABLE_NAME = 'products' AND CONSTRAINT_NAME = 'CK_products_goods_only') THEN
        ALTER TABLE products ADD CONSTRAINT CK_products_goods_only CHECK (ProductType = 1);
    END IF;
    IF NOT EXISTS (SELECT 1 FROM information_schema.KEY_COLUMN_USAGE WHERE TABLE_SCHEMA = DATABASE()
                   AND TABLE_NAME = 'products' AND COLUMN_NAME = 'DefaultInstallationServiceID' AND REFERENCED_TABLE_NAME = 'services') THEN
        ALTER TABLE products ADD CONSTRAINT FK_products_default_installation_service
            FOREIGN KEY (DefaultInstallationServiceID) REFERENCES services (ServiceID);
    END IF;
    IF NOT EXISTS (SELECT 1 FROM information_schema.KEY_COLUMN_USAGE WHERE TABLE_SCHEMA = DATABASE()
                   AND TABLE_NAME = 'quote_items' AND COLUMN_NAME = 'ProductID' AND REFERENCED_TABLE_NAME = 'products') THEN
        ALTER TABLE quote_items ADD CONSTRAINT FK_quote_items_products FOREIGN KEY (ProductID) REFERENCES products (ProductID);
    END IF;
    IF NOT EXISTS (SELECT 1 FROM information_schema.KEY_COLUMN_USAGE WHERE TABLE_SCHEMA = DATABASE()
                   AND TABLE_NAME = 'order_items' AND COLUMN_NAME = 'ProductID' AND REFERENCED_TABLE_NAME = 'products') THEN
        ALTER TABLE order_items ADD CONSTRAINT FK_order_items_products FOREIGN KEY (ProductID) REFERENCES products (ProductID);
    END IF;
    IF NOT EXISTS (SELECT 1 FROM information_schema.KEY_COLUMN_USAGE WHERE TABLE_SCHEMA = DATABASE()
                   AND TABLE_NAME = 'quote_items' AND COLUMN_NAME = 'ServiceID' AND REFERENCED_TABLE_NAME = 'services') THEN
        ALTER TABLE quote_items ADD CONSTRAINT FK_quote_items_services FOREIGN KEY (ServiceID) REFERENCES services (ServiceID);
    END IF;
    IF NOT EXISTS (SELECT 1 FROM information_schema.KEY_COLUMN_USAGE WHERE TABLE_SCHEMA = DATABASE()
                   AND TABLE_NAME = 'order_items' AND COLUMN_NAME = 'ServiceID' AND REFERENCED_TABLE_NAME = 'services') THEN
        ALTER TABLE order_items ADD CONSTRAINT FK_order_items_services FOREIGN KEY (ServiceID) REFERENCES services (ServiceID);
    END IF;
    IF NOT EXISTS (SELECT 1 FROM information_schema.TABLE_CONSTRAINTS WHERE CONSTRAINT_SCHEMA = DATABASE()
                   AND TABLE_NAME = 'quote_items' AND CONSTRAINT_NAME = 'CK_quote_items_one_catalog') THEN
        ALTER TABLE quote_items ADD CONSTRAINT CK_quote_items_one_catalog CHECK ((ProductID IS NULL) <> (ServiceID IS NULL));
    END IF;
    IF NOT EXISTS (SELECT 1 FROM information_schema.TABLE_CONSTRAINTS WHERE CONSTRAINT_SCHEMA = DATABASE()
                   AND TABLE_NAME = 'order_items' AND CONSTRAINT_NAME = 'CK_order_items_one_catalog') THEN
        ALTER TABLE order_items ADD CONSTRAINT CK_order_items_one_catalog CHECK ((ProductID IS NULL) <> (ServiceID IS NULL));
    END IF;
END;

INSERT IGNORE INTO permissions (PermissionCode, Description) VALUES ('Services.View', 'Consultar servicos');
INSERT IGNORE INTO role_permissions (RoleID, PermissionID)
SELECT r.RoleID, p.PermissionID FROM roles r CROSS JOIN permissions p
WHERE r.IsSystemProtected = 1 AND r.RoleName IN ('Administrador', 'Administradores')
  AND p.PermissionCode = 'Services.View' AND ROW_COUNT() = 1;
INSERT IGNORE INTO permissions (PermissionCode, Description) VALUES ('Services.Create', 'Cadastrar servicos');
INSERT IGNORE INTO role_permissions (RoleID, PermissionID)
SELECT r.RoleID, p.PermissionID FROM roles r CROSS JOIN permissions p
WHERE r.IsSystemProtected = 1 AND r.RoleName IN ('Administrador', 'Administradores')
  AND p.PermissionCode = 'Services.Create' AND ROW_COUNT() = 1;
INSERT IGNORE INTO permissions (PermissionCode, Description) VALUES ('Services.Edit', 'Editar servicos');
INSERT IGNORE INTO role_permissions (RoleID, PermissionID)
SELECT r.RoleID, p.PermissionID FROM roles r CROSS JOIN permissions p
WHERE r.IsSystemProtected = 1 AND r.RoleName IN ('Administrador', 'Administradores')
  AND p.PermissionCode = 'Services.Edit' AND ROW_COUNT() = 1;
INSERT IGNORE INTO permissions (PermissionCode, Description) VALUES ('Services.Delete', 'Excluir servicos');
INSERT IGNORE INTO role_permissions (RoleID, PermissionID)
SELECT r.RoleID, p.PermissionID FROM roles r CROSS JOIN permissions p
WHERE r.IsSystemProtected = 1 AND r.RoleName IN ('Administrador', 'Administradores')
  AND p.PermissionCode = 'Services.Delete' AND ROW_COUNT() = 1;