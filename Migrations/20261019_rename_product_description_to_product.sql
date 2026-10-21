-- Offline maintenance only. Run once against each database after deploying the ProductName mapping.
-- MariaDB may have created the original full-text index under either legacy name.
DROP INDEX IF EXISTS IX_products_Description_FullText ON products;
DROP INDEX IF EXISTS FT_products_Description ON products;
ALTER TABLE products RENAME COLUMN Description TO Product;
CREATE FULLTEXT INDEX IX_products_Product_FullText ON products (Product);
