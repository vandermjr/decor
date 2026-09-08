using Decor.Core.DTOs;

namespace Decor.Core.Interfaces.Services;

public interface IDatabaseBackupService
{
    Task<DatabaseBackupResult> CreateBackupAsync(IReadOnlyCollection<string> backupFiles, CancellationToken cancellationToken = default);
}
