using Dapper;
using Decor.Core.DTOs;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Data;
using Decor.Core.Interfaces.Repositories;
using Decor.FluentSqlBuilder;

namespace Decor.Infrastructure.Data.Repositories;

public sealed class UserAdministrationRepository(IDatabaseConnection databaseConnection, Func<FluentCommandBuilder> createCommandBuilder) : IUserAdministrationRepository
{
    public async Task<IReadOnlyList<AdministrativeUserDTO>> SearchAsync(string? search, CancellationToken cancellationToken = default)
    {
        var normalized = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        var query = createCommandBuilder()
            .Select(s => s.Columns<ApplicationUser>(u => u.UserID))
            .From<ApplicationUser>();

        if (normalized is not null)
        {
            query = query.Where(w =>
            {
                w.Contains((ApplicationUser u) => u.Username, normalized);
                w.Or();
                w.Contains((ApplicationUser u) => u.DisplayName, normalized);
            });
        }

        var (sql, parameters) = query.OrderBy("au.Username ASC").Build();

        using var connection = databaseConnection.CreateConnection();
        var ids = await connection.QueryAsync<int>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        var users = new List<AdministrativeUserDTO>();
        foreach (var id in ids) { var user = await GetByIdAsync(id, cancellationToken); if (user is not null) users.Add(user); }
        return users;
    }

    public async Task<AdministrativeUserDTO?> GetByIdAsync(int userId, CancellationToken cancellationToken = default)
    {
        // Um único lote (Batch) mantém os três result sets consistentes numa única ida ao banco.
        var (sql, parameters) = createCommandBuilder()
            .Batch()
            .Select(s => s
                .Select(sc => sc.Columns<ApplicationUser>(u => u.UserID, u => u.Username, u => u.DisplayName, u => u.IsActive))
                .From<ApplicationUser>()
                .Where(w => w.Equals((ApplicationUser u) => u.UserID, userId)))
            .Select(s => s
                .Select(sc => sc.Columns<Role>(r => r.RoleID, r => r.RoleName, r => r.Description, r => r.HierarchyLevel, r => r.IsSystemProtected))
                .From<Role>()
                .Join(j => j.Inner<Role, UserRole>((r, ur) => r.RoleID == ur.RoleID))
                .Where(w => w.Equals((UserRole ur) => ur.UserID, userId))
                .OrderBy("r.HierarchyLevel DESC, r.RoleName ASC"))
            .Select(s => s
                .Select(sc =>
                {
                    sc.Columns<Permission>(p => p.PermissionID, p => p.PermissionCode);
                    sc.Columns<UserPermissionOverride>(o => o.IsGranted);
                })
                .From<UserPermissionOverride>()
                .Join(j => j.Inner<UserPermissionOverride, Permission>((o, p) => o.PermissionID == p.PermissionID))
                .Where(w => w.Equals((UserPermissionOverride o) => o.UserID, userId))
                .OrderBy("p.PermissionID ASC"))
            .Build();

        using var connection = databaseConnection.CreateConnection();
        using var results = await connection.QueryMultipleAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        var user = await results.ReadSingleOrDefaultAsync<ApplicationUser>();
        if (user is null) return null;
        var roles = (await results.ReadAsync<AdministrativeRoleDTO>()).ToArray();
        var overrides = (await results.ReadAsync<PermissionOverrideDTO>()).ToArray();
        return new AdministrativeUserDTO(user.UserID, user.Username, user.DisplayName, user.IsActive, roles, overrides);
    }

