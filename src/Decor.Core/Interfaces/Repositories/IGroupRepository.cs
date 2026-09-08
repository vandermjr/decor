using Decor.Core.Entities;

namespace Decor.Core.Interfaces.Repositories
{
    public interface IGroupRepository : IRepository<Group>
    {
        /// <summary>
        /// Obtém uma coleção de Grupos que pertencem a uma Família específica.
        /// </summary>
        /// <param name="familyId">O ID da Família pai.</param>
        /// <returns>Uma coleção de Grupos.</returns>
        IEnumerable<Group> GetByFamilyId(int familyId);
        Task<IReadOnlyList<Group>> GetByFamilyIdAsync(int familyId, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default);
    }
}