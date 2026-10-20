using System.Collections.Immutable;
using System.Text;
using Game.Domain.Events;
using Game.Domain.Pokemon;

namespace Game.Content;

public enum FameCheckerTextColor { DarkGray = 2, LightGray = 3, Blue = 8, LightBlue = 9 }
public enum FameCheckerTextFont { Normal, Male, Female }
public enum FameCheckerTextEndAction { End, Clear, Scroll }
public readonly record struct FameCheckerTextStyle(FameCheckerTextColor Foreground, FameCheckerTextColor Shadow,
    FameCheckerTextFont Font);

/// <summary>Column and CharacterStart are UTF-16 offsets, not pixel or glyph counts.</summary>
public sealed record FameCheckerTextRun(int LineIndex, int Column, int CharacterStart, string Text,
    FameCheckerTextStyle Style);
public sealed record FameCheckerTextLine(int LineIndex, int CharacterStart, string Text,
    ImmutableArray<FameCheckerTextRun> Runs);

/// <summary>A complete two-row window at one prompt. A scrolled top row is already visible;
/// feed only RevealText into SourceTextProgress and add RetainedTextLength when clipping runs.</summary>
public sealed record FameCheckerTextPage(string Text, ImmutableArray<FameCheckerTextLine> Lines,
    int RetainedTextLength, FameCheckerTextEndAction EndAction)
{
    public string RevealText => Text[RetainedTextLength..];
    /// <summary>Zero-width source formatting commands, in RevealText UTF-16 offsets; duplicates matter.</summary>
    public ImmutableArray<int> RepeatControlOffsets { get; init; } = [];
}

/// <summary>Bounded layout decoder for the authenticated English Fame Checker bodies, not a general
/// source-text engine. Preserves prompt boundaries, color/font state and zero-width printer commands.
/// Frame progression and scrolling belong to the presentation layer.</summary>
public static class FameCheckerTextPages
{
    public static string Plain(FameCheckerText source, string playerName, string rivalName)
    {
        var pages = Parse(source.EncodedText.EndsWith('$') ? source : source with { EncodedText = source.EncodedText + "$" },
            playerName, rivalName);
        if (pages.Length != 1 || pages[0].EndAction != FameCheckerTextEndAction.End)
            throw new InvalidDataException("Fame Checker label must be one unprompted page.");
        return pages[0].Text;
    }

    public const int MaximumEncodedLength = 16384;
    public const int MaximumNameLength = 64;
    public const int MaximumLineLength = 1024;
    public const int MaximumPages = 64;

    /// <summary>Accepts only \n, \p, \l, the four catalog color/shadow controls, the two gender font
    /// controls, PLAYER/RIVAL and terminal $. Styles survive both clear and scroll prompts.
    /// The source font importer verifies the English male/female bitmap and widths equal normal;
    /// callers can draw every admitted Font with the same normal atlas.</summary>
    public static ImmutableArray<FameCheckerTextPage> Parse(FameCheckerText source, string playerName, string rivalName)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(source.EncodedText);
        ValidateName(playerName, nameof(playerName));
        ValidateName(rivalName, nameof(rivalName));
        string encoded = source.EncodedText;
        if (encoded.Length is 0 or > MaximumEncodedLength)
            throw Error(source, 0, "body length is outside the supported bound");
        ValidateUnicode(encoded, nameof(source));
        var pages = ImmutableArray.CreateBuilder<FameCheckerTextPage>();
        var lines = new List<LineBuffer> { new() };
        var style = new FameCheckerTextStyle(FameCheckerTextColor.DarkGray, FameCheckerTextColor.LightGray,
            FameCheckerTextFont.Normal);
        int retainedLength = 0;
        var repeatControls = ImmutableArray.CreateBuilder<int>();

        void Append(string value, int offset)
        {
            if (lines[^1].Text.Length + value.Length > MaximumLineLength)
                throw Error(source, offset, "line length is outside the supported bound");
            lines[^1].Append(value, style);
        }

        FameCheckerTextPage Finish(FameCheckerTextEndAction action, int offset)
        {
            if (pages.Count >= MaximumPages) throw Error(source, offset, "too many prompt pages");
            var output = ImmutableArray.CreateBuilder<FameCheckerTextLine>();
            int start = 0;
            for (int row = 0; row < lines.Count; row++)
            {
                var line = lines[row];
                var runs = ImmutableArray.CreateBuilder<FameCheckerTextRun>();
                int column = 0;
                foreach (var run in line.Runs)
                {
                    string value = run.Text.ToString();
                    runs.Add(new(row, column, start + column, value, run.Style));
                    column += value.Length;
                }
                output.Add(new(row, start, line.Text.ToString(), runs.ToImmutable()));
                start += line.Text.Length + 1;
            }
            var page = new FameCheckerTextPage(string.Join('\n', output.Select(l => l.Text)),
                output.ToImmutable(), retainedLength, action) { RepeatControlOffsets = repeatControls.ToImmutable() };
            pages.Add(page);
            repeatControls.Clear();
            return page;
        }

