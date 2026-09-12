-- Fase P5, Passo 1 — Campos de precificação e serviço em Product
-- Referência: Md/DECOR_FASE_P5_SERVIÇOS.md
--
-- Confirmado por análise do banco real: hoje todos os produtos cadastrados têm
-- ProductType = Good; não existe nenhum Service cadastrado ainda — portanto esta
-- migration NÃO inclui passo de UPDATE de dados, só alteração de schema.

-- SubgroupID passa a ser opcional: obrigatório apenas quando ProductType = Good,
-- deve ser NULL quando ProductType = Service. Validação migra para ProductService.
ALTER TABLE products
    DROP FOREIGN KEY FK_products_subgroups;

ALTER TABLE products
    MODIFY COLUMN SubgroupID INT NULL;

ALTER TABLE products
    ADD CONSTRAINT FK_products_subgroups FOREIGN KEY (SubgroupID) REFERENCES subgroups (SubgroupID);

ALTER TABLE products
    ADD COLUMN CostPrice DECIMAL(10,2) NULL AFTER ProductType,
    ADD COLUMN SalePrice DECIMAL(10,2) NULL AFTER CostPrice,
    ADD COLUMN EmployeeCommissionValue DECIMAL(10,2) NULL AFTER SalePrice,
    ADD COLUMN DefaultInstallationServiceID INT NULL AFTER EmployeeCommissionValue;

ALTER TABLE products
    ADD KEY IX_products_DefaultInstallationServiceID (DefaultInstallationServiceID),
    ADD CONSTRAINT FK_products_default_installation_service FOREIGN KEY (DefaultInstallationServiceID) REFERENCES products (ProductID);
