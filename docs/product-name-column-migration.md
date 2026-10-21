# Product column rename

The product catalog field is now named `ProductName` in C# and `Product` in the `products` table. The Portuguese UI label is `Produto`.

Before deploying this version, back up the target database and run `Migrations/20261019_rename_product_description_to_product.sql` once during a maintenance window with application writers stopped. It renames the column without changing its type or data and rebuilds the product full-text index. This migration is intentionally not part of application startup.

Verify the resulting schema with:

```sql
SHOW FULL COLUMNS FROM products LIKE 'Product';
SHOW INDEX FROM products WHERE Key_name = 'IX_products_Product_FullText';
```
