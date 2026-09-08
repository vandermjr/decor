-- Run once against the dcg database with a MariaDB account that has ALTER or INDEX privilege.
-- This index supports QueryBuilder.FullText(...) on products.Description.
CREATE FULLTEXT INDEX IX_products_Description_FullText
    ON products (Description);