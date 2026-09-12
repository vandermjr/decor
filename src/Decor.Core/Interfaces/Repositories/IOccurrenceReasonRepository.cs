using Decor.Core.Entities;

namespace Decor.Core.Interfaces.Repositories;

public interface IOccurrenceReasonRepository : IRepository<OccurrenceReason>
{
    Task<OccurrenceReason?> GetByIdAsync(int reasonId, CancellationToken cancellationToken = default);
    bool DescriptionExists(string description, int currentReasonId);
    Task<bool> IsInUseAsync(int reasonId, CancellationToken cancellationToken = default);
}