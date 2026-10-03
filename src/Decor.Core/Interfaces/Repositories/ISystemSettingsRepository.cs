namespace Decor.Core.Interfaces.Repositories;

public interface ISystemSettingsRepository
{
    Task<IReadOnlyDictionary<string, string>> GetAllAsync(CancellationToken cancellationToken = default);
    Task SetAsync(IReadOnlyDictionary<string, string> settings, CancellationToken cancellationToken = default);
}