        for (int offset = 0; offset < encoded.Length;)
        {
            int start = offset;
            char value = encoded[offset++];
            if (value == '$')
            {
                if (offset != encoded.Length) throw Error(source, start, "text follows terminal $");
                Finish(FameCheckerTextEndAction.End, start);
                return pages.ToImmutable();
            }
            if (value == '\\')
            {
                if (offset == encoded.Length) throw Error(source, start, "unfinished escape");
                switch (encoded[offset++])
                {
                    case 'n':
                        if (lines.Count != 1) throw Error(source, start, "newline exceeds the two-row window");
                        lines.Add(new());
                        break;
                    case 'p':
                        Finish(FameCheckerTextEndAction.Clear, start);
                        lines = [new()];
                        retainedLength = 0;
                        break;
                    case 'l':
                        if (lines.Count != 2) throw Error(source, start, "scroll requires an existing bottom row");
                        Finish(FameCheckerTextEndAction.Scroll, start);
                        lines = [lines[1], new()];
                        retainedLength = lines[0].Text.Length + 1;
                        break;
                    default:
                        throw Error(source, start, "unsupported escape " + encoded[start..offset]);
                }
                continue;
            }
            if (value == '{')
            {
                int end = encoded.IndexOf('}', offset);
                if (end < 0) throw Error(source, start, "unfinished control");
                string control = encoded[start..(end + 1)];
                switch (control)
                {
                    case "{COLOR BLUE}": style = style with { Foreground = FameCheckerTextColor.Blue }; break;
                    case "{COLOR DARK_GRAY}": style = style with { Foreground = FameCheckerTextColor.DarkGray }; break;
                    case "{SHADOW LIGHT_BLUE}": style = style with { Shadow = FameCheckerTextColor.LightBlue }; break;
                    case "{SHADOW LIGHT_GRAY}": style = style with { Shadow = FameCheckerTextColor.LightGray }; break;
                    case "{FONT_MALE}": style = style with { Font = FameCheckerTextFont.Male }; break;
                    case "{FONT_FEMALE}": style = style with { Font = FameCheckerTextFont.Female }; break;
                    case "{PLAYER}": Append(playerName, start); break;
                    case "{RIVAL}": Append(rivalName, start); break;
                    default: throw Error(source, start, "unsupported control " + control);
                }
                if (control is not ("{PLAYER}" or "{RIVAL}"))
                    repeatControls.Add(lines.Sum(line => line.Text.Length) + lines.Count - 1 - retainedLength);
                offset = end + 1;
                continue;
            }
            if (value == '}' || char.IsControl(value)) throw Error(source, start, "unsupported literal character");
            // Consume an ordinary span at once. Names are appended literally, never reparsed as source controls.
            while (offset < encoded.Length && encoded[offset] is not ('\\' or '{' or '}' or '$') && !char.IsControl(encoded[offset])) offset++;
            Append(encoded[start..offset], start);
        }
        throw Error(source, encoded.Length, "missing terminal $");
    }

    private static void ValidateName(string name, string parameter)
    {
        ArgumentNullException.ThrowIfNull(name, parameter);
        if (NameTextRules.TextElementCount(name) is 0 or > MaximumNameLength || name.Any(char.IsControl))
            throw new ArgumentException("Name must be bounded plain text without control characters.", parameter);
        ValidateUnicode(name, parameter);
    }

    private static void ValidateUnicode(string text, string parameter)
    {
        for (int index = 0; index < text.Length; index++)
        {
            if (!char.IsSurrogate(text[index])) continue;
            if (!char.IsHighSurrogate(text[index]) || index + 1 == text.Length || !char.IsLowSurrogate(text[index + 1]))
                throw new ArgumentException("Text must contain valid Unicode.", parameter);
            index++;
        }
    }

    private static InvalidDataException Error(FameCheckerText source, int offset, string reason) =>
        new($"Fame Checker text {source.Symbol} at offset {offset}: {reason}.");

    private sealed class LineBuffer
    {
        public StringBuilder Text { get; } = new();
        public List<(StringBuilder Text, FameCheckerTextStyle Style)> Runs { get; } = [];
        public void Append(string text, FameCheckerTextStyle style)
        {
            if (text.Length == 0) return;
            Text.Append(text);
            if (Runs.Count == 0 || Runs[^1].Style != style) Runs.Add((new(), style));
            Runs[^1].Text.Append(text);
        }
    }
}
