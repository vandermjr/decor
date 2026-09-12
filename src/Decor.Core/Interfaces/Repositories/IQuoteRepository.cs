using Decor.Core.Entities;

namespace Decor.Core.Interfaces.Repositories;

public interface IQuoteRepository : IRepository<Quote>
{
    Task<Quote?> GetByIdAsync(int quoteId, CancellationToken cancellationToken = default);
    Task<Quote?> GetCompleteQuoteAsync(int quoteId, CancellationToken cancellationToken = default);
    Task<int> SaveSectionAsync(QuoteSection section, CancellationToken cancellationToken = default);
    Task<int> SaveItemAsync(QuoteItem item, CancellationToken cancellationToken = default);
    Task<int> SaveSpecificationValueAsync(QuoteItemSpecificationValue value, CancellationToken cancellationToken = default);
}
