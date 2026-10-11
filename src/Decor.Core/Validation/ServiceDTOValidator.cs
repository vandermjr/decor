using System.ComponentModel.DataAnnotations;
using System.Text;
using Decor.Core.DTOs;

namespace Decor.Core.Validation;

public class ServiceDTOValidator : IDTOValidator<ServiceDTO>
{
    public IEnumerable<string> Validate(ServiceDTO dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(dto, new ValidationContext(dto), results, validateAllProperties: true);
        ValidateMoneyPrecision(dto.CostPrice, "Preco de custo", results);
        ValidateMoneyPrecision(dto.SalePrice, "Preco de venda", results);
        ValidateMoneyPrecision(dto.EmployeeCommissionValue, "Comissao do funcionario", results);
        if (dto.Observations is not null && Encoding.UTF8.GetByteCount(dto.Observations) > 65535)
            results.Add(new ValidationResult("Observacoes nao podem exceder 65535 bytes em UTF-8."));
        return results.Select(result => result.ErrorMessage!);
    }

    private static void ValidateMoneyPrecision(decimal? value, string fieldName, List<ValidationResult> results)
    {
        if (value.HasValue && decimal.Round(value.Value, 2) != value.Value)
            results.Add(new ValidationResult($"{fieldName} deve ter no maximo duas casas decimais."));
    }
}