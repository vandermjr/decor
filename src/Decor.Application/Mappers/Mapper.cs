using Decor.Core.DTOs;
using Decor.Core.Entities;

namespace Decor.Application.Mappers;

public static class Mapper
{
    // Mapeia uma coleção de Entidades 'Product' para uma coleção de 'ProductDTO'
    public static IEnumerable<ProductDTO> ToDTO(this IEnumerable<Product> products) => products.Select(p => p.ToDTO());

    // Mapeia uma ÚNICA entidade 'Product' para um 'ProductDTO'
    public static ProductDTO ToDTO(this Product product)
    {
        return new ProductDTO(
            ProductID: product.ProductID,
            Barcode: product.Barcode,
            IsActive: product.IsActive,
            Description: product.Description,
            StockQuantity: product.StockQuantity,
            ManufacturerRef: product.ManufacturerRef,
            AuxiliarRef: product.AuxiliaryRef,
            Dimensions: product.Dimensions,
            Observations: product.Observations,
            MinimumStock: product.MinimumStock,
            BrandID: product.Brand?.BrandID,
            BrandName: product.Brand?.BrandName,
            ClassID: product.Subgroup?.Group?.Family?.Class?.ClassID,
            ClassName: product.Subgroup?.Group?.Family?.Class?.ClassName,
            FamilyID: product.Subgroup?.Group?.Family?.FamilyID,
            FamilyName: product.Subgroup?.Group?.Family?.FamilyName,
            GroupID: product.Subgroup?.Group?.GroupID,
            GroupName: product.Subgroup?.Group?.GroupName,
            SubgroupID: product.Subgroup?.SubgroupID,
            SubgroupName: product.Subgroup?.SubgroupName
        );
    }

    // Mapeia um 'ProductDTO' (da tela) de volta para uma Entidade 'Product'
    public static Product FromDTO(this ProductDTO productDto) => new()
    {

        ProductID = productDto.ProductID,
        Barcode = productDto.Barcode,
        IsActive = productDto.IsActive,
        Description = productDto.Description,
        ManufacturerRef = productDto.ManufacturerRef,
        AuxiliaryRef = productDto.AuxiliarRef,
        Dimensions = productDto.Dimensions,
        Observations = productDto.Observations,
        StockQuantity = productDto.StockQuantity,
        MinimumStock = productDto.MinimumStock,
        BrandID = productDto.BrandID ?? 0,
        SubgroupID = productDto.SubgroupID ?? 0,
        Brand = null,
        Subgroup = null
    };

// --- Mapeadores para Brand ---
public static IEnumerable<BrandDTO> ToDTO(this IEnumerable<Brand> brands) => brands.Select(b => b.ToDTO());
    public static BrandDTO ToDTO(this Brand brand) => new(brand.BrandID, brand.BrandName);
    public static Brand FromDTO(this BrandDTO brandDto) => new() { BrandID = brandDto.BrandID, BrandName = brandDto.BrandName };

    // --- Mapeadores para OccurrenceReason ---
    public static IEnumerable<OccurrenceReasonDTO> ToDTO(this IEnumerable<OccurrenceReason> reasons) => reasons.Select(r => r.ToDTO());
    public static OccurrenceReasonDTO ToDTO(this OccurrenceReason reason) => new(reason.ReasonID, reason.Description, reason.IsActive);
    public static OccurrenceReason FromDTO(this OccurrenceReasonDTO dto) => new() { ReasonID = dto.ReasonID, Description = dto.Description, IsActive = dto.IsActive };

    // --- Mapeadores para OrderOccurrence ---
    public static IEnumerable<OrderOccurrenceDTO> ToDTO(this IEnumerable<OrderOccurrence> occurrences) => occurrences.Select(o => o.ToDTO());
    public static OrderOccurrenceDTO ToDTO(this OrderOccurrence occurrence) => new(
        occurrence.OccurrenceID,
        occurrence.OrderID,
        occurrence.ReasonID,
        occurrence.RegisteredByEmployeeID,
        occurrence.RegisteredAt,
        occurrence.Observation,
        occurrence.NewManufacturingDeadline,
        occurrence.NewInstallationDeadline
    );

