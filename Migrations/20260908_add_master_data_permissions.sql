-- Permissões para os dados mestres da Fase P1 (Customer, Supplier, Employee, Partner, StockLocation)
-- Referência: Md/DECOR_FASE_A1_DECISOES_BLOQUEADORAS.md

INSERT INTO permissions (PermissionCode, Description) VALUES
    ('Customers.View', 'Consultar clientes'),
    ('Customers.Create', 'Cadastrar clientes'),
    ('Customers.Edit', 'Editar clientes'),
    ('Customers.Delete', 'Excluir clientes'),
    ('Suppliers.View', 'Consultar fornecedores'),
    ('Suppliers.Create', 'Cadastrar fornecedores'),
    ('Suppliers.Edit', 'Editar fornecedores'),
    ('Suppliers.Delete', 'Excluir fornecedores'),
    ('Employees.View', 'Consultar funcionários'),
    ('Employees.Create', 'Cadastrar funcionários'),
    ('Employees.Edit', 'Editar funcionários'),
    ('Employees.Delete', 'Excluir funcionários'),
    ('Partners.View', 'Consultar parceiros'),
    ('Partners.Create', 'Cadastrar parceiros'),
    ('Partners.Edit', 'Editar parceiros'),
    ('Partners.Delete', 'Excluir parceiros'),
    ('StockLocations.View', 'Consultar depósitos'),
    ('StockLocations.Create', 'Cadastrar depósitos'),
    ('StockLocations.Edit', 'Editar depósitos'),
    ('StockLocations.Delete', 'Excluir depósitos')
ON DUPLICATE KEY UPDATE Description = VALUES(Description);

INSERT IGNORE INTO role_permissions (RoleID, PermissionID)
SELECT r.RoleID, p.PermissionID
FROM roles r
CROSS JOIN permissions p
WHERE r.RoleName = 'Administrador';
