using Dapper;
using Decor.Application.Services;
using Decor.Application.Validation;
using Decor.Core.Common;
using Decor.Core.DTOs;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Data;
using Decor.Core.Interfaces.Repositories;
using Decor.Core.Interfaces.Services;
using Decor.Core.Validation;
using Decor.FluentSqlBuilder;
using Decor.FluentSqlBuilder.Dialects.MariaDB;
using Decor.Infrastructure.Data;
using Decor.Infrastructure.Data.Repositories;
using FluentAssertions;
using MySqlConnector;

namespace Decor.Infrastructure.IntegrationTests;

public sealed class MultiSaveTransactionIntegrationTests(MariaDbFixture fixture) : IClassFixture<MariaDbFixture>
{
    [Fact]
    public async Task ConvertFromQuote_WhenSecondOrderItemFails_RollsBackEntireConversion()
    {
        await using var connection = new MySqlConnection(fixture.ConnectionString);
        await EnsureOrderTablesAsync(connection);

        var customerId = await InsertIdAsync(connection, "INSERT INTO customers (Name, IsActive) VALUES ('Txn Customer', 1);");
        var employeeId = await InsertIdAsync(connection, "INSERT INTO employees (Name, IsActive) VALUES ('Txn Employee', 1);");
        var productId = await InsertIdAsync(connection, "INSERT INTO products (Description, IsActive, ProductType) VALUES ('Valid Product', 1, 1);");
        var attributeId = await InsertIdAsync(connection, "INSERT INTO product_specification_attributes (ProductCategoryID, Name, DataType, IsRequired, DisplayOrder) VALUES (1, 'Color', 1, 0, 1);");
        var quoteId = await InsertIdAsync(connection, $"INSERT INTO quotes (CustomerID, CreatedByEmployeeID, SourceType, CreatedAt) VALUES ({customerId}, {employeeId}, 1, NOW());");
        var sectionId = await InsertIdAsync(connection, $"INSERT INTO quote_sections (QuoteID, SectionType, Status, CreatedAt) VALUES ({quoteId}, 2, 4, NOW());");
        var firstQuoteItemId = await InsertIdAsync(connection, $"INSERT INTO quote_items (QuoteSectionID, ProductID, Quantity, UnitPrice, HasInstallationService) VALUES ({sectionId}, {productId}, 1, 10, 0);");
        var secondQuoteItemId = await InsertIdAsync(connection, $"INSERT INTO quote_items (QuoteSectionID, ProductID, Quantity, UnitPrice, HasInstallationService) VALUES ({sectionId}, 999999, 1, 20, 0);");

        var quote = new Quote { QuoteID = quoteId, CustomerID = customerId, Sections = [] };
        var section = new QuoteSection { QuoteSectionID = sectionId, QuoteID = quoteId, SectionType = QuoteSectionType.Custom, Status = QuoteSectionStatus.Approved, Items = [] };
        section.Items.Add(new QuoteItem { QuoteItemID = firstQuoteItemId, QuoteSectionID = sectionId, ProductID = productId, Quantity = 1, UnitPrice = 10, SpecificationValues = [new QuoteItemSpecificationValue { QuoteItemID = firstQuoteItemId, AttributeID = attributeId, Value = "Blue" }] });
        section.Items.Add(new QuoteItem { QuoteItemID = secondQuoteItemId, QuoteSectionID = sectionId, ProductID = 999999, Quantity = 1, UnitPrice = 20, SpecificationValues = [] });
        quote.Sections.Add(section);

        var orderRepository = CreateOrderRepository();
        var quoteRepository = CreateQuoteRepository();
        var service = new OrderService(
            orderRepository,
            new FixedQuoteRepository(quote),
            new EmptyProductRepository(),
            new EmptyReservationService(),
            new EmptyInstallmentService(),
            new OrderDTOValidator(),
            new OrderRepositoryValidator(orderRepository),
            new AllowAuthorizationService(),
            new DatabaseConnection(fixture.ConnectionString),
            (ITransactionalOrderRepository)orderRepository,
            (ITransactionalQuoteRepository)quoteRepository);

        var act = () => service.ConvertFromQuoteAsync(sectionId);
        await act.Should().ThrowAsync<MySqlException>();

        (await connection.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM orders WHERE QuoteSectionID = {sectionId}")).Should().Be(0);
        (await connection.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM order_items WHERE QuoteItemID IN ({firstQuoteItemId}, {secondQuoteItemId})")).Should().Be(0);
        (await connection.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM order_item_specification_values WHERE AttributeID = {attributeId}")).Should().Be(0);
        (await connection.ExecuteScalarAsync<int>($"SELECT Status FROM quote_sections WHERE QuoteSectionID = {sectionId}")).Should().Be((int)QuoteSectionStatus.Approved);
    }

    [Fact]
    public async Task RegisterReceipt_WhenStockMovementFails_RollsBackReceiptAndMovement()
    {
        await using var connection = new MySqlConnection(fixture.ConnectionString);
        await EnsureGoodsReceiptTablesAsync(connection);

        var employeeId = await InsertIdAsync(connection, "INSERT INTO employees (Name, IsActive) VALUES ('Receipt Employee', 1);");
        var productId = await InsertIdAsync(connection, "INSERT INTO products (Description, IsActive, ProductType) VALUES ('Receipt Product', 1, 1);");
        var supplierId = await InsertIdAsync(connection, "INSERT INTO suppliers (Name, IsActive) VALUES ('Receipt Supplier', 1);");
        var purchaseOrderId = await InsertIdAsync(connection, $"INSERT INTO purchase_orders (SupplierID, OrderDate, Status) VALUES ({supplierId}, NOW(), 1);");
        var purchaseOrderItemId = await InsertIdAsync(connection, $"INSERT INTO purchase_order_items (PurchaseOrderID, ProductID, QuantityOrdered, UnitPrice, ReceivingMethod, FinalDestination, StockLocationID) VALUES ({purchaseOrderId}, {productId}, 1, 10, 1, 1, 999999);");

        var databaseConnection = new DatabaseConnection(fixture.ConnectionString);
        var goodsReceiptRepository = new GoodsReceiptRepository(databaseConnection, () => FluentCommandBuilder.Create(new MariaDBDialect()));
        var movementRepository = new StockMovementRepository(databaseConnection, () => FluentCommandBuilder.Create(new MariaDBDialect()));
        var service = new GoodsReceiptService(
            goodsReceiptRepository,
            new FixedPurchaseOrderItemRepository(new PurchaseOrderItem { PurchaseOrderItemID = purchaseOrderItemId, PurchaseOrderID = purchaseOrderId, ProductID = productId, FinalDestination = PurchaseOrderFinalDestination.DepositoEmpresa, StockLocationID = 999999 }),
            new StockMovementService(movementRepository, new AllowAuthorizationService()),
            new AllowAuthorizationService(),
            databaseConnection,
            (ITransactionalGoodsReceiptRepository)goodsReceiptRepository,
            new StockMovementService(movementRepository, new AllowAuthorizationService()));

        var act = () => service.RegisterReceiptAsync(purchaseOrderItemId, 1, employeeId, false);
        await act.Should().ThrowAsync<MySqlException>();

        (await connection.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM goods_receipts WHERE PurchaseOrderItemID = {purchaseOrderItemId}")).Should().Be(0);
        (await connection.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM stock_movements WHERE ProductID = {productId}")).Should().Be(0);
    }

    private OrderRepository CreateOrderRepository()
    {
        var db = new DatabaseConnection(fixture.ConnectionString);
        return new OrderRepository(db, () => FluentCommandBuilder.Create(new MariaDBDialect()));
    }

    private QuoteRepository CreateQuoteRepository()
    {
        var db = new DatabaseConnection(fixture.ConnectionString);
        return new QuoteRepository(db, () => FluentCommandBuilder.Create(new MariaDBDialect()));
    }

    private static async Task<int> InsertIdAsync(MySqlConnection connection, string sql)
        => await connection.QuerySingleAsync<int>($"{sql} SELECT LAST_INSERT_ID();");

    private static async Task EnsureOrderTablesAsync(MySqlConnection connection)
    {
        await connection.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS customers (CustomerID INT AUTO_INCREMENT PRIMARY KEY, Name VARCHAR(150) NOT NULL, IsActive TINYINT(1) NOT NULL) ENGINE=InnoDB;
            CREATE TABLE IF NOT EXISTS employees (EmployeeID INT AUTO_INCREMENT PRIMARY KEY, Name VARCHAR(150) NOT NULL, IsActive TINYINT(1) NOT NULL) ENGINE=InnoDB;
            CREATE TABLE IF NOT EXISTS products (ProductID INT AUTO_INCREMENT PRIMARY KEY, Description VARCHAR(255), IsActive TINYINT(1) NOT NULL, ProductType TINYINT UNSIGNED NOT NULL) ENGINE=InnoDB;
            CREATE TABLE IF NOT EXISTS product_specification_attributes (AttributeID INT AUTO_INCREMENT PRIMARY KEY, ProductCategoryID INT NOT NULL, Name VARCHAR(150) NOT NULL, DataType TINYINT UNSIGNED NOT NULL, IsRequired TINYINT(1) NOT NULL, DisplayOrder INT NOT NULL) ENGINE=InnoDB;
            CREATE TABLE IF NOT EXISTS quotes (QuoteID INT AUTO_INCREMENT PRIMARY KEY, CustomerID INT NOT NULL, CreatedByEmployeeID INT NOT NULL, SourceType TINYINT UNSIGNED NOT NULL, CreatedAt DATETIME NOT NULL) ENGINE=InnoDB;
            CREATE TABLE IF NOT EXISTS quote_sections (QuoteSectionID INT AUTO_INCREMENT PRIMARY KEY, QuoteID INT NOT NULL, SectionType TINYINT UNSIGNED NOT NULL, Status TINYINT UNSIGNED NOT NULL, SentToCustomerAt DATETIME NULL, ApprovedAt DATETIME NULL, CreatedAt DATETIME NOT NULL) ENGINE=InnoDB;
            CREATE TABLE IF NOT EXISTS quote_items (QuoteItemID INT AUTO_INCREMENT PRIMARY KEY, QuoteSectionID INT NOT NULL, ProductID INT NOT NULL, Quantity DECIMAL(12,3) NOT NULL, UnitPrice DECIMAL(12,2), HasInstallationService TINYINT(1) NOT NULL) ENGINE=InnoDB;
            CREATE TABLE IF NOT EXISTS orders (OrderID INT AUTO_INCREMENT PRIMARY KEY, QuoteSectionID INT NOT NULL, CustomerID INT NOT NULL, OrderType TINYINT UNSIGNED NOT NULL, Status TINYINT UNSIGNED NOT NULL, RequiresDownPayment TINYINT(1) NULL, ManufacturingDeadline DATE NULL, InstallationDeadline DATE NULL, CreatedAt DATETIME NOT NULL, UNIQUE KEY (QuoteSectionID)) ENGINE=InnoDB;
            CREATE TABLE IF NOT EXISTS order_items (OrderItemID INT AUTO_INCREMENT PRIMARY KEY, OrderID INT NOT NULL, QuoteItemID INT NOT NULL, ProductID INT NOT NULL, Quantity DECIMAL(12,3) NOT NULL, UnitPrice DECIMAL(12,2) NOT NULL, HasInstallationService TINYINT(1) NOT NULL, SentToProductionAt DATETIME NULL, SentToProductionByEmployeeID INT NULL, CONSTRAINT FK_test_order_items_order FOREIGN KEY (OrderID) REFERENCES orders(OrderID), CONSTRAINT FK_test_order_items_quote FOREIGN KEY (QuoteItemID) REFERENCES quote_items(QuoteItemID), CONSTRAINT FK_test_order_items_product FOREIGN KEY (ProductID) REFERENCES products(ProductID)) ENGINE=InnoDB;
            CREATE TABLE IF NOT EXISTS order_item_specification_values (ValueID INT AUTO_INCREMENT PRIMARY KEY, OrderItemID INT NOT NULL, AttributeID INT NOT NULL, Value VARCHAR(255) NOT NULL, CONSTRAINT FK_test_spec_order FOREIGN KEY (OrderItemID) REFERENCES order_items(OrderItemID)) ENGINE=InnoDB;");
    }

    private static async Task EnsureGoodsReceiptTablesAsync(MySqlConnection connection)
    {
        await connection.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS suppliers (SupplierID INT AUTO_INCREMENT PRIMARY KEY, Name VARCHAR(150) NOT NULL, IsActive TINYINT(1) NOT NULL) ENGINE=InnoDB;
            CREATE TABLE IF NOT EXISTS stock_locations (StockLocationID INT AUTO_INCREMENT PRIMARY KEY, Name VARCHAR(150) NOT NULL, IsActive TINYINT(1) NOT NULL) ENGINE=InnoDB;
            CREATE TABLE IF NOT EXISTS purchase_orders (PurchaseOrderID INT AUTO_INCREMENT PRIMARY KEY, SupplierID INT NOT NULL, OrderDate DATETIME NOT NULL, Status TINYINT UNSIGNED NOT NULL) ENGINE=InnoDB;
            CREATE TABLE IF NOT EXISTS purchase_order_items (PurchaseOrderItemID INT AUTO_INCREMENT PRIMARY KEY, PurchaseOrderID INT NOT NULL, ProductID INT NOT NULL, QuantityOrdered DECIMAL(7,3) NOT NULL, UnitPrice DECIMAL(10,2) NOT NULL, ReceivingMethod TINYINT UNSIGNED NOT NULL, FinalDestination TINYINT UNSIGNED NOT NULL, StockLocationID INT NULL, CustomerID INT NULL) ENGINE=InnoDB;
            CREATE TABLE IF NOT EXISTS goods_receipts (GoodsReceiptID INT AUTO_INCREMENT PRIMARY KEY, PurchaseOrderItemID INT NOT NULL, ReceiptDate DATETIME NOT NULL, QuantityReceived DECIMAL(7,3) NOT NULL, ReceivedByEmployeeID INT NOT NULL, HasDivergence TINYINT(1) NOT NULL, DivergenceNotes VARCHAR(255), Status TINYINT UNSIGNED NOT NULL) ENGINE=InnoDB;
            CREATE TABLE IF NOT EXISTS stock_movements (StockMovementID INT AUTO_INCREMENT PRIMARY KEY, ProductID INT NOT NULL, StockLocationID INT NOT NULL, Quantity DECIMAL(12,3) NOT NULL, MovementType TINYINT UNSIGNED NOT NULL, TransferID CHAR(36) NULL, Reason TINYINT UNSIGNED NULL, Justification VARCHAR(255) NULL, AuthorizedByEmployeeID INT NULL, PerformedByEmployeeID INT NOT NULL, ReviewStatus TINYINT UNSIGNED NULL, ReviewedByEmployeeID INT NULL, ReviewedAt DATETIME NULL, MovementDate DATETIME NOT NULL, Notes VARCHAR(255) NULL, OrderItemID INT NULL, CONSTRAINT FK_test_movement_product FOREIGN KEY (ProductID) REFERENCES products(ProductID), CONSTRAINT FK_test_movement_location FOREIGN KEY (StockLocationID) REFERENCES stock_locations(StockLocationID), CONSTRAINT FK_test_movement_employee FOREIGN KEY (PerformedByEmployeeID) REFERENCES employees(EmployeeID)) ENGINE=InnoDB;
            CREATE TABLE IF NOT EXISTS stock_balances (StockBalanceID INT AUTO_INCREMENT PRIMARY KEY, ProductID INT NOT NULL, StockLocationID INT NOT NULL, Quantity DECIMAL(12,3) NOT NULL, UpdatedAt DATETIME NOT NULL, UNIQUE KEY (ProductID, StockLocationID), CONSTRAINT FK_test_balance_product FOREIGN KEY (ProductID) REFERENCES products(ProductID), CONSTRAINT FK_test_balance_location FOREIGN KEY (StockLocationID) REFERENCES stock_locations(StockLocationID)) ENGINE=InnoDB;");
    }

    private sealed class AllowAuthorizationService : IAuthorizationService
    {
        public bool HasPermission(string permissionCode) => true;
        public bool CanView(string resource) => true;
        public bool CanCreate(string resource) => true;
        public bool CanEdit(string resource) => true;
        public bool CanDelete(string resource) => true;
    }

    private sealed class FixedQuoteRepository(Quote quote) : IQuoteRepository
    {
        public int Save(Quote entity) => 1; public int Delete(int id) => 1; public IEnumerable<Quote> SearchGetBy(string? arg = null) => []; public Task<int> SaveAsync(Quote entity, CancellationToken cancellationToken = default) => Task.FromResult(1); public Task<int> DeleteAsync(int id, CancellationToken cancellationToken = default) => Task.FromResult(1); public Task<IReadOnlyList<Quote>> SearchGetByAsync(string? arg = null, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Quote>>([]);
        public Task<Quote?> GetByIdAsync(int quoteId, CancellationToken cancellationToken = default) => Task.FromResult<Quote?>(quote);
        public Task<Quote?> GetCompleteQuoteAsync(int quoteId, CancellationToken cancellationToken = default) => Task.FromResult<Quote?>(quote);
        public Task<QuoteSection?> GetSectionByItemIdAsync(int quoteItemId, CancellationToken cancellationToken = default) => Task.FromResult<QuoteSection?>(quote.Sections.FirstOrDefault(s => s.Items.Any(i => i.QuoteItemID == quoteItemId)));
        public Task<QuoteSection?> GetSectionByIdAsync(int quoteSectionId, CancellationToken cancellationToken = default) => Task.FromResult<QuoteSection?>(quote.Sections.FirstOrDefault(s => s.QuoteSectionID == quoteSectionId));
        public Task<QuoteItem?> GetItemByIdAsync(int quoteItemId, CancellationToken cancellationToken = default) => Task.FromResult<QuoteItem?>(quote.Sections.SelectMany(s => s.Items).FirstOrDefault(i => i.QuoteItemID == quoteItemId));
        public Task<IEnumerable<QuoteItemSpecificationValue>> GetSpecificationValuesByItemIdAsync(int quoteItemId, CancellationToken cancellationToken = default) => Task.FromResult<IEnumerable<QuoteItemSpecificationValue>>([]);
        public Task<int> SaveSectionAsync(QuoteSection section, CancellationToken cancellationToken = default) => Task.FromResult(1); public Task<int> SaveItemAsync(QuoteItem item, CancellationToken cancellationToken = default) => Task.FromResult(1); public Task<int> SaveSpecificationValueAsync(QuoteItemSpecificationValue value, CancellationToken cancellationToken = default) => Task.FromResult(1);
    }

    private sealed class EmptyProductRepository : IProductRepository
    {
        public int Save(Product entity) => 1; public int Delete(int id) => 1; public IEnumerable<Product> SearchGetBy(string? arg = null) => []; public Task<int> SaveAsync(Product entity, CancellationToken cancellationToken = default) => Task.FromResult(1); public Task<int> DeleteAsync(int id, CancellationToken cancellationToken = default) => Task.FromResult(1); public Task<IReadOnlyList<Product>> SearchGetByAsync(string? arg = null, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Product>>([]); public bool BrandExists(int marcaId) => true; public bool SubgroupExists(int subgroupID) => true; public bool ServiceProductExists(int productId) => true; public bool GoodProductExists(int productId) => true;
    }

    private sealed class EmptyReservationService : IStockReservationService
    {
        public Task<StockReservationDTO> CreateReservationAsync(int orderItemId, int stockLocationId, int createdByEmployeeId, decimal quantity, CancellationToken cancellationToken = default) => throw new NotImplementedException(); public Task ReleaseReservationAsync(int reservationId, int? releasedByEmployeeId = null, CancellationToken cancellationToken = default) => throw new NotImplementedException(); public Task ReleaseActiveReservationsForOrderAsync(int orderId, CancellationToken cancellationToken = default) => throw new NotImplementedException(); public Task<StockReservationDTO> GetByIdAsync(int reservationId, CancellationToken cancellationToken = default) => throw new NotImplementedException(); public Task<IEnumerable<StockReservationDTO>> GetByOrderItemIdAsync(int orderItemId, CancellationToken cancellationToken = default) => throw new NotImplementedException(); public Task<IEnumerable<StockReservationDTO>> GetByOrderIdAsync(int orderId, CancellationToken cancellationToken = default) => throw new NotImplementedException(); public Task<IEnumerable<StockReservationDTO>> GetAllAsync(int page = 1, int pageSize = 100, CancellationToken cancellationToken = default) => throw new NotImplementedException(); public Task<IEnumerable<StockReservationDTO>> SearchAsync(string? searchTerm, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private sealed class EmptyInstallmentService : IOrderInstallmentService
    {
        public Task<OrderInstallmentDTO> GetInstallmentByIdAsync(int installmentId, CancellationToken cancellationToken = default) => throw new NotImplementedException(); public Task<IEnumerable<OrderInstallmentDTO>> GetInstallmentsByOrderIdAsync(int orderId, CancellationToken cancellationToken = default) => throw new NotImplementedException(); public Task<IEnumerable<OrderInstallmentDTO>> CreateInstallmentPlanAsync(int orderId, IEnumerable<InstallmentItemInputDTO> items, CancellationToken cancellationToken = default) => throw new NotImplementedException(); public Task RegisterPaymentAsync(int installmentId, int receivedByEmployeeId, int receivedIntoCashAccountId, CancellationToken cancellationToken = default) => throw new NotImplementedException(); public Task MarkOverdueAsync(int installmentId, CancellationToken cancellationToken = default) => throw new NotImplementedException(); public Task CancelInstallmentAsync(int installmentId, CancellationToken cancellationToken = default) => throw new NotImplementedException(); public Task CancelInstallmentsForOrderAsync(int orderId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private sealed class FixedPurchaseOrderItemRepository(PurchaseOrderItem item) : IPurchaseOrderItemRepository
    {
        public int Save(PurchaseOrderItem entity) => 1; public int Delete(int id) => 1; public IEnumerable<PurchaseOrderItem> SearchGetBy(string? arg = null) => [item]; public Task<int> SaveAsync(PurchaseOrderItem entity, CancellationToken cancellationToken = default) => Task.FromResult(1); public Task<int> DeleteAsync(int id, CancellationToken cancellationToken = default) => Task.FromResult(1); public Task<IReadOnlyList<PurchaseOrderItem>> SearchGetByAsync(string? arg = null, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<PurchaseOrderItem>>([item]); public Task<IEnumerable<PurchaseOrderItem>> GetByPurchaseOrderIdAsync(int purchaseOrderId, CancellationToken cancellationToken = default) => Task.FromResult<IEnumerable<PurchaseOrderItem>>([item]);
    }
}