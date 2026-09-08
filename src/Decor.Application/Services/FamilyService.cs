using Decor.Application.Mappers;
using Decor.Core.DTOs;
using Decor.Core.Interfaces.Repositories;
using Decor.Core.Interfaces.Services;

namespace Decor.Application.Services;

public class FamilyService(IFamilyRepository familyRepository) : IFamilyService
{
    public async Task<IEnumerable<FamilyDTO>> GetByClassIdAsync(int classId, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default)
    {
        var families = await familyRepository.GetByClassIdAsync(classId, page, pageSize, cancellationToken);
        return families.ToDTO();
    }
}