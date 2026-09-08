using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Decor.Core.Common.Attributes;

namespace Decor.Core.DTOs;

public record ProductDTO(
    [property: KeyProperty, Display(Name = "ID"), Browsable(true)] int ProductID,
    [property: Display(Name = "Código de Barras"), Browsable(true)] string? Barcode,
    [property: Display(Name = "Ativo"), Browsable(true)] bool IsActive,
    [property: Display(Name = "Descrição"), Browsable(true)] string? Description,
    [property: Display(Name = "Estoque"), Browsable(true)] decimal StockQuantity,
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
    [property: Display(Name = "Estoque Mínimo"), Browsable(false)] decimal MinimumStock
);