    // --- Mapeadores para PaymentMethod ---
    public static IEnumerable<PaymentMethodDTO> ToDTO(this IEnumerable<PaymentMethod> paymentMethods) => paymentMethods.Select(p => p.ToDTO());
    public static PaymentMethodDTO ToDTO(this PaymentMethod paymentMethod) => new(paymentMethod.PaymentMethodID, paymentMethod.Name, paymentMethod.Timing, paymentMethod.IsActive);
    public static PaymentMethod FromDTO(this PaymentMethodDTO dto) => new()
    {
        PaymentMethodID = dto.PaymentMethodID,
        Name = dto.Name,
        Timing = dto.Timing,
        IsActive = dto.IsActive
    };

    // --- Mapeadores para OrderInstallment ---
    public static IEnumerable<OrderInstallmentDTO> ToDTO(this IEnumerable<OrderInstallment> installments) => installments.Select(i => i.ToDTO());
    public static OrderInstallmentDTO ToDTO(this OrderInstallment installment) => new(
        installment.InstallmentID,
        installment.OrderID,
        installment.PaymentMethodID,
        installment.InstallmentNumber,
        installment.Amount,
        installment.DueDate,
        installment.Status,
        installment.PaidAt,
        installment.ReceivedByEmployeeID
    );
    public static OrderInstallment FromDTO(this OrderInstallmentDTO dto) => new()
    {
        InstallmentID = dto.InstallmentID,
        OrderID = dto.OrderID,
        PaymentMethodID = dto.PaymentMethodID,
        InstallmentNumber = dto.InstallmentNumber,
        Amount = dto.Amount,
        DueDate = dto.DueDate,
        Status = dto.Status,
        PaidAt = dto.PaidAt,
        ReceivedByEmployeeID = dto.ReceivedByEmployeeID
    };

    // --- Mapeadores para ProductSpecificationAttribute ---
    public static IEnumerable<ProductSpecificationAttributeDTO> ToDTO(this IEnumerable<ProductSpecificationAttribute> attributes) => attributes.Select(a => a.ToDTO());
    public static ProductSpecificationAttributeDTO ToDTO(this ProductSpecificationAttribute attribute) => new(attribute.AttributeID, attribute.ProductCategoryID, attribute.Name, attribute.DataType, attribute.Unit, attribute.EnumOptions, attribute.IsRequired, attribute.DisplayOrder);
    public static ProductSpecificationAttribute FromDTO(this ProductSpecificationAttributeDTO attributeDto) => new() { AttributeID = attributeDto.AttributeID, ProductCategoryID = attributeDto.ProductCategoryID, Name = attributeDto.Name, DataType = attributeDto.DataType, Unit = attributeDto.Unit, EnumOptions = attributeDto.EnumOptions, IsRequired = attributeDto.IsRequired, DisplayOrder = attributeDto.DisplayOrder };

    // --- Mapeadores para Quote ---
    public static IEnumerable<QuoteDTO> ToDTO(this IEnumerable<Quote> quotes) => quotes.Select(q => q.ToDTO());
    public static QuoteDTO ToDTO(this Quote quote) => new(quote.QuoteID, quote.CustomerID, quote.CreatedByEmployeeID, quote.SourcePartnerID, (int)quote.SourceType, quote.CreatedAt, quote.Notes);
    public static Quote FromDTO(this QuoteDTO quoteDto) => new() { QuoteID = quoteDto.QuoteID, CustomerID = quoteDto.CustomerID, CreatedByEmployeeID = quoteDto.CreatedByEmployeeID, SourcePartnerID = quoteDto.SourcePartnerID, SourceType = (QuoteSourceType)quoteDto.SourceType, CreatedAt = quoteDto.CreatedAt, Notes = quoteDto.Notes };

