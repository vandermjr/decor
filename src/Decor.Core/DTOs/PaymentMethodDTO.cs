using System.ComponentModel.DataAnnotations;
using Decor.Core.Entities;

namespace Decor.Core.DTOs;

public record PaymentMethodDTO(
    [property: Display(Name = "ID da Forma de Pagamento")] int PaymentMethodID,
    [property: Display(Name = "Nome")] string Name,
    [property: Display(Name = "Timing")] PaymentTiming Timing,
    [property: Display(Name = "Ativo")] bool IsActive
);
