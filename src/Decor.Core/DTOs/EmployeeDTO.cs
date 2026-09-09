using System.ComponentModel.DataAnnotations;

namespace Decor.Core.DTOs;
public record EmployeeDTO([property: Display(Name = "ID do Funcionário")] int EmployeeID,
                          [property: Display(Name = "Nome")] string? Name,
                          [property: Display(Name = "Documento")] string? Document,
                          [property: Display(Name = "Telefone")] string? Phone,
                          [property: Display(Name = "Ativo")] bool IsActive,
                          [property: Display(Name = "ID do Usuário")] int? UserID);
