using System.ComponentModel.DataAnnotations;
using Decor.Core.Common.Attributes;
using Decor.Core.Entities;

namespace Decor.Core.DTOs;
public record ProductSpecificationAttributeDTO(
    [property: KeyProperty, Display(Name = "ID do Atributo")] int AttributeID,
    [property: Display(Name = "ID da Categoria")] int ProductCategoryID,
    [property: Display(Name = "Nome")] string? Name,
    [property: Display(Name = "Tipo de Dado")] ProductSpecificationDataType DataType,
    [property: Display(Name = "Unidade")] string? Unit,
    [property: Display(Name = "Opções (Enum)")] string? EnumOptions,
    [property: Display(Name = "Obrigatório")] bool IsRequired,
    [property: Display(Name = "Ordem de Exibição")] int DisplayOrder);
