using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Decor.Core.Common.Attributes;
using Decor.Core.Entities;

namespace Decor.Core.DTOs;

public record ProductDTO(
    [property: KeyProperty, Display(Name = "ID"), Browsable(true)] int ProductID,
    [property: Display(Name = "Código de Barras"), Browsable(true)] string? Barcode,
    [property: Display(Name = "Ativo"), Browsable(true)] bool IsActive,
    [property: Display(Name = "Produto"), Browsable(true)] string? ProductName,
    [property: Display(Name = "Estoque Inicial"), Browsable(true)] decimal StockQuantity,
    [property: KeyProperty, Display(Name = "ID"), Browsable(false)] int? BrandID,
    [property: Display(Name = "Marca"), Browsable(true)] string? BrandName,    
    [property: KeyProperty, Display(Name = "ID"), Browsable(false)] int? SubgroupID,
    [property: Display(Name = "Subgrupo"), Browsable(false)] string? SubgroupName,
    [property: KeyProperty, Display(Name = "ID"), Browsable(false)] int? GroupID,
    [property: Display(Name = "Grupo"), Browsable(false)] string? GroupName,
    [property: KeyProperty, Display(Name = "ID"), Browsable(false)] int? FamilyID,
    [property: Display(Name = "Família"), Browsable(false)] string? FamilyName,
    [property: KeyProperty, Display(Name = "ID"), Browsable(false)] int? ClassID,
    [property: Display(Name = "Classe"), Browsable(false)] string? ClassName,
    [property: Display(Name = "Ref. Fabricante"), Browsable(true)] string? ManufacturerRef,
    [property: Display(Name = "Ref. Auxiliar"), Browsable(false)] string? AuxiliarRef,
    [property: Display(Name = "Dimensões"), Browsable(true)] string? Dimensions,
    [property: Display(Name = "Observações"), Browsable(false)] string? Observations,
    [property: Display(Name = "Estoque Mínimo"), Browsable(true)] decimal MinimumStock,
    [property: Display(Name = "Tipo"), Browsable(false)] int ProductType,
    [property: Display(Name = "Preço de Custo"), Browsable(true)] decimal? CostPrice,
    [property: Display(Name = "Preço de Venda"), Browsable(true)] decimal? SalePrice,
    [property: Display(Name = "Comissão do Funcionário"), Browsable(false)] decimal? EmployeeCommissionValue,
    [property: KeyProperty, Display(Name = "ID"), Browsable(false)] int? DefaultInstallationServiceID,
    [property: KeyProperty, Display(Name = "ID da Unidade de Medida"), Browsable(false)] int? StockUnitID,
    [property: Display(Name = "Unidade"), Browsable(true)] string? StockUnitCode = null,
    [property: Display(Name = "Estoque Físico"), Browsable(true)] decimal CurrentStockQuantity = 0,
    [property: Display(Name = "Reservado"), Browsable(true)] decimal ReservedStockQuantity = 0,
    [property: Display(Name = "Modo de Aquisição"), Browsable(false)] ProductAcquisitionMode AcquisitionMode = ProductAcquisitionMode.Estocado
)
{
    [Display(Name = "Disponível"), Browsable(true)]
    public decimal AvailableStockQuantity => CurrentStockQuantity - ReservedStockQuantity;

    [Display(Name = "Situação"), Browsable(true)]
    public string StockStatus => AcquisitionMode == ProductAcquisitionMode.SobEncomenda
        ? "Sob Demanda"
        : CurrentStockQuantity < 0
        ? "Negativo"
        : AvailableStockQuantity <= 0 && MinimumStock > 0
            ? "Repor"
            : AvailableStockQuantity < MinimumStock
                ? "Atenção"
                : "Positivo";
}