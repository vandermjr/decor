namespace Decor.Core.DTOs;

public sealed record DatabaseBackupResult(DateTime CreatedAt, IReadOnlyList<string> Files);
