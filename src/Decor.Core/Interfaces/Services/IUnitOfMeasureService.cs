using Decor.Core.DTOs;

namespace Decor.Core.Interfaces.Services;

public interface IUnitOfMeasureService
{
    Task<IEnumerable<UnitOfMeasureDTO>> GetAllAsync(int page = 1, int pageSize = 100, CancellationToken cancellationToken = default);
    Task<IEnumerable<UnitOfMeasureDTO>> SearchAsync(string searchTerm, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default);
    Task<UnitOfMeasureDTO> GetByIdAsync(int unitOfMeasureId, CancellationToken cancellationToken = default);
    Task SaveUnitOfMeasureAsync(UnitOfMeasureDTO unitOfMeasure, CancellationToken cancellationToken = default);
    Task ActivateAsync(int unitOfMeasureId, CancellationToken cancellationToken = default);
    Task DeactivateAsync(int unitOfMeasureId, CancellationToken cancellationToken = default);
}