    public static IEnumerable<QuoteSectionDTO> ToDTO(this IEnumerable<QuoteSection> sections) => sections.Select(s => s.ToDTO());
    public static QuoteSectionDTO ToDTO(this QuoteSection section) => new(section.QuoteSectionID, section.QuoteID, (int)section.SectionType, (int)section.Status, section.SentToCustomerAt, section.ApprovedAt, section.CreatedAt);
    public static QuoteSection FromDTO(this QuoteSectionDTO sectionDto) => new() { QuoteSectionID = sectionDto.QuoteSectionID, QuoteID = sectionDto.QuoteID, SectionType = (QuoteSectionType)sectionDto.SectionType, Status = (QuoteSectionStatus)sectionDto.Status, SentToCustomerAt = sectionDto.SentToCustomerAt, ApprovedAt = sectionDto.ApprovedAt, CreatedAt = sectionDto.CreatedAt };

    public static IEnumerable<QuoteItemDTO> ToDTO(this IEnumerable<QuoteItem> items) => items.Select(i => i.ToDTO());
    public static QuoteItemDTO ToDTO(this QuoteItem item) => new(item.QuoteItemID, item.QuoteSectionID, item.ProductID, item.Quantity, item.UnitPrice, item.HasInstallationService);
    public static QuoteItem FromDTO(this QuoteItemDTO itemDto) => new() { QuoteItemID = itemDto.QuoteItemID, QuoteSectionID = itemDto.QuoteSectionID, ProductID = itemDto.ProductID, Quantity = itemDto.Quantity, UnitPrice = itemDto.UnitPrice, HasInstallationService = itemDto.HasInstallationService };

    public static IEnumerable<QuoteItemSpecificationValueDTO> ToDTO(this IEnumerable<QuoteItemSpecificationValue> values) => values.Select(v => v.ToDTO());
    public static QuoteItemSpecificationValueDTO ToDTO(this QuoteItemSpecificationValue value) => new(value.ValueID, value.QuoteItemID, value.AttributeID, value.Value);
    public static QuoteItemSpecificationValue FromDTO(this QuoteItemSpecificationValueDTO valueDto) => new() { ValueID = valueDto.ValueID, QuoteItemID = valueDto.QuoteItemID, AttributeID = valueDto.AttributeID, Value = valueDto.Value };

    // --- Mapeadores para TailorQuotationRequest ---
    public static IEnumerable<TailorQuotationRequestDTO> ToDTO(this IEnumerable<TailorQuotationRequest> requests) => requests.Select(r => r.ToDTO());
    public static TailorQuotationRequestDTO ToDTO(this TailorQuotationRequest request) => new(
        request.RequestID,
        request.QuoteItemID,
        request.PartnerID,
        request.RequestedByEmployeeID,
        request.RequestedAt,
        request.Deadline,
        (int)request.Status
    );
    public static TailorQuotationRequest FromDTO(this TailorQuotationRequestDTO requestDto) => new()
    {
        RequestID = requestDto.RequestID,
        QuoteItemID = requestDto.QuoteItemID,
        PartnerID = requestDto.PartnerID,
        RequestedByEmployeeID = requestDto.RequestedByEmployeeID,
        RequestedAt = requestDto.RequestedAt,
        Deadline = requestDto.Deadline,
        Status = (TailorQuotationRequestStatus)requestDto.Status
    };

    // --- Mapeadores para TailorQuotationRevision ---
    public static IEnumerable<TailorQuotationRevisionDTO> ToDTO(this IEnumerable<TailorQuotationRevision> revisions) => revisions.Select(r => r.ToDTO());
    public static TailorQuotationRevisionDTO ToDTO(this TailorQuotationRevision revision) => new(
        revision.RevisionID,
        revision.RequestID,
        revision.RevisionNumber,
        revision.Price,
        revision.ChangeReason,
        revision.RespondedAt,
        revision.RegisteredByEmployeeID
    );
    public static TailorQuotationRevision FromDTO(this TailorQuotationRevisionDTO revisionDto) => new()
    {
        RevisionID = revisionDto.RevisionID,
        RequestID = revisionDto.RequestID,
        RevisionNumber = revisionDto.RevisionNumber,
        Price = revisionDto.Price,
        ChangeReason = revisionDto.ChangeReason,
        RespondedAt = revisionDto.RespondedAt,
        RegisteredByEmployeeID = revisionDto.RegisteredByEmployeeID
    };

