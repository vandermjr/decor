using Decor.Core.DTOs;

namespace Decor.Core.Validation;
public class ProductDTOValidator : IDTOValidator<ProductDTO>
{
    public IEnumerable<string> Validate(ProductDTO dto)
    {
        var errors = new List<string>();

        if (dto.ProductID < 0)
            errors.Add("O ID do Produto não pode ser negativo.");

        if (dto.BrandID is null or <= 0)
            errors.Add("O ID da Marca deve ser maior que zero.");

        if (dto.SubgroupID is null or <= 0)
            errors.Add("O ID do Subgrupo deve ser maior que zero.");

        if (!string.IsNullOrWhiteSpace(dto.Barcode))
        {
            if (dto.Barcode.Length is not 13 and not 8)
            {
                errors.Add("O Código de Barras deve ter 13 ou 8 caracteres.");
            }
        }

        if (string.IsNullOrWhiteSpace(dto.Description))
            errors.Add("A descrição é obrigatória.");
        else if (dto.Description.Length < 3)
            errors.Add("A descrição deve ter no mínimo 3 caracteres.");
        else if (dto.Description.Length > 50)
            errors.Add("A descrição não pode exceder 50 caracteres.");

        if (dto.StockQuantity < 0)
            errors.Add("O Estoque Físico não pode ser negativo.");

        if (dto.MinimumStock < 0)
            errors.Add("O Estoque Mínimo não pode ser negativo.");

        if (dto.MinimumStock > dto.StockQuantity)
            errors.Add("O Estoque Mínimo não pode ser maior que o Estoque Físico.");

        if (dto.BrandID > 0 && string.IsNullOrWhiteSpace(dto.BrandName))
            errors.Add("O nome da Marca é obrigatório quando o ID da Marca é informado.");

        if (!string.IsNullOrWhiteSpace(dto.ManufacturerRef) && dto.ManufacturerRef.Length > 50)
            errors.Add("A referência do Fabricante não pode exceder 50 caracteres.");

        if (!string.IsNullOrWhiteSpace(dto.AuxiliarRef) && dto.AuxiliarRef.Length > 50)
            errors.Add("A referência Auxiliar não pode exceder 50 caracteres.");

        if (!string.IsNullOrWhiteSpace(dto.Dimensions))
        {
            if (!dto.Dimensions.Contains('x', StringComparison.OrdinalIgnoreCase))
            {
                errors.Add("As dimensões devem estar no formato 'LxAxC' (exemplo: 10x20x30).");
            }
        }

        return errors;
    }
}
