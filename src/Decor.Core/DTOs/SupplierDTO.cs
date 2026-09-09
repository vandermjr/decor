using System.ComponentModel.DataAnnotations;

namespace Decor.Core.DTOs;
public record SupplierDTO([property: Display(Name = "ID do Fornecedor")] int SupplierID,
                          [property: Display(Name = "Razão Social")] string? CorporateName,
                          [property: Display(Name = "Documento")] string? Document,
                          [property: Display(Name = "Telefone")] string? Phone,
                          [property: Display(Name = "E-mail")] string? Email,
                          [property: Display(Name = "Ativo")] bool IsActive);
