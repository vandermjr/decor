using Decor.Core.Entities;

namespace Decor.Core.Interfaces.Repositories;
public interface IStockBalanceRepository
{
    Task<StockBalance?> GetByProductAndLocationAsync(int productId, int stockLocationId, CancellationToken cancellationToken = default);
    Task<IEnumerable<StockBalance>> GetByProductAsync(int productId, CancellationToken cancellationToken = default);
    Task<IEnumerable<StockBalance>> GetByLocationAsync(int stockLocationId, CancellationToken cancellationToken = default);
}