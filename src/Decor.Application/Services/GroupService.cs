using Decor.Application.Mappers;
using Decor.Core.DTOs;
using Decor.Core.Interfaces.Repositories;
using Decor.Core.Interfaces.Services;

namespace Decor.Application.Services;

public class GroupService(IGroupRepository groupRepository) : IGroupService
{
    public async Task<IEnumerable<GroupDTO>> GetByFamilyIdAsync(int familyId, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default)
    {
        var groups = await groupRepository.GetByFamilyIdAsync(familyId, page, pageSize, cancellationToken);
        return groups.ToDTO();
    }
}