    public async Task<int> CreateWithRolesAsync(string username, string displayName, string passwordHash, IReadOnlyCollection<int> roleIds, CancellationToken cancellationToken = default)
    {
        using var connection = databaseConnection.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();
        try
        {
            var (insertSql, insertParameters) = createCommandBuilder()
                .Insert().Into<UserAccount>()
                .Values(new UserAccount { Username = username, DisplayName = displayName, PasswordHash = passwordHash, IsActive = true, MustChangePassword = true })
                .ReturningGeneratedId()
                .Build();
            var id = await connection.QuerySingleAsync<int>(new CommandDefinition(insertSql, insertParameters, transaction, cancellationToken: cancellationToken));

            if (roleIds.Count > 0)
            {
                var rows = roleIds.Distinct().Select(roleId => new UserRole { UserID = id, RoleID = roleId }).ToList();
                var (rolesSql, rolesParameters) = createCommandBuilder().Insert().Into<UserRole>().ValuesBatch(rows).BuildBatch();
                await connection.ExecuteAsync(new CommandDefinition(rolesSql, rolesParameters, transaction, cancellationToken: cancellationToken));
            }

            transaction.Commit();
            return id;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task<bool> UpdateAsync(int userId, string username, string displayName, CancellationToken cancellationToken = default)
    {
        var (sql, parameters) = createCommandBuilder()
            .Update().Table<ApplicationUser>()
            .Set((ApplicationUser u) => u.Username, username)
            .Set((ApplicationUser u) => u.DisplayName, displayName)
            .Where(w => w.Equals((ApplicationUser u) => u.UserID, userId))
            .Build();
        return await ExecuteAsync(sql, parameters, cancellationToken) == 1;
    }

    public async Task<bool> SetActivePreservingLastAdministratorAsync(int userId, bool isActive, int administratorRoleId, CancellationToken cancellationToken = default)
    {
        using var c = databaseConnection.CreateConnection();
        c.Open();
        using var t = c.BeginTransaction();
        try
        {
            var (lockRoleSql, lockRoleParameters) = createCommandBuilder()
                .Select(s => s.Columns<Role>(r => r.RoleID))
                .From<Role>()
                .Where(w => w.Equals((Role r) => r.RoleID, administratorRoleId))
                .ForUpdate()
                .Build();
            await c.ExecuteAsync(new CommandDefinition(lockRoleSql, lockRoleParameters, t, cancellationToken: cancellationToken));

            var (activeAdminsSql, activeAdminsParameters) = createCommandBuilder()
                .Select(s => s.Columns<ApplicationUser>(u => u.UserID))
                .From<ApplicationUser>()
                .Join(j => j.Inner<ApplicationUser, UserRole>((u, ur) => u.UserID == ur.UserID))
                .Where(w =>
                {
                    w.Equals((UserRole ur) => ur.RoleID, administratorRoleId);
                    w.Equals((ApplicationUser u) => u.IsActive, true);
                })
                .ForUpdate()
                .Build();
            var activeAdministrators = (await c.QueryAsync<int>(new CommandDefinition(activeAdminsSql, activeAdminsParameters, t, cancellationToken: cancellationToken))).ToArray();

            if (!isActive && activeAdministrators.Length <= 1 && activeAdministrators.Contains(userId))
            {
                throw new InvalidOperationException("O último Administrador ativo não pode ser desativado.");
            }

            var (updateSql, updateParameters) = createCommandBuilder()
                .Update().Table<ApplicationUser>()
                .Set((ApplicationUser u) => u.IsActive, isActive)
                .Where(w => w.Equals((ApplicationUser u) => u.UserID, userId))
                .Build();
            var changed = await c.ExecuteAsync(new CommandDefinition(updateSql, updateParameters, t, cancellationToken: cancellationToken));
            t.Commit();
            return changed == 1;
        }
        catch { t.Rollback(); throw; }
    }

    public async Task<bool> UpdateTemporaryPasswordAsync(int userId, string passwordHash, CancellationToken cancellationToken = default)
    {
        var (sql, parameters) = createCommandBuilder()
            .Update().Table<UserAccount>()
            .Set((UserAccount u) => u.PasswordHash, passwordHash)
            .Set((UserAccount u) => u.MustChangePassword, true)
            .Where(w =>
            {
                w.Equals((UserAccount u) => u.UserID, userId);
                w.Equals((UserAccount u) => u.IsActive, true);
            })
            .Build();
        return await ExecuteAsync(sql, parameters, cancellationToken) == 1;
    }

    public async Task ReplaceRolesPreservingLastAdministratorAsync(int userId, IReadOnlyCollection<int> roleIds, int administratorRoleId, CancellationToken cancellationToken = default)
    {
        using var connection = databaseConnection.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();
        try
        {
            var (lockRoleSql, lockRoleParameters) = createCommandBuilder()
                .Select(s => s.Columns<Role>(r => r.RoleID))
                .From<Role>()
                .Where(w => w.Equals((Role r) => r.RoleID, administratorRoleId))
                .ForUpdate()
                .Build();
            await connection.ExecuteAsync(new CommandDefinition(lockRoleSql, lockRoleParameters, transaction, cancellationToken: cancellationToken));

            var (activeAdminsSql, activeAdminsParameters) = createCommandBuilder()
                .Select(s => s.Columns<ApplicationUser>(u => u.UserID))
                .From<ApplicationUser>()
                .Join(j => j.Inner<ApplicationUser, UserRole>((u, ur) => u.UserID == ur.UserID))
                .Where(w =>
                {
                    w.Equals((UserRole ur) => ur.RoleID, administratorRoleId);
                    w.Equals((ApplicationUser u) => u.IsActive, true);
                })
                .ForUpdate()
                .Build();
            var active = (await connection.QueryAsync<int>(new CommandDefinition(activeAdminsSql, activeAdminsParameters, transaction, cancellationToken: cancellationToken))).ToArray();

            if (active.Length <= 1 && active.Contains(userId) && !roleIds.Contains(administratorRoleId))
            {
                throw new InvalidOperationException("O último Administrador ativo não pode perder a role Administrador.");
            }

            var (deleteSql, deleteParameters) = createCommandBuilder()
                .Delete<UserRole>()
                .Where(w => w.Equals((UserRole ur) => ur.UserID, userId))
                .Build();
            await connection.ExecuteAsync(new CommandDefinition(deleteSql, deleteParameters, transaction, cancellationToken: cancellationToken));

            if (roleIds.Count > 0)
            {
                var rows = roleIds.Distinct().Select(roleId => new UserRole { UserID = userId, RoleID = roleId }).ToList();
                var (insertSql, insertParameters) = createCommandBuilder().Insert().Into<UserRole>().ValuesBatch(rows).BuildBatch();
                await connection.ExecuteAsync(new CommandDefinition(insertSql, insertParameters, transaction, cancellationToken: cancellationToken));
            }

            transaction.Commit();
        }
        catch { transaction.Rollback(); throw; }
    }

    public async Task ReplacePermissionOverridesAsync(int userId, IReadOnlyCollection<PermissionOverrideDTO> overrides, CancellationToken cancellationToken = default)
    {
        using var connection = databaseConnection.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();
        try
        {
            var (deleteSql, deleteParameters) = createCommandBuilder()
                .Delete<UserPermissionOverride>()
                .Where(w => w.Equals((UserPermissionOverride o) => o.UserID, userId))
                .Build();
            await connection.ExecuteAsync(new CommandDefinition(deleteSql, deleteParameters, transaction, cancellationToken: cancellationToken));

            if (overrides.Count > 0)
            {
                var rows = overrides
                    .GroupBy(x => x.PermissionID)
                    .Select(g => new UserPermissionOverride { UserID = userId, PermissionID = g.Key, IsGranted = g.Last().IsGranted })
                    .ToList();
                var (insertSql, insertParameters) = createCommandBuilder().Insert().Into<UserPermissionOverride>().ValuesBatch(rows).BuildBatch();
                await connection.ExecuteAsync(new CommandDefinition(insertSql, insertParameters, transaction, cancellationToken: cancellationToken));
            }

            transaction.Commit();
        }
        catch { transaction.Rollback(); throw; }
    }

    public async Task<IReadOnlyList<AdministrativeRoleDTO>> GetRolesAsync(CancellationToken cancellationToken = default)
    {
        var (sql, parameters) = createCommandBuilder()
            .Select(s => s.Columns<Role>(r => r.RoleID, r => r.RoleName, r => r.Description, r => r.HierarchyLevel, r => r.IsSystemProtected))
            .From<Role>()
            .OrderBy("r.HierarchyLevel DESC, r.RoleName ASC")
            .Build();
        using var connection = databaseConnection.CreateConnection();
        var result = await connection.QueryAsync<AdministrativeRoleDTO>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        return result.ToArray();
    }

    public async Task<IReadOnlyList<AdministrativePermissionDTO>> GetPermissionsAsync(CancellationToken cancellationToken = default)
    {
        var (sql, parameters) = createCommandBuilder()
            .Select(s => s.Columns<Permission>(p => p.PermissionID, p => p.PermissionCode, p => p.Description))
            .From<Permission>()
            .OrderBy("p.PermissionCode ASC")
            .Build();
        using var connection = databaseConnection.CreateConnection();
        var result = await connection.QueryAsync<AdministrativePermissionDTO>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        return result.ToArray();
    }

    private async Task<int> ExecuteAsync(string sql, object parameters, CancellationToken cancellationToken)
    {
        using var c = databaseConnection.CreateConnection();
        return await c.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }
}