    // --- Mapeadores para Classificação ---
    public static IEnumerable<ClassDTO> ToDTO(this IEnumerable<Class> classes) => classes.Select(c => c.ToDTO());
    public static ClassDTO ToDTO(this Class @class) => new(@class.ClassID, @class.ClassName);

    public static IEnumerable<FamilyDTO> ToDTO(this IEnumerable<Family> families) => families.Select(f => f.ToDTO());
    public static FamilyDTO ToDTO(this Family family) => new(family.FamilyID, family.FamilyName);

    public static IEnumerable<GroupDTO> ToDTO(this IEnumerable<Group> groups) => groups.Select(g => g.ToDTO());
    public static GroupDTO ToDTO(this Group group) => new(group.GroupID, group.GroupName);

    public static IEnumerable<SubgroupDTO> ToDTO(this IEnumerable<Subgroup> subgroups) => subgroups.Select(s => s.ToDTO());
    public static SubgroupDTO ToDTO(this Subgroup subgroup) => new(subgroup.SubgroupID, subgroup.SubgroupName);

    // --- Mapeadores para Customer ---
    public static IEnumerable<CustomerDTO> ToDTO(this IEnumerable<Customer> customers) => customers.Select(c => c.ToDTO());
    public static CustomerDTO ToDTO(this Customer customer) => new(customer.CustomerID, customer.Name, customer.Document, customer.Phone, customer.Email, customer.Address, customer.IsActive);
    public static Customer FromDTO(this CustomerDTO customerDto) => new() { CustomerID = customerDto.CustomerID, Name = customerDto.Name ?? string.Empty, Document = customerDto.Document, Phone = customerDto.Phone, Email = customerDto.Email, Address = customerDto.Address, IsActive = customerDto.IsActive };

    // --- Mapeadores para Supplier ---
    public static IEnumerable<SupplierDTO> ToDTO(this IEnumerable<Supplier> suppliers) => suppliers.Select(s => s.ToDTO());
    public static SupplierDTO ToDTO(this Supplier supplier) => new(supplier.SupplierID, supplier.CorporateName, supplier.Document, supplier.Phone, supplier.Email, supplier.IsActive);
    public static Supplier FromDTO(this SupplierDTO supplierDto) => new() { SupplierID = supplierDto.SupplierID, CorporateName = supplierDto.CorporateName ?? string.Empty, Document = supplierDto.Document, Phone = supplierDto.Phone, Email = supplierDto.Email, IsActive = supplierDto.IsActive };

    // --- Mapeadores para Employee ---
    public static IEnumerable<EmployeeDTO> ToDTO(this IEnumerable<Employee> employees) => employees.Select(e => e.ToDTO());
    public static EmployeeDTO ToDTO(this Employee employee) => new(employee.EmployeeID, employee.Name, employee.Document, employee.Phone, employee.IsActive, employee.UserID);
    public static Employee FromDTO(this EmployeeDTO employeeDto) => new() { EmployeeID = employeeDto.EmployeeID, Name = employeeDto.Name ?? string.Empty, Document = employeeDto.Document, Phone = employeeDto.Phone, IsActive = employeeDto.IsActive, UserID = employeeDto.UserID };

    // --- Mapeadores para Partner ---
    public static IEnumerable<PartnerDTO> ToDTO(this IEnumerable<Partner> partners) => partners.Select(p => p.ToDTO());
    public static PartnerDTO ToDTO(this Partner partner) => new(partner.PartnerID, partner.Name, partner.Document, partner.Phone, partner.PartnerType, partner.IsActive);
    public static Partner FromDTO(this PartnerDTO partnerDto) => new() { PartnerID = partnerDto.PartnerID, Name = partnerDto.Name ?? string.Empty, Document = partnerDto.Document, Phone = partnerDto.Phone, PartnerType = partnerDto.PartnerType, IsActive = partnerDto.IsActive };

