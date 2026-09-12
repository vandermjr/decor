using Dapper;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Data;
using Decor.Core.Interfaces.Repositories;
using Decor.FluentSqlBuilder;

namespace Decor.Infrastructure.Data.Repositories;

public class QuoteRepository(IDatabaseConnection dbConnection, Func<FluentCommandBuilder> createCommandBuilder) : IQuoteRepository
{
    private readonly IDatabaseConnection _dbConnection = dbConnection;
    private readonly Func<FluentCommandBuilder> _createCommandBuilder = createCommandBuilder;

    public int Save(Quote entity)
    {
        using var conn = _dbConnection.CreateConnection();
        if (entity.QuoteID != 0)
        {
            var (sql, parameters) = _createCommandBuilder()
                .Update()
                .Table<Quote>()
                .Set(entity)
                .Where(w => w.Equals((Quote q) => q.QuoteID, entity.QuoteID))
                .Build();
            return conn.Execute(sql, parameters);
        }
        else
        {
            var (sql, parameters) = _createCommandBuilder()
                .Insert()
                .Into<Quote>()
                .Values(entity)
                .ReturningGeneratedId()
                .Build();
            var generatedId = conn.QuerySingle<int>(sql, parameters);
            entity.QuoteID = generatedId;
            return 1;
        }
    }

    public async Task<int> SaveAsync(Quote entity, CancellationToken cancellationToken = default)
    {
        using var connection = _dbConnection.CreateConnection();
        if (entity.QuoteID != 0)
        {
            var (sql, parameters) = _createCommandBuilder()
                .Update()
                .Table<Quote>()
                .Set(entity)
                .Where(w => w.Equals((Quote q) => q.QuoteID, entity.QuoteID))
                .Build();
            return await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        }
        else
        {
            var (sql, parameters) = _createCommandBuilder()
                .Insert()
                .Into<Quote>()
                .Values(entity)
                .ReturningGeneratedId()
                .Build();
            var generatedId = await connection.QuerySingleAsync<int>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
            entity.QuoteID = generatedId;
            return 1;
        }
    }

    public int Delete(int id)
    {
        var (sql, parameters) = _createCommandBuilder()
            .Delete<Quote>()
            .Where(w => w.Equals((Quote q) => q.QuoteID, id))
            .Build();

        using var conn = _dbConnection.CreateConnection();
        return conn.Execute(sql, parameters);
    }

    public async Task<int> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var (sql, parameters) = _createCommandBuilder()
            .Delete<Quote>()
            .Where(w => w.Equals((Quote q) => q.QuoteID, id))
            .Build();

