using Decor.Core.Entities;

namespace Decor.Core.Interfaces.Repositories;

public interface IFamilyRepository
{
    IEnumerable<Family> GetByClassId(int classId);
    Task<IReadOnlyList<Family>> GetByClassIdAsync(int classId, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default);
}