using System.Globalization;
using System.Text;

namespace Game.Domain.Pokemon;

/// <summary>Literal player, rival, OT and custom Pokemon names. Limits count user-visible
/// text elements; stored identity stays exactly as entered, without normalization or encoding.</summary>
public static class NameTextRules
{
    public const int TrainerNameLength = 7;
    public const int NicknameLength = 10;
    // Defensive bound for pathological combining sequences, shared with the native editor.
    public const int MaximumScalarCount = 256;

    public static int TextElementCount(string value) => new StringInfo(value).LengthInTextElements;

    public static bool IsValidTrainerName(string? value) => IsValidName(value, TrainerNameLength);

    public static bool IsValidName(string? value, int maximumLength) =>
        !string.IsNullOrWhiteSpace(value) && HasValidCharacters(value) && IsWithinLength(value, maximumLength);

    public static bool IsWithinLength(string value, int maximumLength)
    {
        int scalars = 0;
        foreach (var rune in value.EnumerateRunes())
            if (++scalars > MaximumScalarCount) return false;
        return TextElementCount(value) <= maximumLength;
    }

    public static bool HasValidCharacters(string value)
    {
        bool visible = false;
        for (int index = 0; index < value.Length;)
        {
            if (!Rune.TryGetRuneAt(value, index, out var rune)) return false;
            index += rune.Utf16SequenceLength;
            // Names are literals, never source text commands, line breaks or bidi controls.
            // Joiners are needed for ordinary Unicode grapheme clusters (including emoji).
            var category = Rune.GetUnicodeCategory(rune);
            if (category is UnicodeCategory.Control or UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator ||
                category == UnicodeCategory.Format && rune.Value is not (0x200C or 0x200D) ||
                rune.Value is '{' or '}' or '\\' or '$') return false;
            if (!Rune.IsWhiteSpace(rune) && category is not (UnicodeCategory.Format or
                UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark)) visible = true;
        }
        return visible || string.IsNullOrWhiteSpace(value);
    }

    public static string Truncate(string value, int maximumLength)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maximumLength);
        var starts = StringInfo.ParseCombiningCharacters(value);
        return starts.Length <= maximumLength ? value : value[..starts[maximumLength]];
    }
}
