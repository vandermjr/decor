using Decor.Core.DTOs;

namespace Decor.Core.Interfaces.Services;

/// <summary>
/// Serviço para obter as Famílias que pertencem a uma Classe específica.
/// </summary>
public interface IFamilyService
{
    Task<IEnumerable<FamilyDTO>> GetByClassIdAsync(int classId, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default);
}