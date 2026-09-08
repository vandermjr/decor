using Decor.Application.Mappers;
using Decor.Core.DTOs;
using Decor.Core.Interfaces.Repositories;
using Decor.Core.Interfaces.Services;

namespace Decor.Application.Services;

public class ClassService(IClassRepository classRepository) : IClassService
{
    public async Task<IEnumerable<ClassDTO>> GetAllAsync(int page = 1, int pageSize = 100, CancellationToken cancellationToken = default)
    {
        var classes = await classRepository.GetAllAsync(page, pageSize, cancellationToken);
        return classes.ToDTO();
    }
}