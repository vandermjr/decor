using Decor.Core.Interfaces.Repositories;
using Decor.Core.Interfaces.Services;

namespace Decor.Application.Services;

public sealed class StockSettingsService(ISystemSettingsRepository systemSettingsRepository) : IStockSettingsService
{
    private const string AllowNegativeStockKey = "AllowNegativeStock";

    public async Task<bool> GetAllowNegativeStockAsync(CancellationToken cancellationToken = default)
    {
        await systemSettingsRepository.InitializeAsync(cancellationToken);
        var settings = await systemSettingsRepository.GetAllAsync(cancellationToken);
        return settings.TryGetValue(AllowNegativeStockKey, out var value)
            && bool.TryParse(value, out var allow)
            && allow;
    }

    public Task SetAllowNegativeStockAsync(bool allow, CancellationToken cancellationToken = default) =>
        systemSettingsRepository.SetAsync(
            new Dictionary<string, string>(StringComparer.Ordinal) { [AllowNegativeStockKey] = allow.ToString() },
            cancellationToken);
}