using Dapper;
using Decor.FluentSqlBuilder;
using Decor.FluentSqlBuilder.Dialects.MariaDB;
using Decor.Infrastructure.Data;
using Decor.Infrastructure.Data.Repositories;
using MySqlConnector;

namespace Decor.Infrastructure.IntegrationTests;

public sealed class IntelligentUserSearchIntegrationTests(MariaDbFixture fixture) : IClassFixture<MariaDbFixture>
{
    [Fact]
    public async Task SearchUsesAllTokensAcrossColumnsAndPreservesExactIdsAndEmptyListing()
    {
        await using var connection = new MySqlConnection(fixture.ConnectionString);
        await connection.ExecuteAsync("CREATE TABLE IF NOT EXISTS employees (EmployeeID INT NOT NULL AUTO_INCREMENT PRIMARY KEY, Name VARCHAR(150) NOT NULL, UserID INT NULL) ENGINE=InnoDB;");
        var suffix = Guid.NewGuid().ToString("N");
        var matchingId = await connection.QuerySingleAsync<int>(
            "INSERT INTO users (Username, DisplayName, PasswordHash, IsActive) VALUES (@Username, @DisplayName, 'test-hash', 1); SELECT LAST_INSERT_ID();",
            new { Username = "search_" + suffix, DisplayName = "Alpha Omega" });
        var otherId = await connection.QuerySingleAsync<int>(
            "INSERT INTO users (Username, DisplayName, PasswordHash, IsActive) VALUES (@Username, @DisplayName, 'test-hash', 0); SELECT LAST_INSERT_ID();",
            new { Username = "other_" + suffix, DisplayName = "Alpha only" });
        var repository = new UserAdministrationRepository(new DatabaseConnection(fixture.ConnectionString),
            () => FluentCommandBuilder.Create(new MariaDBDialect()));
        try
        {
            var tokens = await repository.SearchAsync(suffix + " Omega");
            tokens.Should().ContainSingle().Which.UserID.Should().Be(matchingId);
            var reversed = await repository.SearchAsync("Omega " + suffix);
            reversed.Should().ContainSingle().Which.UserID.Should().Be(matchingId);
            var phrase = await repository.SearchAsync(suffix + " \"Alpha Omega\"");
            phrase.Should().ContainSingle().Which.UserID.Should().Be(matchingId);
            (await repository.SearchAsync(suffix + " missing")).Should().BeEmpty();
            (await repository.SearchAsync(suffix + " ' OR 1=1 --")).Should().BeEmpty();
            var exact = await repository.SearchAsync(matchingId.ToString("D10"));
            exact.Should().ContainSingle().Which.UserID.Should().Be(matchingId);
            (await repository.SearchAsync("0")).Should().BeEmpty();
            (await repository.SearchAsync("-1")).Should().BeEmpty();
            (await repository.SearchAsync("2147483648")).Should().BeEmpty();
            var all = await repository.SearchAsync(" \t ");
            all.Should().Contain(user => user.UserID == matchingId);
            all.Should().Contain(user => user.UserID == otherId && !user.IsActive);
        }
        finally
        {
            await connection.ExecuteAsync("DELETE FROM users WHERE UserID IN @Ids;", new { Ids = new[] { matchingId, otherId } });
        }
    }
}