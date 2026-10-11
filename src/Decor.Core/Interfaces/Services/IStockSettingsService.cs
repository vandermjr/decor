namespace Decor.Core.Interfaces.Services;

public interface IStockSettingsService
{
    Task<bool> GetAllowNegativeStockAsync(CancellationToken cancellationToken = default);
    Task SetAllowNegativeStockAsync(bool allow, CancellationToken cancellationToken = default);
}