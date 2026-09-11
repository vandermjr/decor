-- Permissões de compras/recebimento (Fase P3)
-- Referência: Md/ (Fase P3 — Compras/Recebimento)

INSERT INTO permissions (PermissionCode, Description) VALUES
    ('PurchaseOrders.View', 'Consultar pedidos de compra'),
    ('PurchaseOrders.Create', 'Cadastrar pedidos de compra'),
    ('PurchaseOrders.Edit', 'Editar pedidos de compra'),
    ('PurchaseOrders.Delete', 'Excluir pedidos de compra'),
    ('PurchaseOrderItems.View', 'Consultar itens de pedido de compra'),
    ('PurchaseOrderItems.Create', 'Cadastrar itens de pedido de compra'),
    ('PurchaseOrderItems.Edit', 'Editar itens de pedido de compra'),
    ('PurchaseOrderItems.Delete', 'Excluir itens de pedido de compra'),
    ('GoodsReceipts.View', 'Consultar recebimentos de mercadoria'),
    ('GoodsReceipts.Register', 'Registrar recebimento de mercadoria')
ON DUPLICATE KEY UPDATE Description = VALUES(Description);

INSERT IGNORE INTO role_permissions (RoleID, PermissionID)
SELECT r.RoleID, p.PermissionID
FROM roles r
CROSS JOIN permissions p
WHERE r.RoleName = 'Administrador';