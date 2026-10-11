using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Decor.Core.Common;

public sealed record IntelligentSearchQuery(IReadOnlyList<string> Tokens)
{
    private static readonly Regex Parts = new("\"(?<phrase>[^\"]*)(?:\"|$)|(?<text>[^\"]+)", RegexOptions.CultureInvariant);

    public bool IsIdSearch => Tokens.Count == 1 && Tokens[0].Length > 0
        && Tokens[0].All(character => character is >= '0' and <= '9');

    public int? Id => IsIdSearch
        && int.TryParse(Tokens[0], NumberStyles.None, CultureInfo.InvariantCulture, out var id)
        ? id : null;

    public bool TryGetIntegerId(out int id)
    {
        id = default;
        return Tokens.Count == 1
            && int.TryParse(Tokens[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out id);
    }

    public static IntelligentSearchQuery Parse(string? search)
    {
        var tokens = new List<string>();
        foreach (Match part in Parts.Matches(search ?? string.Empty))
        {
            if (part.Groups["phrase"].Success)
            {
                var phrase = part.Groups["phrase"].Value.Trim();
                if (phrase.Length > 0)
                    tokens.Add(phrase);
            }
            else
                tokens.AddRange(part.Value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        }
        return new IntelligentSearchQuery(tokens.AsReadOnly());
    }

    internal static string TransformUnquotedText(string? search, Func<string, string> transform)
    {
        var result = new StringBuilder();
        foreach (Match part in Parts.Matches(search ?? string.Empty))
            result.Append(part.Groups["phrase"].Success ? part.Value : transform(part.Value));
        return result.ToString();
    }
}