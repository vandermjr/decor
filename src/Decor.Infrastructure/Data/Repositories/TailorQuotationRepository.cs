using Dapper;
using Decor.Core.Entities;
using Decor.Core.Interfaces.Data;
using Decor.Core.Interfaces.Repositories;
using Decor.FluentSqlBuilder;

namespace Decor.Infrastructure.Data.Repositories;

public class TailorQuotationRepository(IDatabaseConnection dbConnection, Func<FluentCommandBuilder> createCommandBuilder) : ITailorQuotationRepository
{
    private readonly IDatabaseConnection _dbConnection = dbConnection;
    private readonly Func<FluentCommandBuilder> _createCommandBuilder = createCommandBuilder;

    public int Save(TailorQuotationRequest entity)
    {
        using var conn = _dbConnection.CreateConnection();
        if (entity.RequestID != 0)
        {
            var (sql, parameters) = _createCommandBuilder()
                .Update()
                .Table<TailorQuotationRequest>()
                .Set(entity)
                .Where(w => w.Equals((TailorQuotationRequest r) => r.RequestID, entity.RequestID))
                .Build();
            return conn.Execute(sql, parameters);
        }
        else
        {
            var (sql, parameters) = _createCommandBuilder()
                .Insert()
                .Into<TailorQuotationRequest>()
                .Values(entity)
                .ReturningGeneratedId()
                .Build();
            var generatedId = conn.QuerySingle<int>(sql, parameters);
            entity.RequestID = generatedId;
            return 1;
        }
    }

    public async Task<int> SaveAsync(TailorQuotationRequest entity, CancellationToken cancellationToken = default)
    {
        using var connection = _dbConnection.CreateConnection();
        if (entity.RequestID != 0)
        {
            var (sql, parameters) = _createCommandBuilder()
                .Update()
                .Table<TailorQuotationRequest>()
                .Set(entity)
                .Where(w => w.Equals((TailorQuotationRequest r) => r.RequestID, entity.RequestID))
                .Build();
            return await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        }
        else
        {
            var (sql, parameters) = _createCommandBuilder()
                .Insert()
                .Into<TailorQuotationRequest>()
                .Values(entity)
                .ReturningGeneratedId()
                .Build();
            var generatedId = await connection.QuerySingleAsync<int>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
            entity.RequestID = generatedId;
            return 1;
        }
    }

    public int Delete(int id)
    {
        var (sql, parameters) = _createCommandBuilder()
            .Delete<TailorQuotationRequest>()
            .Where(w => w.Equals((TailorQuotationRequest r) => r.RequestID, id))
            .Build();

        using var conn = _dbConnection.CreateConnection();
        return conn.Execute(sql, parameters);
    }

    public async Task<int> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var (sql, parameters) = _createCommandBuilder()
            .Delete<TailorQuotationRequest>()
            .Where(w => w.Equals((TailorQuotationRequest r) => r.RequestID, id))
            .Build();

