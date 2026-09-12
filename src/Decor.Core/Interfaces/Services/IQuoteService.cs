using Decor.Core.DTOs;
using Decor.Core.Entities;

namespace Decor.Core.Interfaces.Services;

public interface IQuoteService
{
    Task<IEnumerable<QuoteDTO>> SearchQuotesAsync(string searchTerm, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default);
    Task<IEnumerable<QuoteDTO>> GetAllQuotesAsync(int page = 1, int pageSize = 100, CancellationToken cancellationToken = default);
    Task<QuoteDTO> GetQuoteByIdAsync(int quoteId, CancellationToken cancellationToken = default);
    Task SaveQuoteAsync(QuoteDTO quoteDto, CancellationToken cancellationToken = default);
    Task DeleteQuoteAsync(int quoteId, CancellationToken cancellationToken = default);
    Task UpdateSectionStatusAsync(int quoteId, int sectionId, QuoteSectionStatus newStatus, CancellationToken cancellationToken = default);
    Task AddQuoteItemAsync(int quoteId, QuoteItemDTO quoteItemDto, CancellationToken cancellationToken = default);
    Task AddSpecificationValueAsync(int quoteId, int quoteItemId, QuoteItemSpecificationValueDTO valueDto, CancellationToken cancellationToken = default);
}
