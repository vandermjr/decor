using Decor.Core.Entities;

namespace Decor.Core.Interfaces.Repositories;

public interface IClassRepository : IRepository<Class>
{
    /// <summary>
    /// Obtém todas as Classes cadastradas, ordenadas por nome.
    /// </summary>
    /// <returns>Uma coleção de Classes.</returns>
    IEnumerable<Class> GetAll();
    Task<IReadOnlyList<Class>> GetAllAsync(int page = 1, int pageSize = 100, CancellationToken cancellationToken = default);
}