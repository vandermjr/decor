using System.Globalization;

namespace Decor.AvaloniaUI.ViewModels;

internal static class RecordCodeDisplay
{
    private const int CountPageSize = 500;
    private static readonly CultureInfo BrazilianCulture = CultureInfo.GetCultureInfo("pt-BR");

    public static async Task<int> CountAllAsync<T>(Func<int, int, Task<IEnumerable<T>>> loadPage)
    {
        var total = 0;
        var page = 1;
        while (true)
        {
            var records = (await loadPage(page, CountPageSize)).ToArray();
            total += records.Length;
            if (records.Length < CountPageSize)
                return total;
            page++;
        }
    }

    public static string ForNewRecord(int recordCount)
    {
        var digitCount = Math.Max(1, Math.Max(0, recordCount).ToString(CultureInfo.InvariantCulture).Length);
        var digits = new string('0', digitCount);
        var firstGroupLength = digitCount % 3;

        if (firstGroupLength == 0)
            firstGroupLength = 3;

        return string.Join(".", Enumerable.Range(0, (digitCount + 2) / 3)
            .Select(group => digits.Substring(group == 0 ? 0 : firstGroupLength + (group - 1) * 3,
                group == 0 ? firstGroupLength : 3)));
    }

    public static string ForExistingRecord(int recordId) => recordId.ToString("N0", BrazilianCulture);
}