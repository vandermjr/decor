using System.Text;
using Decor.Core.Common;
using Decor.Core.DTOs;
using Decor.Core.Validation;

namespace Decor.Application.Tests;

public class QuoteNotesRulesTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void EmptyNotes_FitAndTruncateToEmpty(string? notes)
    {
        Assert.True(QuoteNotesRules.Fits(notes));
        Assert.Equal(string.Empty, QuoteNotesRules.Truncate(notes));
    }

    [Fact]
    public void Ascii_UsesFullTextByteLimit()
    {
        Assert.Equal(65535, QuoteNotesRules.MaximumBytes);
        var maximum = new string('x', 65535);
        Assert.True(QuoteNotesRules.Fits(maximum));
        Assert.Same(maximum, QuoteNotesRules.Truncate(maximum));
        Assert.False(QuoteNotesRules.Fits(maximum + "x"));
        Assert.Equal(maximum, QuoteNotesRules.Truncate(maximum + "x"));
    }

    [Theory]
    [InlineData("\u00e9", 32767, 65534)]
    [InlineData("\u20ac", 21845, 65535)]
    [InlineData("\U0001f600", 16383, 65532)]
    public void Unicode_TruncatesOnlyAtRuneBoundary(string rune, int count, int expectedBytes)
    {
        var maximum = string.Concat(Enumerable.Repeat(rune, count));
        Assert.True(QuoteNotesRules.Fits(maximum));
        Assert.Same(maximum, QuoteNotesRules.Truncate(maximum));
        Assert.False(QuoteNotesRules.Fits(maximum + rune));

        var truncated = QuoteNotesRules.Truncate(maximum + rune);

        Assert.Equal(maximum, truncated);
        Assert.Equal(expectedBytes, Encoding.UTF8.GetByteCount(truncated));
        Assert.Equal(count, truncated.EnumerateRunes().Count());
    }

    [Theory]
    [InlineData("x", 65535)]
    [InlineData("\u00e9", 32767)]
    [InlineData("\u20ac", 21845)]
    [InlineData("\U0001f600", 16383)]
    public void Validator_RejectsExcessBytesWithoutChangingNotes(string rune, int count)
    {
        var notes = string.Concat(Enumerable.Repeat(rune, count));
        var dto = new QuoteDTO(0, 1, 1, null, 1, DateTime.UtcNow, notes);
        var validator = new QuoteDTOValidator();
        Assert.Empty(validator.Validate(dto));
        var oversized = dto with { Notes = notes + rune };

        var error = Assert.Single(validator.Validate(oversized));

        Assert.Contains("65535 bytes", error);
        Assert.Contains("UTF-8", error);
        Assert.Equal(notes + rune, oversized.Notes);
    }
}