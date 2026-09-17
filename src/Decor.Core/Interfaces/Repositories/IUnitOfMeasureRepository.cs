using Decor.Core.Entities;

namespace Decor.Core.Interfaces.Repositories;

public interface IUnitOfMeasureRepository : IRepository<UnitOfMeasure>
{
    Task<UnitOfMeasure?> GetByIdAsync(int unitOfMeasureId, CancellationToken cancellationToken = default);
    bool CodeExists(string code, int currentUnitOfMeasureId);
    Task<bool> CodeExistsAsync(string code, int currentUnitOfMeasureId, CancellationToken cancellationToken = default);
    int Deactivate(int unitOfMeasureId);
    Task<int> DeactivateAsync(int unitOfMeasureId, CancellationToken cancellationToken = default);
    int Activate(int unitOfMeasureId);
    Task<int> ActivateAsync(int unitOfMeasureId, CancellationToken cancellationToken = default);
}
