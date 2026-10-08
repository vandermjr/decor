using System.Text;

namespace Decor.Core.Common;

public static class QuoteNotesRules
{
    public const int MaximumBytes = 65535;

    public static bool Fits(string? notes) =>
        notes is null || Encoding.UTF8.GetByteCount(notes) <= MaximumBytes;

    public static string Truncate(string? notes)
    {
        if (string.IsNullOrEmpty(notes)) return string.Empty;

        var byteCount = 0;
        var characterCount = 0;
        foreach (var rune in notes.EnumerateRunes())
        {
            if (byteCount + rune.Utf8SequenceLength > MaximumBytes)
                return notes[..characterCount];

            byteCount += rune.Utf8SequenceLength;
            characterCount += rune.Utf16SequenceLength;
        }

        return notes;
    }
}