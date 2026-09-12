using Decor.Core.DTOs;

namespace Decor.Core.Interfaces.Services;

public interface ITailorQuotationService
{
    Task<IEnumerable<TailorQuotationRequestDTO>> SearchRequestsAsync(string searchTerm, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default);
    Task<IEnumerable<TailorQuotationRequestDTO>> GetAllRequestsAsync(int page = 1, int pageSize = 100, CancellationToken cancellationToken = default);
    Task<TailorQuotationRequestDTO> GetRequestByIdAsync(int requestId, CancellationToken cancellationToken = default);
    Task CreateRequestAsync(TailorQuotationRequestDTO requestDto, CancellationToken cancellationToken = default);
    Task AddRevisionAsync(TailorQuotationRevisionDTO revisionDto, CancellationToken cancellationToken = default);
    Task CloseRequestAsync(int requestId, CancellationToken cancellationToken = default);
    Task<IEnumerable<TailorQuotationRevisionDTO>> GetRevisionsByRequestIdAsync(int requestId, CancellationToken cancellationToken = default);
}
