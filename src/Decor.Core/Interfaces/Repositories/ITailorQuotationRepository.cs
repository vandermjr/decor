using Decor.Core.Entities;

namespace Decor.Core.Interfaces.Repositories;

public interface ITailorQuotationRepository : IRepository<TailorQuotationRequest>
{
    Task<TailorQuotationRequest?> GetByIdAsync(int requestId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TailorQuotationRequest>> GetByQuoteItemIdAsync(int quoteItemId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TailorQuotationRequest>> GetByQuoteSectionIdAsync(int quoteSectionId, CancellationToken cancellationToken = default);
    Task<int> SaveRevisionAsync(TailorQuotationRevision revision, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TailorQuotationRevision>> GetRevisionsByRequestIdAsync(int requestId, CancellationToken cancellationToken = default);
}
