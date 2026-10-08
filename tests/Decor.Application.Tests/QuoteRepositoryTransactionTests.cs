using System.Collections;
using System.Data;
using System.Data.Common;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Data;
using Decor.FluentSqlBuilder;
using Decor.Infrastructure.Data.Repositories;
using Moq;
using Moq.Protected;

namespace Decor.Application.Tests;

public sealed class QuoteRepositoryTransactionTests
{
    [Fact]
    public async Task SaveAsync_InsertsQuoteAndDraftSectionInOneTransaction()
    {
        var database = new RecordingDatabase();
        var repository = new QuoteRepository(database, FluentCommandBuilder.Create);
        var quote = CreateOpenQuote();

        Assert.Equal(1, await repository.SaveAsync(quote));

        Assert.Equal(42, quote.QuoteID);
        var section = Assert.Single(quote.Sections);
        Assert.Equal(81, section.QuoteSectionID);
        Assert.Equal(42, section.QuoteID);
        Assert.Equal(2, database.Commands.Count);
        Assert.Contains("quotes", database.Commands[0].Sql);
        Assert.Contains("DiscountAmount", database.Commands[0].Sql);
        Assert.Contains(database.Commands[0].Parameters, parameter => parameter.Key.Contains("CustomerID") && parameter.Value == DBNull.Value);
        Assert.Contains(database.Commands[0].Parameters, parameter => parameter.Key.Contains("CreatedByEmployeeID") && parameter.Value == DBNull.Value);
        Assert.Contains(database.Commands[0].Parameters, parameter => parameter.Key.Contains("DiscountAmount") && Equals(parameter.Value, 0m));
        Assert.Contains("quote_sections", database.Commands[1].Sql);
        Assert.Contains(database.Commands[1].Parameters, parameter => parameter.Key.Contains("QuoteID") && Equals(parameter.Value, 42));
        Assert.All(database.Commands, command => Assert.Same(database.Transaction.Object, command.Transaction));
        database.Transaction.Verify(transaction => transaction.Commit(), Times.Once);
        database.Transaction.Verify(transaction => transaction.Rollback(), Times.Never);
    }

    [Fact]
    public async Task SaveAsync_RollsBackHeaderWhenInitialSectionFails()
    {
        var database = new RecordingDatabase { FailAtCommand = 2 };
        var repository = new QuoteRepository(database, FluentCommandBuilder.Create);

        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.SaveAsync(CreateOpenQuote()));

