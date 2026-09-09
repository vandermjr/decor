using Decor.Core.DTOs;

namespace Decor.Core.Interfaces.Services;

public interface IStockLocationService
{
    Task<StockLocationDTO> GetStockLocationByIdAsync(int stockLocationId, CancellationToken cancellationToken = default);
    Task<IEnumerable<StockLocationDTO>> GetAllStockLocationsAsync(int page = 1, int pageSize = 100, CancellationToken cancellationToken = default);
    Task<IEnumerable<StockLocationDTO>> SearchStockLocationsAsync(string searchTerm, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default);
    Task SaveStockLocationAsync(StockLocationDTO stockLocation, CancellationToken cancellationToken = default);
    Task DeleteStockLocationAsync(int stockLocationId, CancellationToken cancellationToken = default);
}