    // --- Mapeadores para StockLocation ---
    public static IEnumerable<StockLocationDTO> ToDTO(this IEnumerable<StockLocation> stockLocations) => stockLocations.Select(sl => sl.ToDTO());
    public static StockLocationDTO ToDTO(this StockLocation stockLocation) => new(stockLocation.StockLocationID, stockLocation.Name, stockLocation.LocationType, stockLocation.PartnerID, stockLocation.IsActive);
    public static StockLocation FromDTO(this StockLocationDTO stockLocationDto) => new() { StockLocationID = stockLocationDto.StockLocationID, Name = stockLocationDto.Name ?? string.Empty, LocationType = stockLocationDto.LocationType, PartnerID = stockLocationDto.PartnerID, IsActive = stockLocationDto.IsActive };

    // --- Mapeadores para StockMovement ---
    public static IEnumerable<StockMovementDTO> ToDTO(this IEnumerable<StockMovement> stockMovements) => stockMovements.Select(sm => sm.ToDTO());
    public static StockMovementDTO ToDTO(this StockMovement stockMovement) => new(stockMovement.StockMovementID, stockMovement.ProductID, stockMovement.StockLocationID, stockMovement.Quantity, stockMovement.MovementType, stockMovement.TransferID, stockMovement.Reason, stockMovement.Justification, stockMovement.AuthorizedByEmployeeID, stockMovement.PerformedByEmployeeID, stockMovement.ReviewStatus, stockMovement.ReviewedByEmployeeID, stockMovement.ReviewedAt, stockMovement.MovementDate, stockMovement.Notes, stockMovement.OrderItemID);
    public static StockMovement FromDTO(this StockMovementDTO stockMovementDto) => new() { StockMovementID = stockMovementDto.StockMovementID, ProductID = stockMovementDto.ProductID, StockLocationID = stockMovementDto.StockLocationID, Quantity = stockMovementDto.Quantity, MovementType = stockMovementDto.MovementType, TransferID = stockMovementDto.TransferID, Reason = stockMovementDto.Reason, Justification = stockMovementDto.Justification, AuthorizedByEmployeeID = stockMovementDto.AuthorizedByEmployeeID, PerformedByEmployeeID = stockMovementDto.PerformedByEmployeeID, ReviewStatus = stockMovementDto.ReviewStatus, ReviewedByEmployeeID = stockMovementDto.ReviewedByEmployeeID, ReviewedAt = stockMovementDto.ReviewedAt, MovementDate = stockMovementDto.MovementDate, Notes = stockMovementDto.Notes, OrderItemID = stockMovementDto.OrderItemID };

    // --- Mapeadores para StockBalance ---
    public static IEnumerable<StockBalanceDTO> ToDTO(this IEnumerable<StockBalance> stockBalances) => stockBalances.Select(sb => sb.ToDTO());
    public static StockBalanceDTO ToDTO(this StockBalance stockBalance) => new(stockBalance.StockBalanceID, stockBalance.ProductID, stockBalance.StockLocationID, stockBalance.Quantity, stockBalance.UpdatedAt);
    public static StockBalance FromDTO(this StockBalanceDTO stockBalanceDto) => new() { StockBalanceID = stockBalanceDto.StockBalanceID, ProductID = stockBalanceDto.ProductID, StockLocationID = stockBalanceDto.StockLocationID, Quantity = stockBalanceDto.Quantity, UpdatedAt = stockBalanceDto.UpdatedAt };

