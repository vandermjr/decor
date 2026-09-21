// Inicia o builder (os aliases são resolvidos automaticamente por convenção)
var builder = FluentCommandBuilder.Create(new MariaDBDialect());

// Cria a query SELECT com todas as colunas
var (sql, parameters) = builder
    .Select<Product>()
    .AllColumns()
    .Build();

// SQL gerado:
// SELECT p.* FROM products AS p ORDER BY p.ProductID ASC;

// Cria a query SELECT com colunas específicas
var (sql, parameters) = builder
    .Select<Product>()
    .WithColumns(p => p.ProductID, p => p.Description, p => p.StockQuantity)
    .Build();

// SQL gerado:
// SELECT p.ProductID, p.Description, p.StockQuantity FROM products AS p ORDER BY p.ProductID ASC;

// Cria a query SELECT em um bloco de código com filtros e joins
var (sql, parameters) = builder
    .Select<Product>()
    .WithColumns(p => p.ProductID, p => p.Description, p => p.IsActive)
    .Where(w => w.Equals<Product>(p => p.IsActive, true))
    .Build();

// SQL gerado:
// SELECT p.ProductID, p.Description, p.IsActive FROM products AS p WHERE p.IsActive = @IsActive ORDER BY p.ProductID ASC;