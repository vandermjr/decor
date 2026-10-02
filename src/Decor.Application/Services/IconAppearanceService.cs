using Decor.Core.Configuration;
using Decor.Core.Interfaces.Services;

namespace Decor.Application.Services;

public sealed class IconAppearanceService(IUserSettingsService userSettingsService) : IIconAppearanceService
{
    public IconAppearance CurrentAppearance { get; private set; } = IconAppearance.Default;
    public event Action<IconAppearance>? AppearanceChanged;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var appearance = (await userSettingsService.GetAsync(cancellationToken)).IconAppearance;
        if (CurrentAppearance == appearance) return;

        CurrentAppearance = appearance;
        AppearanceChanged?.Invoke(appearance);
    }

    public async Task SetAppearanceAsync(int materialSymbolWeight, double strokeThickness, CancellationToken cancellationToken = default)
    {
        if (materialSymbolWeight is < 100 or > 700 || materialSymbolWeight % 100 != 0)
            throw new ArgumentOutOfRangeException(nameof(materialSymbolWeight));
        if (strokeThickness is < 0 or > 3)
            throw new ArgumentOutOfRangeException(nameof(strokeThickness));

        var appearance = new IconAppearance(materialSymbolWeight, Math.Round(strokeThickness, 1, MidpointRounding.AwayFromZero));
        if (CurrentAppearance == appearance)
            return;

        await userSettingsService.SetIconAppearanceAsync(appearance, cancellationToken);
        CurrentAppearance = appearance;
        AppearanceChanged?.Invoke(appearance);
    }
}
