namespace Decor.Core.DTOs;

public sealed record DatabaseRestoreResult(DateTime RestoredAt, string SafetyBackupFile, bool RequiresSignOut = true);