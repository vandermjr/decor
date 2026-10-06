using Decor.Core.Common;
using Decor.Core.DTOs;
using Decor.Core.Interfaces.Repositories;
using Decor.Core.Interfaces.Services;

namespace Decor.Application.Services;

public sealed class RoleRegistrationService(
    IUserAdministrationRepository users,
    IRoleRegistrationRepository repository,
    IAuthenticatedUserContext context,
    IAuthorizationService authorization) : IRoleRegistrationService
{
    public async Task<IReadOnlyList<AdministrativeRoleDTO>> GetRolesAsync(CancellationToken cancellationToken = default)
    {
        Require(DecorPermissions.RolesView);
        var roles = await users.GetRolesAsync(cancellationToken);
        return roles.Select(role => role with { IsSystemProtected = IsProtected(role) }).ToArray();
    }

    public async Task<IReadOnlyList<int>> GetEditableRoleIdsAsync(CancellationToken cancellationToken = default)
    {
        Require(DecorPermissions.RolesView);
        if (!authorization.HasPermission(DecorPermissions.RolesEdit)) return [];
        var roles = await users.GetRolesAsync(cancellationToken);
        var authority = OperatorAuthority(roles);
        return roles.Where(role => !IsProtected(role) && CanManage(role.HierarchyLevel, authority))
            .Select(role => role.RoleID).ToArray();
    }

    public async Task<AdministrativeRoleDTO> SaveAsync(int roleId, string name, string? description, int hierarchyLevel, CancellationToken cancellationToken = default)
    {
        Require(DecorPermissions.RolesEdit);
        if (roleId < 0 || hierarchyLevel < 0) throw new ArgumentException("Identificador ou nível hierárquico inválido.");
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 50)
            throw new ArgumentException("O nome do grupo é obrigatório e deve ter até 50 caracteres.");
        name = name.Trim();
        description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        if (description?.Length > 255) throw new ArgumentException("A descrição deve ter até 255 caracteres.");
        if (IsProtectedName(name)) throw new InvalidOperationException("Este nome está reservado a um grupo protegido do sistema.");

        var roles = await users.GetRolesAsync(cancellationToken);
        var authority = OperatorAuthority(roles);
        if (roleId > 0)
        {
            var target = roles.SingleOrDefault(role => role.RoleID == roleId)
                ?? throw new InvalidOperationException("Grupo não encontrado.");
            if (IsProtected(target)) throw new InvalidOperationException("Grupos protegidos do sistema não podem ser editados.");
            EnsureManage(target.HierarchyLevel, authority);
        }
        EnsureManage(hierarchyLevel, authority);
        if (roles.Any(role => role.RoleID != roleId && string.Equals(role.RoleName.Trim(), name, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Já existe um grupo com este nome.");

        if (roleId == 0)
            roleId = await repository.CreateAsync(name, description, hierarchyLevel, cancellationToken);
        else if (!await repository.UpdateAsync(roleId, name, description, hierarchyLevel, cancellationToken))
            throw new InvalidOperationException("O grupo não foi encontrado ou está protegido.");

        return new AdministrativeRoleDTO(roleId, name, description, hierarchyLevel, false);
    }

    private void Require(string permission)
    {
        if (!context.IsAuthenticated || !authorization.HasPermission(permission))
            throw new UnauthorizedAccessException("Você não possui permissão para esta operação administrativa.");
    }

    private (int Level, bool IsAdministrator) OperatorAuthority(IReadOnlyList<AdministrativeRoleDTO> roles)
    {
        var assigned = roles.Where(role => context.User!.Roles.Contains(role.RoleName, StringComparer.OrdinalIgnoreCase)).ToArray();
        var administrator = SystemAccountDefaults.IsAdministrator(context.User?.Username)
            || assigned.Any(role => role.IsSystemProtected && role.RoleName.Equals(SystemRoleDefaults.Administrators, StringComparison.OrdinalIgnoreCase));
        return (assigned.Select(role => role.HierarchyLevel).DefaultIfEmpty(0).Max(), administrator);
    }

    private static bool CanManage(int level, (int Level, bool IsAdministrator) authority)
        => authority.IsAdministrator || level < authority.Level;

    private static void EnsureManage(int level, (int Level, bool IsAdministrator) authority)
    {
        if (!CanManage(level, authority))
            throw new UnauthorizedAccessException("O nível hierárquico atual e o proposto devem ter valor estritamente menor que o seu maior nível hierárquico. Valores maiores representam maior autoridade.");
    }

    private static bool IsProtected(AdministrativeRoleDTO role) => IsProtectedName(role.RoleName);

    private static bool IsProtectedName(string name)
        => name.Trim().Equals(SystemRoleDefaults.Administrators, StringComparison.OrdinalIgnoreCase)
            || name.Trim().Equals("Administrador", StringComparison.OrdinalIgnoreCase);
}