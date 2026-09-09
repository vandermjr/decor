using System.ComponentModel.DataAnnotations;
using Decor.Core.Entities;

namespace Decor.Core.DTOs;
public record PartnerDTO([property: Display(Name = "ID do Parceiro")] int PartnerID,
                         [property: Display(Name = "Nome")] string? Name,
                         [property: Display(Name = "Documento")] string? Document,
                         [property: Display(Name = "Telefone")] string? Phone,
                         [property: Display(Name = "Tipo de Parceiro")] PartnerType PartnerType,
                         [property: Display(Name = "Ativo")] bool IsActive);
