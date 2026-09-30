namespace Decor.Core.Configuration;

public sealed record IconAppearance(int MaterialSymbolWeight, double StrokeThickness)
{
    public static IconAppearance Default { get; } = new(400, 1.1);
}
