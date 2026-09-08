namespace Decor.Core.Interfaces.Repositories;
public interface IRepository<TEntity>
{
    int Save(TEntity entity);
    int Delete(int id);
    IEnumerable<TEntity> SearchGetBy(string? arg = null);
    Task<int> SaveAsync(TEntity entity, CancellationToken cancellationToken = default);
    Task<int> DeleteAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TEntity>> SearchGetByAsync(string? arg = null, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default);
}
