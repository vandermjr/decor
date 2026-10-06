using Dapper;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Data;
using Decor.Core.Interfaces.Repositories;
using Decor.FluentSqlBuilder;

namespace Decor.Infrastructure.Data.Repositories;

public sealed class UserRepository(IDatabaseConnection databaseConnection, Func<FluentCommandBuilder> createCommandBuilder,
    IUserAdministrationRepository? administrationRepository = null) : IUserRepository
{
    private readonly IDatabaseConnection _databaseConnection = databaseConnection;
    private readonly Func<FluentCommandBuilder> _createCommandBuilder = createCommandBuilder;

    public async Task<bool> UpdatePasswordAsync(int userId, string passwordHash, bool mustChangePassword, CancellationToken cancellationToken = default)
    {
        var (sql, parameters) = _createCommandBuilder()
            .Update(u => u.Entity<UserAccount>(user => user.PasswordHash, passwordHash)
                .Entity<UserAccount>(user => user.MustChangePassword, mustChangePassword))
            .Where(w => w
                .Equals<UserAccount>(u => u.UserID, userId)
                .Equals<UserAccount>(u => u.IsActive, true))
            .Build();

        using var connection = _databaseConnection.CreateConnection();
        var affectedRows = await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        return affectedRows == 1;
    }

    public async Task<string?> GetPasswordHashByUserIdAsync(int userId, CancellationToken cancellationToken = default)
    {
        var (sql, parameters) = _createCommandBuilder()
            .Select<UserAccount>(s => s.WithColumns(u => u.PasswordHash))
            .Where(w => w
                .Equals<UserAccount>(u => u.UserID, userId)
                .Equals<UserAccount>(u => u.IsActive, true))
            .Take(1)
            .Build();

        using var connection = _databaseConnection.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<string>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }

    public async Task<ApplicationUser?> GetByUserIdAsync(int userId, CancellationToken cancellationToken = default)
    {
        if (administrationRepository is not null)
            await administrationRepository.GetRolesAsync(cancellationToken);
        // Permissões concedidas por role, respeitando um override negativo/positivo do próprio usuário.
        var permissionsByRole = _createCommandBuilder()
            .Select<Permission>(s => s.WithColumns(p => p.PermissionCode))
            .Join(j => j
                .Inner<Permission, RolePermission>((p, rp) => p.PermissionID == rp.PermissionID)
                .Inner<RolePermission, UserRole>((rp, ur) => ur.RoleID == rp.RoleID)
                .Inner<UserRole, ApplicationUser>((ur, u) => u.UserID == ur.UserID)
                .Left<ApplicationUser, Permission, UserPermissionOverride>((u, p, o) => o.UserID == u.UserID && o.PermissionID == p.PermissionID))
            .Where(w => w
                .Equals<ApplicationUser>(u => u.UserID, userId)
                .Equals<ApplicationUser>(u => u.IsActive, true)
                .Group(g => g
                    .IsNull<UserPermissionOverride>(o => o.IsGranted)
                    .Or()
                    .Equals<UserPermissionOverride>(o => o.IsGranted, true)));

        // Permissões concedidas exclusivamente por override positivo, mesmo sem nenhuma role que as conceda.
        var permissionsByOverride = _createCommandBuilder()
            .Select<Permission>(s => s.WithColumns(p => p.PermissionCode))
            .Join(j => j
                .Inner<Permission, UserPermissionOverride>((p, o) => p.PermissionID == o.PermissionID && o.IsGranted == true)
                .Inner<UserPermissionOverride, ApplicationUser>((o, u) => u.UserID == o.UserID))
            .Where(w => w
                .Equals<ApplicationUser>(u => u.UserID, userId)
                .Equals<ApplicationUser>(u => u.IsActive, true));

        var effectivePermissions = permissionsByRole.Union(permissionsByOverride);

        var (sql, parameters) = _createCommandBuilder()
            .Batch()
            .Select<ApplicationUser>(s => s
                .WithColumns(u => u.UserID, u => u.Username, u => u.DisplayName, u => u.IsActive, u => u.MustChangePassword)
                .Where(w => w
                    .Equals<ApplicationUser>(u => u.UserID, userId)
                    .Equals<ApplicationUser>(u => u.IsActive, true))
                .Take(1))
            .Select<Employee>(s => s
                .WithColumns(e => e.Name)
                .Where(w => w.Equals<Employee>(e => e.UserID, userId))
                .OrderBy(o => o.Ascending<Employee>(e => e.EmployeeID))
                .Take(1))
            .Select<Role>(s => s
                .WithColumns(r => r.RoleName)
                .Join(j => j
                    .Inner<Role, UserRole>((r, ur) => r.RoleID == ur.RoleID)
                    .Inner<UserRole, ApplicationUser>((ur, u) => u.UserID == ur.UserID))
                .Where(w => w
                    .Equals<ApplicationUser>(u => u.UserID, userId)
                    .Equals<ApplicationUser>(u => u.IsActive, true)))
            .Select<Permission>(s => s
                .Distinct()
                .Column("effective.PermissionCode")
                .FromSubquery(effectivePermissions, "effective"))
            .Build();

        using var connection = _databaseConnection.CreateConnection();
        using var results = await connection.QueryMultipleAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        var user = await results.ReadSingleOrDefaultAsync<ApplicationUser>();
        if (user is null) return null;
        var employeeName = await results.ReadSingleOrDefaultAsync<string>();

        return new ApplicationUser
        {
            UserID = user.UserID,
            Username = user.Username,
            DisplayName = user.DisplayName,
            EmployeeName = employeeName,
            IsActive = user.IsActive,
            MustChangePassword = user.MustChangePassword,
            Roles = (await results.ReadAsync<string>()).ToArray(),
            Permissions = (await results.ReadAsync<string>()).ToArray()
        };
    }
}

