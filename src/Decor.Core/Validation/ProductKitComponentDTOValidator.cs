using Decor.Core.DTOs;

namespace Decor.Core.Validation;
public class ProductKitComponentDTOValidator : IDTOValidator<ProductKitComponentDTO>
{
    public IEnumerable<string> Validate(ProductKitComponentDTO dto)
    {
        var errors = new List<string>();

        if (dto.KitProductID <= 0)
            errors.Add("O ID do Produto Kit deve ser maior que zero.");

        if (dto.ComponentProductID <= 0)
            errors.Add("O ID do Produto Componente deve ser maior que zero.");

        if (dto.KitProductID > 0 && dto.KitProductID == dto.ComponentProductID)
            errors.Add("Um produto não pode ser componente de si mesmo.");

        if (dto.Quantity <= 0)
            errors.Add("A Quantidade deve ser maior que zero.");

        return errors;
    }
}
