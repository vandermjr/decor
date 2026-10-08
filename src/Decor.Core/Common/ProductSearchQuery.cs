using System.Globalization;
using System.Text.RegularExpressions;
using Decor.Core.Entities;

namespace Decor.Core.Common;

public sealed record ProductSearchQuery(IReadOnlyList<string> Tokens, decimal? StockGreaterThan, ProductType? Type,
    decimal? StockLessThan = null, decimal? StockEquals = null)
{
    public ProductType CatalogProductType => ProductType.Good;
    public bool IsProductIdSearch => new IntelligentSearchQuery(Tokens).IsIdSearch;
    public int? ProductID => new IntelligentSearchQuery(Tokens).Id;

    private static readonly Regex TypeFilter = new(@"(?<!\S)tipo:(produto|servico|serviço)(?!\S)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex StockFilter = new(@"(?<!\S)(?:(?<negative>(?:com\s+)?estoque\s+negativo)|(?<zero>sem\s+estoque)|com\s+estoque|estoque\s*(?<operator>acima\s+de|abaixo\s+de|[><=])\s*(?<quantity>[+-]?\d+(?:[.,]\d+)?))(?!\S)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static ProductSearchQuery Parse(string? search)
    {
        ProductType? type = null;
        decimal? stockGreaterThan = null;
        decimal? stockLessThan = null;
        decimal? stockEquals = null;
        var text = IntelligentSearchQuery.TransformUnquotedText(search, fragment =>
        {
            var filtered = TypeFilter.Replace(fragment, match =>
            {
                var parsedType = match.Groups[1].Value.Equals("produto", StringComparison.OrdinalIgnoreCase)
                    ? ProductType.Good : ProductType.Service;
                if (type.HasValue && type != parsedType)
                    throw new ArgumentException("Filtros de tipo conflitantes.", nameof(search));
                type = parsedType;
                return " ";
            });
            return StockFilter.Replace(filtered, match =>
            {
                var quantity = match.Groups["quantity"];
                var value = 0m;
                if (quantity.Success && !decimal.TryParse(quantity.Value.Replace(',', '.'),
                    NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value))
                    return match.Value;
                var comparison = match.Groups["operator"].Value;
                if (match.Groups["zero"].Success || comparison == "=")
                {
                    if (stockEquals.HasValue && stockEquals != value)
                        throw new ArgumentException("Filtros de estoque conflitantes.", nameof(search));
                    stockEquals = value;
                }
                else if (match.Groups["negative"].Success || comparison == "<"
                    || comparison.StartsWith("abaixo", StringComparison.OrdinalIgnoreCase))
                    stockLessThan = stockLessThan.HasValue ? Math.Min(stockLessThan.Value, value) : value;
                else
                    stockGreaterThan = stockGreaterThan.HasValue ? Math.Max(stockGreaterThan.Value, value) : value;
                return " ";
            });
        });
        if ((stockGreaterThan.HasValue && stockLessThan.HasValue && stockGreaterThan >= stockLessThan)
            || (stockEquals.HasValue && ((stockGreaterThan.HasValue && stockEquals <= stockGreaterThan)
                || (stockLessThan.HasValue && stockEquals >= stockLessThan))))
            throw new ArgumentException("Filtros de estoque conflitantes.", nameof(search));
        return new ProductSearchQuery(IntelligentSearchQuery.Parse(text).Tokens,
            stockGreaterThan, type, stockLessThan, stockEquals);
    }
}