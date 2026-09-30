using Decor.Core.Configuration;

namespace Decor.Core.Interfaces.Services;

public interface IIconAppearanceService
{
    IconAppearance CurrentAppearance { get; }
    event Action<IconAppearance>? AppearanceChanged;

    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task SetAppearanceAsync(int materialSymbolWeight, double strokeThickness, CancellationToken cancellationToken = default);
}
