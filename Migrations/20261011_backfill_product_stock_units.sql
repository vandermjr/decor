-- Assign the baseline unit to products created before StockUnitID was introduced.
-- Review product-specific units and update exceptions after applying this migration.
UPDATE products AS p
JOIN unit_of_measures AS u ON u.Code = 'UN'
SET p.StockUnitID = u.UnitOfMeasureID
WHERE p.StockUnitID IS NULL;