        using var connection = _dbConnection.CreateConnection();
        return await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }

    public IEnumerable<TailorQuotationRequest> SearchGetBy(string? arg = null)
    {
        var (sql, parameters) = _createCommandBuilder()
            .Select(s => s.AllColumns<TailorQuotationRequest>())
            .From<TailorQuotationRequest>()
            .Where(w => w.WithDynamicSearchFilter<TailorQuotationRequest, TailorQuotationRequest>(arg, r => r.RequestID, r => r.RequestID))
            .OrderBy("r.RequestID ASC")
            .Build();

        using var conn = _dbConnection.CreateConnection();
        return conn.Query<TailorQuotationRequest>(sql, parameters);
    }

    public async Task<IReadOnlyList<TailorQuotationRequest>> SearchGetByAsync(string? arg = null, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default)
    {
        ValidatePage(page, pageSize);
        var (sql, parameters) = _createCommandBuilder()
            .Select(s => s.AllColumns<TailorQuotationRequest>())
            .From<TailorQuotationRequest>()
            .Where(w => w.WithDynamicSearchFilter<TailorQuotationRequest, TailorQuotationRequest>(arg, r => r.RequestID, r => r.RequestID))
            .OrderBy("r.RequestID ASC")
            .Take((uint)pageSize)
            .Skip((uint)((page - 1) * pageSize))
            .Build();

        using var connection = _dbConnection.CreateConnection();
        var result = await connection.QueryAsync<TailorQuotationRequest>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        return result.AsList();
    }

    public async Task<TailorQuotationRequest?> GetByIdAsync(int requestId, CancellationToken cancellationToken = default)
    {
        var (sql, parameters) = _createCommandBuilder()
            .Select(s => s.AllColumns<TailorQuotationRequest>())
            .From<TailorQuotationRequest>()
            .Where(w => w.Equals((TailorQuotationRequest r) => r.RequestID, requestId))
            .Build();

        using var connection = _dbConnection.CreateConnection();
        var request = await connection.QuerySingleOrDefaultAsync<TailorQuotationRequest>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        if (request != null)
        {
            var revisions = await GetRevisionsByRequestIdAsync(requestId, cancellationToken);
            request.Revisions = revisions.ToList();
        }
        return request;
    }

    public async Task<IReadOnlyList<TailorQuotationRequest>> GetByQuoteItemIdAsync(int quoteItemId, CancellationToken cancellationToken = default)
    {
        var (sql, parameters) = _createCommandBuilder()
            .Select(s => s.AllColumns<TailorQuotationRequest>())
            .From<TailorQuotationRequest>()
            .Where(w => w.Equals((TailorQuotationRequest r) => r.QuoteItemID, quoteItemId))
            .OrderBy("r.RequestID ASC")
            .Build();

        using var connection = _dbConnection.CreateConnection();
        var result = await connection.QueryAsync<TailorQuotationRequest>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        return result.AsList();
    }

    public async Task<IReadOnlyList<TailorQuotationRequest>> GetByQuoteSectionIdAsync(int quoteSectionId, CancellationToken cancellationToken = default)
    {
        var (sql, parameters) = _createCommandBuilder()
            .Select(s => s.AllColumns<TailorQuotationRequest>())
            .From<TailorQuotationRequest>()
            .Join(j => j.Inner<TailorQuotationRequest, QuoteItem>((r, qi) => r.QuoteItemID == qi.QuoteItemID))
            .Where(w => w.Equals((QuoteItem qi) => qi.QuoteSectionID, quoteSectionId))
            .OrderBy("r.RequestID ASC")
            .Build();

        using var connection = _dbConnection.CreateConnection();
        var result = await connection.QueryAsync<TailorQuotationRequest>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        return result.AsList();
    }

    public async Task<int> SaveRevisionAsync(TailorQuotationRevision revision, CancellationToken cancellationToken = default)
    {
        using var connection = _dbConnection.CreateConnection();
        if (revision.RevisionID != 0)
        {
            var (sql, parameters) = _createCommandBuilder()
                .Update()
                .Table<TailorQuotationRevision>()
                .Set(revision)
                .Where(w => w.Equals((TailorQuotationRevision r) => r.RevisionID, revision.RevisionID))
                .Build();
            return await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        }
        else
        {
            var (sql, parameters) = _createCommandBuilder()
                .Insert()
                .Into<TailorQuotationRevision>()
                .Values(revision)
                .ReturningGeneratedId()
                .Build();
            var generatedId = await connection.QuerySingleAsync<int>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
            revision.RevisionID = generatedId;
            return 1;
        }
    }

    public async Task<IReadOnlyList<TailorQuotationRevision>> GetRevisionsByRequestIdAsync(int requestId, CancellationToken cancellationToken = default)
    {
        var (sql, parameters) = _createCommandBuilder()
            .Select(s => s.AllColumns<TailorQuotationRevision>())
            .From<TailorQuotationRevision>()
            .Where(w => w.Equals((TailorQuotationRevision rev) => rev.RequestID, requestId))
            .OrderBy("rev.RevisionNumber ASC")
            .Build();

        using var connection = _dbConnection.CreateConnection();
        var result = await connection.QueryAsync<TailorQuotationRevision>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        return result.AsList();
    }

    private static void ValidatePage(int page, int pageSize)
    {
        if (page < 1) throw new ArgumentOutOfRangeException(nameof(page));
        if (pageSize is < 1 or > 500) throw new ArgumentOutOfRangeException(nameof(pageSize));
    }
}
