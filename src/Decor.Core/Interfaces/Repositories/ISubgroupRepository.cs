using Decor.Core.Entities;

namespace Decor.Core.Interfaces.Repositories
{
    public interface ISubgroupRepository : IRepository<Subgroup>
    {
        /// <summary>
        /// Obtém uma coleção de Subgrupos que pertencem a um Grupo específico.
        /// </summary>
        /// <param name="groupId">O ID do Grupo pai.</param>
        /// <returns>Uma coleção de Subgrupos.</returns>
        IEnumerable<Subgroup> GetByGroupId(int groupId);
        Task<IReadOnlyList<Subgroup>> GetByGroupIdAsync(int groupId, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default);
    }
}