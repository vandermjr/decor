-- Permissões do núcleo de estoque (Fase P2)
-- Referência: Md/DECOR_FASE_P2_NUCLEO_ESTOQUE.md

INSERT INTO permissions (PermissionCode, Description) VALUES
    ('StockMovements.View', 'Consultar movimentações de estoque'),
    ('StockMovements.Entry', 'Registrar entrada de estoque'),
    ('StockMovements.Exit', 'Registrar saída de estoque'),
    ('StockMovements.Adjust', 'Registrar ajuste de estoque (balanço, quebra, furto, perda)'),
    ('StockMovements.Transfer', 'Realizar transferência de estoque entre depósitos (MIP)'),
    ('StockMovements.Review', 'Confirmar ou contestar transferências de estoque (MIP)')
ON DUPLICATE KEY UPDATE Description = VALUES(Description);

INSERT IGNORE INTO role_permissions (RoleID, PermissionID)
SELECT r.RoleID, p.PermissionID
FROM roles r
CROSS JOIN permissions p
WHERE r.RoleName = 'Administrador';