    // --- Mapeadores para PurchaseOrder ---
    public static IEnumerable<PurchaseOrderDTO> ToDTO(this IEnumerable<PurchaseOrder> purchaseOrders) => purchaseOrders.Select(po => po.ToDTO());
    public static PurchaseOrderDTO ToDTO(this PurchaseOrder purchaseOrder) => new(purchaseOrder.PurchaseOrderID, purchaseOrder.SupplierID, purchaseOrder.OrderDate, purchaseOrder.Status, purchaseOrder.Notes);
    public static PurchaseOrder FromDTO(this PurchaseOrderDTO purchaseOrderDto) => new() { PurchaseOrderID = purchaseOrderDto.PurchaseOrderID, SupplierID = purchaseOrderDto.SupplierID, OrderDate = purchaseOrderDto.OrderDate, Status = purchaseOrderDto.Status, Notes = purchaseOrderDto.Notes };

    // --- Mapeadores para PurchaseOrderItem ---
    public static IEnumerable<PurchaseOrderItemDTO> ToDTO(this IEnumerable<PurchaseOrderItem> purchaseOrderItems) => purchaseOrderItems.Select(poi => poi.ToDTO());
    public static PurchaseOrderItemDTO ToDTO(this PurchaseOrderItem purchaseOrderItem) => new(purchaseOrderItem.PurchaseOrderItemID, purchaseOrderItem.PurchaseOrderID, purchaseOrderItem.ProductID, purchaseOrderItem.QuantityOrdered, purchaseOrderItem.UnitPrice, purchaseOrderItem.ReceivingMethod, purchaseOrderItem.FinalDestination, purchaseOrderItem.StockLocationID, purchaseOrderItem.CustomerID);
    public static PurchaseOrderItem FromDTO(this PurchaseOrderItemDTO purchaseOrderItemDto) => new() { PurchaseOrderItemID = purchaseOrderItemDto.PurchaseOrderItemID, PurchaseOrderID = purchaseOrderItemDto.PurchaseOrderID, ProductID = purchaseOrderItemDto.ProductID, QuantityOrdered = purchaseOrderItemDto.QuantityOrdered, UnitPrice = purchaseOrderItemDto.UnitPrice, ReceivingMethod = purchaseOrderItemDto.ReceivingMethod, FinalDestination = purchaseOrderItemDto.FinalDestination, StockLocationID = purchaseOrderItemDto.StockLocationID, CustomerID = purchaseOrderItemDto.CustomerID };

    // --- Mapeadores para GoodsReceipt ---
    public static IEnumerable<GoodsReceiptDTO> ToDTO(this IEnumerable<GoodsReceipt> goodsReceipts) => goodsReceipts.Select(gr => gr.ToDTO());
    public static GoodsReceiptDTO ToDTO(this GoodsReceipt goodsReceipt) => new(goodsReceipt.GoodsReceiptID, goodsReceipt.PurchaseOrderItemID, goodsReceipt.ReceiptDate, goodsReceipt.QuantityReceived, goodsReceipt.ReceivedByEmployeeID, goodsReceipt.HasDivergence, goodsReceipt.DivergenceNotes, goodsReceipt.Status);
    public static GoodsReceipt FromDTO(this GoodsReceiptDTO goodsReceiptDto) => new() { GoodsReceiptID = goodsReceiptDto.GoodsReceiptID, PurchaseOrderItemID = goodsReceiptDto.PurchaseOrderItemID, ReceiptDate = goodsReceiptDto.ReceiptDate, QuantityReceived = goodsReceiptDto.QuantityReceived, ReceivedByEmployeeID = goodsReceiptDto.ReceivedByEmployeeID, HasDivergence = goodsReceiptDto.HasDivergence, DivergenceNotes = goodsReceiptDto.DivergenceNotes, Status = goodsReceiptDto.Status };

