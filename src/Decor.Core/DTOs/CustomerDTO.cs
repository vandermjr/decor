using System.ComponentModel.DataAnnotations;

namespace Decor.Core.DTOs;
public record CustomerDTO([property: Display(Name = "ID do Cliente")] int CustomerID,
                          [property: Display(Name = "Nome")] string? Name,
                          [property: Display(Name = "Documento")] string? Document,
                          [property: Display(Name = "Telefone")] string? Phone,
                          [property: Display(Name = "E-mail")] string? Email,
                          [property: Display(Name = "Endereço")] string? Address,
                          [property: Display(Name = "Ativo")] bool IsActive);
