using Dapper;
using Decor.Core.Common;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Data;
using Decor.Core.Interfaces.Repositories;
using Decor.FluentSqlBuilder;
using MySqlConnector;

namespace Decor.Infrastructure.Data.Repositories;

public sealed class RoleRegistrationRepository(
    IDatabaseConnection databaseConnection,
    Func<FluentCommandBuilder> createCommandBuilder) : IRoleRegistrationRepository
{
    public async Task<int> CreateAsync(string name, string? description, int hierarchyLevel, CancellationToken cancellationToken = default)
    {
        var (sql, parameters) = createCommandBuilder()
            .Insert(insert => insert.Entity(new Role
            {
                RoleName = name,
                Description = description,
                HierarchyLevel = hierarchyLevel,
                IsSystemProtected = false
            }))
            .ReturningGeneratedId()
            .Build();
        using var connection = databaseConnection.CreateConnection();
        try
        {
            return await connection.QuerySingleAsync<int>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        }
        catch (MySqlException exception) when (exception.Number == 1062)
        {
            throw new InvalidOperationException("Já existe um grupo com este nome.", exception);
        }
    }

    public async Task<bool> UpdateAsync(int roleId, string name, string? description, int hierarchyLevel, CancellationToken cancellationToken = default)
    {
        var (sql, parameters) = createCommandBuilder()
            .Update(update => update.Entity<Role>(role => role.RoleName, name)
                .Entity<Role>(role => role.Description, description)
                .Entity<Role>(role => role.HierarchyLevel, hierarchyLevel))
            .Where(where => where.Equals<Role>(role => role.RoleID, roleId)
                .NotEquals<Role>(role => role.RoleName, SystemRoleDefaults.Administrators)
                .NotEquals<Role>(role => role.RoleName, "Administrador"))
            .Build();
        using var connection = databaseConnection.CreateConnection();
        try
        {
            return await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken)) == 1;
        }
        catch (MySqlException exception) when (exception.Number == 1062)
        {
            throw new InvalidOperationException("Já existe um grupo com este nome.", exception);
        }
    }
}