        database.Transaction.Verify(transaction => transaction.Commit(), Times.Never);
        database.Transaction.Verify(transaction => transaction.Rollback(), Times.Once);
    }

    [Fact]
    public async Task SaveAsync_CancellationRollsBackAndNeverCommits()
    {
        using var cancellation = new CancellationTokenSource();
        var database = new RecordingDatabase { AfterCommand = cancellation.Cancel };
        var repository = new QuoteRepository(database, FluentCommandBuilder.Create);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repository.SaveAsync(CreateOpenQuote(), cancellation.Token));

        database.Transaction.Verify(transaction => transaction.Commit(), Times.Never);
        database.Transaction.Verify(transaction => transaction.Rollback(), Times.Once);
    }

    [Fact]
    public async Task SaveAsync_PersistsDiscountAndAllRejectedSectionsWithoutDelete()
    {
        var database = new RecordingDatabase();
        var repository = new QuoteRepository(database, FluentCommandBuilder.Create);
        var quote = CreateOpenQuote();
        quote.QuoteID = 42;
        quote.DiscountAmount = 15m;
        var section = quote.Sections.First();
        section.QuoteSectionID = 81;
        section.Status = QuoteSectionStatus.Rejected;

        await repository.SaveAsync(quote);

        Assert.All(database.Commands, command => Assert.StartsWith("UPDATE", command.Sql.TrimStart(), StringComparison.OrdinalIgnoreCase));
        Assert.Contains(database.Commands[0].Parameters, parameter => parameter.Key.Contains("DiscountAmount") && Equals(parameter.Value, 15m));
        Assert.Contains(database.Commands[1].Parameters, parameter => parameter.Key.Contains("Status") && Convert.ToInt32(parameter.Value) == (int)QuoteSectionStatus.Rejected);
        database.Transaction.Verify(transaction => transaction.Commit(), Times.Once);
    }

    [Fact]
    public async Task DeleteItemAsync_DeletesSpecificationsAndItemUsingParameterizedIdsInOneTransaction()
    {
        var database = new RecordingDatabase();
        var repository = new QuoteRepository(database, FluentCommandBuilder.Create);

        Assert.Equal(1, await repository.DeleteItemAsync(1234, 5678));

        Assert.Equal(2, database.Commands.Count);
        var specifications = database.Commands[0];
        Assert.StartsWith("DELETE", specifications.Sql.TrimStart(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("quote_item_specification_values", specifications.Sql);
        Assert.Contains("QuoteItemID = @", specifications.Sql);
        Assert.DoesNotContain("1234", specifications.Sql);
        Assert.Equal(1234, Assert.Single(specifications.Parameters).Value);
        var command = database.Commands[1];
        Assert.StartsWith("DELETE", command.Sql.TrimStart(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("quote_items", command.Sql);
        Assert.Contains("QuoteItemID = @", command.Sql);
        Assert.Contains("AND", command.Sql);
        Assert.Contains("QuoteSectionID = @", command.Sql);
        Assert.DoesNotContain("1234", command.Sql);
        Assert.DoesNotContain("5678", command.Sql);
        Assert.Equal(2, command.Parameters.Count);
        Assert.Contains(command.Parameters, parameter => parameter.Key.Contains("QuoteItemID") && Equals(parameter.Value, 1234));
        Assert.Contains(command.Parameters, parameter => parameter.Key.Contains("QuoteSectionID") && Equals(parameter.Value, 5678));
        Assert.All(database.Commands, recorded => Assert.Same(database.Transaction.Object, recorded.Transaction));
        database.Transaction.Verify(transaction => transaction.Commit(), Times.Once);
        database.Transaction.Verify(transaction => transaction.Rollback(), Times.Never);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public async Task DeleteItemAsync_RollsBackSpecificationsWhenItemDeleteDoesNotAffectExactlyOneRow(int affectedRows)
    {
        var database = new RecordingDatabase { AffectedRows = affectedRows };
        await Assert.ThrowsAsync<InvalidOperationException>(() => new QuoteRepository(database, FluentCommandBuilder.Create).DeleteItemAsync(1234, 5678));
        Assert.Equal(2, database.Commands.Count);
        database.Transaction.Verify(transaction => transaction.Commit(), Times.Never);
        database.Transaction.Verify(transaction => transaction.Rollback(), Times.Once);
    }

    [Fact]
    public async Task DeleteItemAsync_RollsBackSpecificationsWhenItemDeleteFails()
    {
        var database = new RecordingDatabase { FailAtCommand = 2 };
        await Assert.ThrowsAsync<InvalidOperationException>(() => new QuoteRepository(database, FluentCommandBuilder.Create).DeleteItemAsync(1234, 5678));
        database.Transaction.Verify(transaction => transaction.Commit(), Times.Never);
        database.Transaction.Verify(transaction => transaction.Rollback(), Times.Once);
    }

    [Fact]
    public async Task DeleteItemAsync_RollsBackWhenCanceledDuringDeletion()
    {
        using var cancellation = new CancellationTokenSource();
        var database = new RecordingDatabase { AfterCommand = cancellation.Cancel };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new QuoteRepository(database, FluentCommandBuilder.Create)
            .DeleteItemAsync(1234, 5678, cancellation.Token));
        database.Transaction.Verify(transaction => transaction.Commit(), Times.Never);
        database.Transaction.Verify(transaction => transaction.Rollback(), Times.Once);
    }

    [Fact]
    public async Task DeleteItemAsync_RejectsCancellationBeforeExecutingDelete()
    {
        var database = new RecordingDatabase();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new QuoteRepository(database, FluentCommandBuilder.Create)
            .DeleteItemAsync(1234, 5678, cancellation.Token));

        Assert.Empty(database.Commands);
    }

    private static Quote CreateOpenQuote() => new()
    {
        SourceType = QuoteSourceType.DirectCapture,
        CreatedAt = DateTime.UtcNow,
        Sections = [new QuoteSection { SectionType = QuoteSectionType.Catalog, Status = QuoteSectionStatus.Draft, CreatedAt = DateTime.UtcNow }]
    };

    private sealed record RecordedCommand(string Sql, Dictionary<string, object?> Parameters, DbTransaction? Transaction);

    private sealed class RecordingDatabase : IDatabaseConnection
    {
        private readonly Mock<DbConnection> _connection = new();
        public Mock<DbTransaction> Transaction { get; } = new();
        public List<RecordedCommand> Commands { get; } = [];
        public int? FailAtCommand { get; init; }
        public Action? AfterCommand { get; init; }
        public int AffectedRows { get; init; } = 1;

        public RecordingDatabase()
        {
            var state = ConnectionState.Closed;
            _connection.SetupGet(connection => connection.State).Returns(() => state);
            _connection.Setup(connection => connection.Open()).Callback(() => state = ConnectionState.Open);
            _connection.Protected().Setup<DbTransaction>("BeginDbTransaction", ItExpr.IsAny<IsolationLevel>()).Returns(Transaction.Object);
            _connection.Protected().Setup<DbCommand>("CreateDbCommand").Returns(CreateCommand);
        }

        public IDbConnection CreateConnection() => _connection.Object;

        private DbCommand CreateCommand()
        {
            var parameters = new List<DbParameter>();
            var collection = new Mock<DbParameterCollection>();
            collection.Setup(instance => instance.Add(It.IsAny<object>()))
                .Callback((object parameter) => parameters.Add((DbParameter)parameter)).Returns(() => parameters.Count - 1);
            collection.Setup(instance => instance.GetEnumerator()).Returns(() => ((IEnumerable)parameters).GetEnumerator());
            var command = new Mock<DbCommand> { CallBase = true };
            command.SetupAllProperties();
            command.Protected().SetupGet<DbParameterCollection>("DbParameterCollection").Returns(collection.Object);
            command.Protected().Setup<DbParameter>("CreateDbParameter").Returns(() =>
            {
                var parameter = new Mock<DbParameter>();
                parameter.SetupAllProperties();
                return parameter.Object;
            });
            void Record()
            {
                Commands.Add(new RecordedCommand(command.Object.CommandText,
                    parameters.ToDictionary(parameter => parameter.ParameterName, parameter => parameter.Value), command.Object.Transaction));
                if (Commands.Count == FailAtCommand)
                    throw new InvalidOperationException("Simulated section persistence failure.");
                AfterCommand?.Invoke();
            }
            command.Protected().Setup<DbDataReader>("ExecuteDbDataReader", ItExpr.IsAny<CommandBehavior>()).Returns(() =>
            {
                Record();
                var table = new DataTable();
                table.Columns.Add("GeneratedId", typeof(int));
                table.Rows.Add(Commands.Count == 1 ? 42 : 81);
                return table.CreateDataReader();
            });
            command.Setup(instance => instance.ExecuteNonQuery()).Returns(() => { Record(); return AffectedRows; });
            return command.Object;
        }
    }
}