namespace Decor.Core.Interfaces.Services;

public interface IDatabaseHealthService
{
    string ConnectionDescription { get; }
    Task<bool> IsOnlineAsync(CancellationToken cancellationToken = default);
}