using Dapper;
using Decor.Core.DTOs;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Data;
using Decor.Core.Interfaces.Repositories;
using Decor.FluentSqlBuilder;
namespace Decor.Infrastructure.Data.Repositories;
public sealed class RoleAdministrationRepository(IDatabaseConnection databaseConnection, Func<FluentCommandBuilder> createCommandBuilder) : IRoleAdministrationRepository
{
    public async Task<IReadOnlyList<AdministrativePermissionDTO>> GetPermissionsAsync(int roleId, CancellationToken cancellationToken = default)
    {
        var (sql, parameters) = createCommandBuilder()
            .Select(s => s.Columns<Permission>(p => p.PermissionID, p => p.PermissionCode, p => p.Description))
            .From<Permission>()
            .Join(j => j.Inner<Permission, RolePermission>((p, rp) => p.PermissionID == rp.PermissionID))
            .Where(w => w.Equals((RolePermission rp) => rp.RoleID, roleId))
            .OrderBy("p.PermissionCode ASC")
            .Build();

        using var connection = databaseConnection.CreateConnection();
        var result = await connection.QueryAsync<AdministrativePermissionDTO>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        return result.ToArray();
    }

    public async Task ReplacePermissionsAsync(int roleId, IReadOnlyCollection<int> permissionIds, CancellationToken cancellationToken = default)
    {
        using var connection = databaseConnection.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();
        try
        {
            var (deleteSql, deleteParameters) = createCommandBuilder()
                .Delete<RolePermission>()
                .Where(w => w.Equals((RolePermission rp) => rp.RoleID, roleId))
                .Build();
            await connection.ExecuteAsync(new CommandDefinition(deleteSql, deleteParameters, transaction, cancellationToken: cancellationToken));

            if (permissionIds.Count > 0)
            {
                var rows = permissionIds.Distinct().Select(permissionId => new RolePermission { RoleID = roleId, PermissionID = permissionId }).ToList();
                var (insertSql, insertParameters) = createCommandBuilder()
                    .Insert().Into<RolePermission>()
                    .ValuesBatch(rows)
                    .BuildBatch();
                await connection.ExecuteAsync(new CommandDefinition(insertSql, insertParameters, transaction, cancellationToken: cancellationToken));
            }

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }
}

