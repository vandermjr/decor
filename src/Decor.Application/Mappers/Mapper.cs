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
}