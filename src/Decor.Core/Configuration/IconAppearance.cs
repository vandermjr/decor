using System;
using System.Collections.Generic;
using System.Linq;

namespace Decor.Core.Configuration;

public sealed record IconAppearance(int MaterialSymbolWeight, double StrokeThickness, IReadOnlyDictionary<string, string> IconColors)
{
    public IconAppearance(int materialSymbolWeight, double strokeThickness)
        : this(materialSymbolWeight, strokeThickness, new Dictionary<string, string>(StringComparer.Ordinal))
    {
    }

    public static IconAppearance Default { get; } = new(400, 1.1, new Dictionary<string, string>(StringComparer.Ordinal));

    public string? GetColor(string? iconId)
    {
        if (string.IsNullOrWhiteSpace(iconId))
            return null;

        return IconColors.TryGetValue(iconId, out var color) ? color : null;
    }

    public bool Equals(IconAppearance? other)
    {
        if (ReferenceEquals(this, other))
            return true;

        if (other is null)
            return false;

        if (MaterialSymbolWeight != other.MaterialSymbolWeight || Math.Abs(StrokeThickness - other.StrokeThickness) > 0.0001)
            return false;

        if (IconColors.Count != other.IconColors.Count)
            return false;

        foreach (var pair in IconColors)
        {
            if (!other.IconColors.TryGetValue(pair.Key, out var value) || !string.Equals(pair.Value, value, StringComparison.Ordinal))
                return false;
        }

        return true;
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(MaterialSymbolWeight);
        hash.Add(StrokeThickness);

        foreach (var pair in IconColors.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
        {
            hash.Add(pair.Key, StringComparer.Ordinal);
            hash.Add(pair.Value, StringComparer.Ordinal);
        }

        return hash.ToHashCode();
    }
}
