using System.Collections.Immutable;
using System.Text;

/// <summary>Selection-only source text. Layout controls never determine action identity;
/// event dialogue continues to use its separate battle page adapter.</summary>
internal sealed class SourceBattleSelectionText
{
    private enum Parameter { Literal, ActiveName, PlayerName }
    private readonly record struct Part(string Text, Parameter Kind = Parameter.Literal);
    private readonly ImmutableArray<Part> _prompt;

    internal ImmutableArray<string> ActionLabels { get; }
    internal string FoePrefix { get; }
    internal string WildPrefix { get; }
    internal string PpLabel { get; }
    internal string TypeLabel { get; }

    internal SourceBattleSelectionText(IReadOnlyDictionary<string, string> source)
    {
        _prompt = ParsePrompt(Read(source, "gText_WhatWillPkmnDo"));
        ActionLabels = ParseMenu(Read(source, "gText_BattleMenu"));
        FoePrefix = Plain(source, "sText_FoePkmnPrefix");
        WildPrefix = Plain(source, "sText_WildPkmnPrefix");
        PpLabel = Plain(source, "gText_MoveInterfacePP");
        TypeLabel = Plain(source, "gText_MoveInterfaceType");
    }

    internal string ActionPrompt(string activeName, string playerName)
    {
        ArgumentNullException.ThrowIfNull(activeName);
        ArgumentNullException.ThrowIfNull(playerName);
        var text = new StringBuilder();
        // Parse only the source. Inserted names may contain braces or source-looking escapes.
        foreach (var part in _prompt)
            text.Append(part.Kind switch
            {
                Parameter.ActiveName => activeName,
                Parameter.PlayerName => playerName,
                _ => part.Text
            });
        return text.ToString();
    }

    private static string Read(IReadOnlyDictionary<string, string> source, string symbol) =>
        source.TryGetValue(symbol, out var text) && text is not null ? text :
            throw Invalid(symbol);

    private static string Plain(IReadOnlyDictionary<string, string> source, string symbol)
    {
        int offset = 0;
        return ReadCell(Read(source, symbol), symbol, ref offset, null);
    }

    private static ImmutableArray<string> ParseMenu(string source)
    {
        const string symbol = "gText_BattleMenu";
        // These are the known source menu colors and two-column layout, already drawn by
        // the battle skin. Require the exact structure rather than stripping arbitrary braces.
        const string header = "{PALETTE 5}{COLOR_HIGHLIGHT_SHADOW 13 14 15}";
        if (!source.StartsWith(header, StringComparison.Ordinal)) throw Invalid(symbol);
        int offset = header.Length;
        ImmutableArray<string> cells =
        [
            ReadCell(source, symbol, ref offset, "{CLEAR_TO 56}"),
            ReadCell(source, symbol, ref offset, "\\n"),
            ReadCell(source, symbol, ref offset, "{CLEAR_TO 56}"),
            ReadCell(source, symbol, ref offset, null)
        ];
        if (cells.Any(string.IsNullOrWhiteSpace)) throw Invalid(symbol);
        return cells;
    }

    private static string ReadCell(string source, string symbol, ref int offset, string? end)
    {
        var text = new StringBuilder();
        while (offset < source.Length)
        {
            if (end is not null && source.AsSpan(offset).StartsWith(end, StringComparison.Ordinal))
            {
                offset += end.Length;
                return text.ToString();
            }
            char value = source[offset++];
            if (value is '{' or '}' or '\r' or '\n') throw Invalid(symbol);
            if (value == '\\')
            {
                if (offset == source.Length || source[offset] is not ('"' or '\\')) throw Invalid(symbol);
                value = source[offset++];
            }
            text.Append(value);
        }
        if (end is not null) throw Invalid(symbol);
        return text.ToString();
    }

    private static ImmutableArray<Part> ParsePrompt(string source)
    {
        const string symbol = "gText_WhatWillPkmnDo";
        var parts = ImmutableArray.CreateBuilder<Part>();
        var literal = new StringBuilder();
        for (int offset = 0; offset < source.Length; offset++)
        {
            char value = source[offset];
            if (value == '{')
            {
                int end = source.IndexOf('}', offset + 1);
                if (end < 0) throw Invalid(symbol);
                var parameter = source[(offset + 1)..end] switch
                {
                    "B_ACTIVE_NAME_WITH_PREFIX" => Parameter.ActiveName,
                    "PLAYER" or "B_PLAYER_NAME" => Parameter.PlayerName,
                    _ => throw Invalid(symbol)
                };
                if (literal.Length > 0) { parts.Add(new(literal.ToString())); literal.Clear(); }
                parts.Add(new("", parameter));
                offset = end;
                continue;
            }
            if (value is '}' or '\r' or '\n') throw Invalid(symbol);
            if (value == '\\')
            {
                if (++offset == source.Length) throw Invalid(symbol);
                value = source[offset] switch
                {
                    'n' => '\n',
                    '"' => '"',
                    '\\' => '\\',
                    _ => throw Invalid(symbol)
                };
            }
            literal.Append(value);
        }
        if (literal.Length > 0) parts.Add(new(literal.ToString()));
        if (parts.Count == 0) throw Invalid(symbol);
        return parts.ToImmutable();
    }

    private static InvalidDataException Invalid(string symbol) =>
        new("Unsupported battle selection text or control: " + symbol);
}
