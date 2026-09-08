using Dapper;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Data;
using Decor.Core.Interfaces.Repositories;
using Decor.FluentSqlBuilder;

namespace Decor.Infrastructure.Data.Repositories;

public sealed class UserRepository(IDatabaseConnection databaseConnection, Func<FluentCommandBuilder> createCommandBuilder) : IUserRepository
{
    public async Task<bool> UpdatePasswordAsync(int userId, string passwordHash, bool mustChangePassword, CancellationToken cancellationToken = default)
    {
        var (sql, parameters) = createCommandBuilder()
            .Update().Table<UserAccount>()
            .Set((UserAccount u) => u.PasswordHash, passwordHash)
            .Set((UserAccount u) => u.MustChangePassword, mustChangePassword)
            .Where(w =>
            {
                w.Equals((UserAccount u) => u.UserID, userId);
                w.Equals((UserAccount u) => u.IsActive, true);
            })
            .Build();

        using var connection = databaseConnection.CreateConnection();
        var affectedRows = await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        return affectedRows == 1;
    }

    public async Task<string?> GetPasswordHashByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        var (sql, parameters) = createCommandBuilder()
            .Select(s => s.Columns<UserAccount>(u => u.PasswordHash))
            .From<UserAccount>()
            .Where(w =>
            {
                w.Equals((UserAccount u) => u.Username, username);
                w.Equals((UserAccount u) => u.IsActive, true);
            })
            .Take(1)
            .Build();

        using var connection = databaseConnection.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<string>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }

    public async Task<ApplicationUser?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        // Permissões concedidas por role, respeitando um override negativo/positivo do próprio usuário.
        var permissionsByRole = createCommandBuilder()
            .Select(s => s.Columns<Permission>(p => p.PermissionCode))
            .From<Permission>()
            .Join(j =>
            {
                j.Inner<Permission, RolePermission>((p, rp) => p.PermissionID == rp.PermissionID);
                j.Inner<RolePermission, UserRole>((rp, ur) => ur.RoleID == rp.RoleID);
                j.Inner<UserRole, ApplicationUser>((ur, u) => u.UserID == ur.UserID);
                j.Left<ApplicationUser, Permission, UserPermissionOverride>((u, p, o) => o.UserID == u.UserID && o.PermissionID == p.PermissionID);
            })
            .Where(w =>
            {
                w.Equals((ApplicationUser u) => u.Username, username);
                w.Equals((ApplicationUser u) => u.IsActive, true);
                w.Group(g =>
                {
                    g.IsNull((UserPermissionOverride o) => o.IsGranted);
                    g.Or();
                    g.Equals((UserPermissionOverride o) => o.IsGranted, true);
                });
            });

        // Permissões concedidas exclusivamente por override positivo, mesmo sem nenhuma role que as conceda.
        var permissionsByOverride = createCommandBuilder()
            .Select(s => s.Columns<Permission>(p => p.PermissionCode))
            .From<Permission>()
            .Join(j =>
            {
                j.Inner<Permission, UserPermissionOverride>((p, o) => p.PermissionID == o.PermissionID && o.IsGranted == true);
                j.Inner<UserPermissionOverride, ApplicationUser>((o, u) => u.UserID == o.UserID);
            })
            .Where(w =>
            {
                w.Equals((ApplicationUser u) => u.Username, username);
                w.Equals((ApplicationUser u) => u.IsActive, true);
            });

        var effectivePermissions = permissionsByRole.Union(permissionsByOverride);

        var (sql, parameters) = createCommandBuilder()
            .Batch()
            .Select(s => s
                .Select(sc => sc.Columns<ApplicationUser>(u => u.UserID, u => u.Username, u => u.DisplayName, u => u.IsActive, u => u.MustChangePassword))
                .From<ApplicationUser>()
                .Where(w =>
                {
                    w.Equals((ApplicationUser u) => u.Username, username);
                    w.Equals((ApplicationUser u) => u.IsActive, true);
                })
                .Take(1))
            .Select(s => s
                .Select(sc => sc.Columns<Role>(r => r.RoleName))
                .From<Role>()
                .Join(j =>
                {
                    j.Inner<Role, UserRole>((r, ur) => r.RoleID == ur.RoleID);
                    j.Inner<UserRole, ApplicationUser>((ur, u) => u.UserID == ur.UserID);
                })
                .Where(w =>
                {
                    w.Equals((ApplicationUser u) => u.Username, username);
                    w.Equals((ApplicationUser u) => u.IsActive, true);
                }))
            .Select(s => s
                .Select(sc =>
                {
                    sc.Distinct();
                    sc.Column("effective.PermissionCode");
                })
                .FromSubquery(effectivePermissions, "effective"))
            .Build();

        using var connection = databaseConnection.CreateConnection();
        using var results = await connection.QueryMultipleAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        var user = await results.ReadSingleOrDefaultAsync<ApplicationUser>();
        if (user is null) return null;

        return new ApplicationUser
        {
            UserID = user.UserID,
            Username = user.Username,
            DisplayName = user.DisplayName,
            IsActive = user.IsActive,
            MustChangePassword = user.MustChangePassword,
            Roles = (await results.ReadAsync<string>()).ToArray(),
            Permissions = (await results.ReadAsync<string>()).ToArray()
        };
    }
}

