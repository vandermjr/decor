using Decor.Core.DTOs;

namespace Decor.Core.Interfaces.Services;

/// <summary>
/// Serviço para obter os dados do nível mais alto da classificação.
/// </summary>
public interface IClassService
{
    Task<IEnumerable<ClassDTO>> GetAllAsync(int page = 1, int pageSize = 100, CancellationToken cancellationToken = default);
}