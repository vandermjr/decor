-- Fase UM, Passo 2 — Product.StockUnitID
-- Requisito: nullable no schema; obrigação apenas para produtos Good daqui para frente.
-- Não há backfill: os produtos existentes permanecem com StockUnitID = NULL.

ALTER TABLE products
    ADD COLUMN StockUnitID INT NULL AFTER SubgroupID;

ALTER TABLE products
    ADD KEY IX_products_StockUnitID (StockUnitID),
    ADD CONSTRAINT FK_products_stock_unit FOREIGN KEY (StockUnitID) REFERENCES unit_of_measures (UnitOfMeasureID);