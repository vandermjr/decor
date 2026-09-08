using Decor.Core.DTOs;

namespace Decor.Core.Interfaces.Services;

/// <summary>
/// Serviço para obter os Subgrupos que pertencem a um Grupo específico.
/// </summary>
public interface ISubgroupService
{
    Task<IEnumerable<SubgroupDTO>> GetByGroupIdAsync(int groupId, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default);
}