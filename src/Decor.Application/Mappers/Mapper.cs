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
    public static StockMovementDTO ToDTO(this StockMovement stockMovement) => new(stockMovement.StockMovementID, stockMovement.ProductID, stockMovement.StockLocationID, stockMovement.Quantity, stockMovement.MovementType, stockMovement.TransferID, stockMovement.Reason, stockMovement.Justification, stockMovement.AuthorizedByEmployeeID, stockMovement.PerformedByEmployeeID, stockMovement.ReviewStatus, stockMovement.ReviewedByEmployeeID, stockMovement.ReviewedAt, stockMovement.MovementDate, stockMovement.Notes);
    public static StockMovement FromDTO(this StockMovementDTO stockMovementDto) => new() { StockMovementID = stockMovementDto.StockMovementID, ProductID = stockMovementDto.ProductID, StockLocationID = stockMovementDto.StockLocationID, Quantity = stockMovementDto.Quantity, MovementType = stockMovementDto.MovementType, TransferID = stockMovementDto.TransferID, Reason = stockMovementDto.Reason, Justification = stockMovementDto.Justification, AuthorizedByEmployeeID = stockMovementDto.AuthorizedByEmployeeID, PerformedByEmployeeID = stockMovementDto.PerformedByEmployeeID, ReviewStatus = stockMovementDto.ReviewStatus, ReviewedByEmployeeID = stockMovementDto.ReviewedByEmployeeID, ReviewedAt = stockMovementDto.ReviewedAt, MovementDate = stockMovementDto.MovementDate, Notes = stockMovementDto.Notes };

    // --- Mapeadores para StockBalance ---
    public static IEnumerable<StockBalanceDTO> ToDTO(this IEnumerable<StockBalance> stockBalances) => stockBalances.Select(sb => sb.ToDTO());
    public static StockBalanceDTO ToDTO(this StockBalance stockBalance) => new(stockBalance.StockBalanceID, stockBalance.ProductID, stockBalance.StockLocationID, stockBalance.Quantity, stockBalance.UpdatedAt);
    public static StockBalance FromDTO(this StockBalanceDTO stockBalanceDto) => new() { StockBalanceID = stockBalanceDto.StockBalanceID, ProductID = stockBalanceDto.ProductID, StockLocationID = stockBalanceDto.StockLocationID, Quantity = stockBalanceDto.Quantity, UpdatedAt = stockBalanceDto.UpdatedAt };
}