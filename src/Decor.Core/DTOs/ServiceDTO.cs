using System.ComponentModel.DataAnnotations;

namespace Decor.Core.DTOs;

public record ServiceDTO(
    [property: Display(Name = "Codigo"), Range(0, int.MaxValue)] int ServiceID,
    [property: Display(Name = "Descricao"), Required, StringLength(255)] string? Description,
    [property: Display(Name = "Ativo")] bool IsActive,
    [property: Display(Name = "Preco de custo"), Range(typeof(decimal), "0", "99999999.99", ParseLimitsInInvariantCulture = true)] decimal? CostPrice,
    [property: Display(Name = "Preco de venda"), Range(typeof(decimal), "0", "99999999.99", ParseLimitsInInvariantCulture = true)] decimal? SalePrice,
    [property: Display(Name = "Comissao do funcionario"), Range(typeof(decimal), "0", "99999999.99", ParseLimitsInInvariantCulture = true)] decimal? EmployeeCommissionValue,
    [property: Display(Name = "Observacoes")] string? Observations);