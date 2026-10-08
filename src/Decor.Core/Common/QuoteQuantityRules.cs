namespace Decor.Core.Common;

public static class QuoteQuantityRules
{
    public const decimal Maximum = 999999999.999m;
    public const int DatabaseDecimalPlaces = 3;

    public static int DecimalPlaces(bool? allowsFraction) => allowsFraction == false ? 0 : DatabaseDecimalPlaces;

    public static bool IsValid(decimal quantity, int decimalPlaces) =>
        quantity > 0 && quantity <= Maximum && decimal.Round(quantity, decimalPlaces) == quantity;
}