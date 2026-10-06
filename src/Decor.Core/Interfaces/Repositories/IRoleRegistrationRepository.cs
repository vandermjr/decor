namespace Decor.Core.Interfaces.Repositories;

public interface IRoleRegistrationRepository
{
    Task<int> CreateAsync(string name, string? description, int hierarchyLevel, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(int roleId, string name, string? description, int hierarchyLevel, CancellationToken cancellationToken = default);
}