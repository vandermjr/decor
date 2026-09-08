using Decor.Application.Mappers;
using Decor.Core.DTOs;
using Decor.Core.Interfaces.Repositories;
using Decor.Core.Interfaces.Services;

namespace Decor.Application.Services;

public class SubgroupService(ISubgroupRepository subgroupRepository) : ISubgroupService
{
    public async Task<IEnumerable<SubgroupDTO>> GetByGroupIdAsync(int groupId, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default)
    {
        var subgroups = await subgroupRepository.GetByGroupIdAsync(groupId, page, pageSize, cancellationToken);
        return subgroups.ToDTO();
    }
}