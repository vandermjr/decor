using Decor.Core.Configuration;
using Decor.Core.Interfaces.Repositories;
using Decor.Core.Interfaces.Services;
using System;
using System.Globalization;
using System.Linq;
using System.Text.Json;

namespace Decor.Application.Services;

public sealed class IconAppearanceService(ISystemSettingsRepository systemSettingsRepository) : IIconAppearanceService
{
    private const string IconWeightKey = "IconWeight";
    private const string IconStrokeThicknessKey = "IconStrokeThickness";
    private const string IconColorsKey = "IconColors";
    public IconAppearance CurrentAppearance { get; private set; } = IconAppearance.Default;
    public event Action<IconAppearance>? AppearanceChanged;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await systemSettingsRepository.InitializeAsync(cancellationToken);
        var settings = await systemSettingsRepository.GetAllAsync(cancellationToken);
        var appearance = ParseAppearance(settings.GetValueOrDefault(IconWeightKey), settings.GetValueOrDefault(IconStrokeThicknessKey), settings.GetValueOrDefault(IconColorsKey));
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

        var appearance = new IconAppearance(
            materialSymbolWeight,
            Math.Round(strokeThickness, 1, MidpointRounding.AwayFromZero),
            new Dictionary<string, string>(CurrentAppearance.IconColors, StringComparer.Ordinal));
        if (CurrentAppearance == appearance)
            return;

        await SaveAppearanceAsync(appearance, cancellationToken);
    }

    public async Task SetIconColorOverrideAsync(string iconId, string? colorHex, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(iconId))
            throw new ArgumentException("An icon identifier is required.", nameof(iconId));

        var iconColors = new Dictionary<string, string>(CurrentAppearance.IconColors, StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(colorHex))
        {
            iconColors.Remove(iconId);
        }
        else
        {
            var normalized = NormalizeColorHex(colorHex);
            if (normalized is null)
                throw new ArgumentException("The icon color must be a valid hex value.", nameof(colorHex));

            iconColors[iconId] = normalized;
        }

        var appearance = new IconAppearance(CurrentAppearance.MaterialSymbolWeight, CurrentAppearance.StrokeThickness, iconColors);
        await SaveAppearanceAsync(appearance, cancellationToken);
    }

    public async Task ResetIconColorsAsync(CancellationToken cancellationToken = default)
    {
        if (CurrentAppearance.IconColors.Count == 0)
            return;

        var appearance = new IconAppearance(CurrentAppearance.MaterialSymbolWeight, CurrentAppearance.StrokeThickness, new Dictionary<string, string>(StringComparer.Ordinal));
        await SaveAppearanceAsync(appearance, cancellationToken);
    }

    private async Task SaveAppearanceAsync(IconAppearance appearance, CancellationToken cancellationToken)
    {
        var serializedColors = appearance.IconColors.Count == 0
            ? string.Empty
            : JsonSerializer.Serialize(appearance.IconColors);

        await systemSettingsRepository.SetAsync(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [IconWeightKey] = appearance.MaterialSymbolWeight.ToString(CultureInfo.InvariantCulture),
            [IconStrokeThicknessKey] = appearance.StrokeThickness.ToString("0.0", CultureInfo.InvariantCulture),
            [IconColorsKey] = serializedColors
        }, cancellationToken);
        CurrentAppearance = appearance;
        AppearanceChanged?.Invoke(appearance);
    }

    private static IconAppearance ParseAppearance(string? weightValue, string? strokeValue, string? iconColorsValue)
    {
        var defaults = IconAppearance.Default;
        var hasValidWeight = int.TryParse(weightValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var weight)
            && weight is >= 100 and <= 700 && weight % 100 == 0;
        var hasValidStroke = double.TryParse(strokeValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var stroke)
            && stroke is >= 0 and <= 3;
        return new IconAppearance(
            hasValidWeight ? weight : defaults.MaterialSymbolWeight,
            hasValidStroke ? Math.Round(stroke, 1, MidpointRounding.AwayFromZero) : defaults.StrokeThickness,
            ParseIconColors(iconColorsValue));
    }

    private static IReadOnlyDictionary<string, string> ParseIconColors(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return new Dictionary<string, string>(StringComparer.Ordinal);

        try
        {
            var dictionary = JsonSerializer.Deserialize<Dictionary<string, string>>(value);
            if (dictionary is null)
                return new Dictionary<string, string>(StringComparer.Ordinal);

            return new Dictionary<string, string>(dictionary, StringComparer.Ordinal);
        }
        catch (JsonException)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }
    }

    private static string? NormalizeColorHex(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var sanitized = value.Trim();
        if (sanitized.StartsWith('#'))
            sanitized = sanitized[1..];

        if (sanitized.Length is 3 or 6 or 8 && sanitized.All(char.IsAsciiHexDigit))
            return "#" + sanitized;

        return null;
    }
}
