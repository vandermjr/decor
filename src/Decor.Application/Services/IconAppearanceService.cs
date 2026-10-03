using Decor.Core.Configuration;
using Decor.Core.Interfaces.Repositories;
using Decor.Core.Interfaces.Services;
using System.Globalization;

namespace Decor.Application.Services;

public sealed class IconAppearanceService(ISystemSettingsRepository systemSettingsRepository) : IIconAppearanceService
{
    private const string IconWeightKey = "IconWeight";
    private const string IconStrokeThicknessKey = "IconStrokeThickness";
    public IconAppearance CurrentAppearance { get; private set; } = IconAppearance.Default;
    public event Action<IconAppearance>? AppearanceChanged;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var settings = await systemSettingsRepository.GetAllAsync(cancellationToken);
        var appearance = ParseAppearance(settings.GetValueOrDefault(IconWeightKey), settings.GetValueOrDefault(IconStrokeThicknessKey));
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

        await systemSettingsRepository.SetAsync(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [IconWeightKey] = appearance.MaterialSymbolWeight.ToString(CultureInfo.InvariantCulture),
            [IconStrokeThicknessKey] = appearance.StrokeThickness.ToString("0.0", CultureInfo.InvariantCulture)
        }, cancellationToken);
        CurrentAppearance = appearance;
        AppearanceChanged?.Invoke(appearance);
    }

    private static IconAppearance ParseAppearance(string? weightValue, string? strokeValue)
    {
        var defaults = IconAppearance.Default;
        var hasValidWeight = int.TryParse(weightValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var weight)
            && weight is >= 100 and <= 700 && weight % 100 == 0;
        var hasValidStroke = double.TryParse(strokeValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var stroke)
            && stroke is >= 0 and <= 3;
        return new IconAppearance(
            hasValidWeight ? weight : defaults.MaterialSymbolWeight,
            hasValidStroke ? Math.Round(stroke, 1, MidpointRounding.AwayFromZero) : defaults.StrokeThickness);
    }
}