    // --- Mapeadores para Order ---
    public static IEnumerable<OrderDTO> ToDTO(this IEnumerable<Order> orders) => orders.Select(o => o.ToDTO());
    public static OrderDTO ToDTO(this Order order) => new(
        order.OrderID,
        order.QuoteSectionID,
        order.CustomerID,
        (int)order.OrderType,
        (int)order.Status,
        order.RequiresDownPayment,
        order.ManufacturingDeadline,
        order.InstallationDeadline,
        order.CreatedAt,
        order.Items?.Select(i => i.ToDTO()).ToList()
    );
    public static Order FromDTO(this OrderDTO orderDto) => new()
    {
        OrderID = orderDto.OrderID,
        QuoteSectionID = orderDto.QuoteSectionID,
        CustomerID = orderDto.CustomerID,
        OrderType = (OrderType)orderDto.OrderType,
        Status = (OrderStatus)orderDto.Status,
        RequiresDownPayment = orderDto.RequiresDownPayment,
        ManufacturingDeadline = orderDto.ManufacturingDeadline,
        InstallationDeadline = orderDto.InstallationDeadline,
        CreatedAt = orderDto.CreatedAt,
        Items = orderDto.Items?.Select(i => i.FromDTO()).ToList() ?? new List<OrderItem>()
    };

    public static IEnumerable<OrderItemDTO> ToDTO(this IEnumerable<OrderItem> items) => items.Select(i => i.ToDTO());
    public static OrderItemDTO ToDTO(this OrderItem item) => new(
        item.OrderItemID,
        item.OrderID,
        item.QuoteItemID,
        item.ProductID,
        item.Quantity,
        item.UnitPrice,
        item.HasInstallationService,
        item.SentToProductionAt,
        item.SentToProductionByEmployeeID,
        item.SpecificationValues?.Select(v => v.ToDTO()).ToList()
    );
    public static OrderItem FromDTO(this OrderItemDTO itemDto) => new()
    {
        OrderItemID = itemDto.OrderItemID,
        OrderID = itemDto.OrderID,
        QuoteItemID = itemDto.QuoteItemID,
        ProductID = itemDto.ProductID,
        Quantity = itemDto.Quantity,
        UnitPrice = itemDto.UnitPrice,
        HasInstallationService = itemDto.HasInstallationService,
        SentToProductionAt = itemDto.SentToProductionAt,
        SentToProductionByEmployeeID = itemDto.SentToProductionByEmployeeID,
        SpecificationValues = itemDto.SpecificationValues?.Select(v => v.FromDTO()).ToList() ?? new List<OrderItemSpecificationValue>()
    };

    public static IEnumerable<OrderItemSpecificationValueDTO> ToDTO(this IEnumerable<OrderItemSpecificationValue> values) => values.Select(v => v.ToDTO());
    public static OrderItemSpecificationValueDTO ToDTO(this OrderItemSpecificationValue value) => new(
        value.ValueID,
        value.OrderItemID,
        value.AttributeID,
        value.Value
    );
    public static OrderItemSpecificationValue FromDTO(this OrderItemSpecificationValueDTO valueDto) => new()
    {
        ValueID = valueDto.ValueID,
        OrderItemID = valueDto.OrderItemID,
        AttributeID = valueDto.AttributeID,
        Value = valueDto.Value
    };

    // --- Mapeadores para StockReservation ---
    public static IEnumerable<StockReservationDTO> ToDTO(this IEnumerable<StockReservation> reservations) => reservations.Select(r => r.ToDTO());
    public static StockReservationDTO ToDTO(this StockReservation reservation) => new(
        reservation.ReservationID,
        reservation.OrderItemID,
        reservation.ProductID,
        reservation.StockLocationID,
        reservation.Quantity,
        reservation.Status,
        reservation.CreatedByEmployeeID,
        reservation.CreatedAt,
        reservation.ReleasedAt
    );
    public static StockReservation FromDTO(this StockReservationDTO reservationDto) => new()
    {
        ReservationID = reservationDto.ReservationID,
        OrderItemID = reservationDto.OrderItemID,
        ProductID = reservationDto.ProductID,
        StockLocationID = reservationDto.StockLocationID,
        Quantity = reservationDto.Quantity,
        Status = reservationDto.Status,
        CreatedByEmployeeID = reservationDto.CreatedByEmployeeID,
        CreatedAt = reservationDto.CreatedAt,
        ReleasedAt = reservationDto.ReleasedAt
    };
}