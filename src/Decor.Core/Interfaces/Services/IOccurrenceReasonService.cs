using Decor.Core.DTOs;

namespace Decor.Core.Interfaces.Services;

public interface IOccurrenceReasonService
{
    Task<OccurrenceReasonDTO> GetOccurrenceReasonByIdAsync(int reasonId, CancellationToken cancellationToken = default);
    Task<IEnumerable<OccurrenceReasonDTO>> GetAllOccurrenceReasonsAsync(int page = 1, int pageSize = 100, CancellationToken cancellationToken = default);
    Task<IEnumerable<OccurrenceReasonDTO>> SearchOccurrenceReasonsAsync(string searchTerm, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default);
    Task SaveOccurrenceReasonAsync(OccurrenceReasonDTO reason, CancellationToken cancellationToken = default);
    Task DeleteOccurrenceReasonAsync(int reasonId, CancellationToken cancellationToken = default);
}