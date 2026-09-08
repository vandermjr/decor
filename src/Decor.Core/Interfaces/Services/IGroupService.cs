using Decor.Core.DTOs;

namespace Decor.Core.Interfaces.Services;

/// <summary>
/// Serviço para obter os Grupos que pertencem a uma Família específica.
/// </summary>
public interface IGroupService
{
    Task<IEnumerable<GroupDTO>> GetByFamilyIdAsync(int familyId, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default);
}