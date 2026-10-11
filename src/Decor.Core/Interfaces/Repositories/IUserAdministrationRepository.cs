using Decor.Core.DTOs;
namespace Decor.Core.Interfaces.Repositories;
public interface IUserAdministrationRepository
{
    Task<IReadOnlyList<AdministrativeUserDTO>> SearchAsync(string? search, CancellationToken cancellationToken = default);
    Task<AdministrativeUserDTO?> GetByIdAsync(int userId, CancellationToken cancellationToken = default);
    Task<int> CreateWithRolesAsync(string username, string displayName, string passwordHash, IReadOnlyCollection<int> roleIds, CancellationToken cancellationToken = default);
    Task<int> CreateWithRolesAndEmployeeAsync(string username, string displayName, string passwordHash,
        IReadOnlyCollection<int> roleIds, int? employeeId, CancellationToken cancellationToken = default)
        => employeeId is null
            ? CreateWithRolesAsync(username, displayName, passwordHash, roleIds, cancellationToken)
            : Task.FromException<int>(new NotSupportedException("A associação de funcionários não está disponível."));
    Task AssignEmployeeAsync(int userId, int? employeeId, CancellationToken cancellationToken = default)
        => Task.FromException(new NotSupportedException("A associação de funcionários não está disponível."));
    Task<bool> UpdateAsync(int userId, string username, string displayName, CancellationToken cancellationToken = default);
    Task<bool> SetActivePreservingLastAdministratorAsync(int userId, bool isActive, int administratorRoleId, CancellationToken cancellationToken = default);
    Task<bool> UpdateTemporaryPasswordAsync(int userId, string passwordHash, CancellationToken cancellationToken = default);
    Task ReplaceRolesPreservingLastAdministratorAsync(int userId, IReadOnlyCollection<int> roleIds, int administratorRoleId, CancellationToken cancellationToken = default);
    Task ReplacePermissionOverridesAsync(int userId, IReadOnlyCollection<PermissionOverrideDTO> overrides, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AdministrativeRoleDTO>> GetRolesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AdministrativePermissionDTO>> GetPermissionsAsync(CancellationToken cancellationToken = default);
}