        using var connection = _dbConnection.CreateConnection();
        return await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }

    public IEnumerable<Quote> SearchGetBy(string? arg = null)
    {
        var (sql, parameters) = _createCommandBuilder()
            .Select(s => s.AllColumns<Quote>())
            .From<Quote>()
            .Where(w => w.WithDynamicSearchFilter<Quote, Quote>(arg, q => q.QuoteID, q => q.Notes))
            .OrderBy("q.QuoteID ASC")
            .Build();

        using var conn = _dbConnection.CreateConnection();
        return conn.Query<Quote>(sql, parameters);
    }

    public async Task<IReadOnlyList<Quote>> SearchGetByAsync(string? arg = null, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default)
    {
        ValidatePage(page, pageSize);
        var (sql, parameters) = _createCommandBuilder()
            .Select(s => s.AllColumns<Quote>())
            .From<Quote>()
            .Where(w => w.WithDynamicSearchFilter<Quote, Quote>(arg, q => q.QuoteID, q => q.Notes))
            .OrderBy("q.QuoteID ASC")
            .Take((uint)pageSize)
            .Skip((uint)((page - 1) * pageSize))
            .Build();

        using var connection = _dbConnection.CreateConnection();
        var result = await connection.QueryAsync<Quote>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        return result.AsList();
    }

    public async Task<Quote?> GetByIdAsync(int quoteId, CancellationToken cancellationToken = default)
    {
        var (sql, parameters) = _createCommandBuilder()
            .Select(s => s.AllColumns<Quote>())
            .From<Quote>()
            .Where(w => w.Equals((Quote q) => q.QuoteID, quoteId))
            .Build();

        using var connection = _dbConnection.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<Quote>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }

    public async Task<Quote?> GetCompleteQuoteAsync(int quoteId, CancellationToken cancellationToken = default)
    {
        var quote = await GetByIdAsync(quoteId, cancellationToken);
        if (quote == null) return null;

        using var connection = _dbConnection.CreateConnection();

        var (secSql, secParams) = _createCommandBuilder()
            .Select(s => s.AllColumns<QuoteSection>())
            .From<QuoteSection>()
            .Where(w => w.Equals((QuoteSection s) => s.QuoteID, quoteId))
            .OrderBy("s.QuoteSectionID ASC")
            .Build();

        var sections = (await connection.QueryAsync<QuoteSection>(new CommandDefinition(secSql, secParams, cancellationToken: cancellationToken))).ToList();
        quote.Sections = sections;

        foreach (var section in sections)
        {
            var (itemSql, itemParams) = _createCommandBuilder()
                .Select(s => s.AllColumns<QuoteItem>())
                .From<QuoteItem>()
                .Where(w => w.Equals((QuoteItem i) => i.QuoteSectionID, section.QuoteSectionID))
                .OrderBy("i.QuoteItemID ASC")
                .Build();

            var items = (await connection.QueryAsync<QuoteItem>(new CommandDefinition(itemSql, itemParams, cancellationToken: cancellationToken))).ToList();
            section.Items = items;

            foreach (var item in items)
            {
                var (valSql, valParams) = _createCommandBuilder()
                    .Select(s => s.AllColumns<QuoteItemSpecificationValue>())
                    .From<QuoteItemSpecificationValue>()
                    .Where(w => w.Equals((QuoteItemSpecificationValue v) => v.QuoteItemID, item.QuoteItemID))
                    .OrderBy("v.ValueID ASC")
                    .Build();

                var values = (await connection.QueryAsync<QuoteItemSpecificationValue>(new CommandDefinition(valSql, valParams, cancellationToken: cancellationToken))).ToList();
                item.SpecificationValues = values;
            }
        }

        return quote;
    }

    public async Task<QuoteSection?> GetSectionByItemIdAsync(int quoteItemId, CancellationToken cancellationToken = default)
    {
        var item = await GetItemByIdAsync(quoteItemId, cancellationToken);
        if (item == null) return null;

        var (sql, parameters) = _createCommandBuilder()
            .Select(s => s.AllColumns<QuoteSection>())
            .From<QuoteSection>()
            .Where(w => w.Equals((QuoteSection s) => s.QuoteSectionID, item.QuoteSectionID))
            .Build();

        using var connection = _dbConnection.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<QuoteSection>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }

    public async Task<QuoteItem?> GetItemByIdAsync(int quoteItemId, CancellationToken cancellationToken = default)
    {
        var (sql, parameters) = _createCommandBuilder()
            .Select(s => s.AllColumns<QuoteItem>())
            .From<QuoteItem>()
            .Where(w => w.Equals((QuoteItem i) => i.QuoteItemID, quoteItemId))
            .Build();

        using var connection = _dbConnection.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<QuoteItem>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }

    public async Task<int> SaveSectionAsync(QuoteSection section, CancellationToken cancellationToken = default)
    {
        using var connection = _dbConnection.CreateConnection();
        if (section.QuoteSectionID != 0)
        {
            var (sql, parameters) = _createCommandBuilder()
                .Update()
                .Table<QuoteSection>()
                .Set(section)
                .Where(w => w.Equals((QuoteSection s) => s.QuoteSectionID, section.QuoteSectionID))
                .Build();
            return await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        }
        else
        {
            var (sql, parameters) = _createCommandBuilder()
                .Insert()
                .Into<QuoteSection>()
                .Values(section)
                .ReturningGeneratedId()
                .Build();
            var generatedId = await connection.QuerySingleAsync<int>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
            section.QuoteSectionID = generatedId;
            return 1;
        }
    }

    public async Task<int> SaveItemAsync(QuoteItem item, CancellationToken cancellationToken = default)
    {
        using var connection = _dbConnection.CreateConnection();
        if (item.QuoteItemID != 0)
        {
            var (sql, parameters) = _createCommandBuilder()
                .Update()
                .Table<QuoteItem>()
                .Set(item)
                .Where(w => w.Equals((QuoteItem i) => i.QuoteItemID, item.QuoteItemID))
                .Build();
            return await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        }
        else
        {
            var (sql, parameters) = _createCommandBuilder()
                .Insert()
                .Into<QuoteItem>()
                .Values(item)
                .ReturningGeneratedId()
                .Build();
            var generatedId = await connection.QuerySingleAsync<int>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
            item.QuoteItemID = generatedId;
            return 1;
        }
    }

    public async Task<int> SaveSpecificationValueAsync(QuoteItemSpecificationValue value, CancellationToken cancellationToken = default)
    {
        using var connection = _dbConnection.CreateConnection();
        if (value.ValueID != 0)
        {
            var (sql, parameters) = _createCommandBuilder()
                .Update()
                .Table<QuoteItemSpecificationValue>()
                .Set(value)
                .Where(w => w.Equals((QuoteItemSpecificationValue v) => v.ValueID, value.ValueID))
                .Build();
            return await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        }
        else
        {
            var (sql, parameters) = _createCommandBuilder()
                .Insert()
                .Into<QuoteItemSpecificationValue>()
                .Values(value)
                .ReturningGeneratedId()
                .Build();
            var generatedId = await connection.QuerySingleAsync<int>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
            value.ValueID = generatedId;
            return 1;
        }
    }

    private static void ValidatePage(int page, int pageSize)
    {
        if (page < 1) throw new ArgumentOutOfRangeException(nameof(page));
        if (pageSize is < 1 or > 500) throw new ArgumentOutOfRangeException(nameof(pageSize));
    }
}
