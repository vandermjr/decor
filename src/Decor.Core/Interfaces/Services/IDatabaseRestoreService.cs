using Decor.Core.DTOs;

namespace Decor.Core.Interfaces.Services;

public interface IDatabaseRestoreService
{
    Task<DatabaseRestoreResult> RestoreBackupAsync(string backupFile, CancellationToken cancellationToken = default);
}