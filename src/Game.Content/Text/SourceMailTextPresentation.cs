using System.Collections.Immutable;
using Game.Domain.Events;

namespace Game.Content;

/// <summary>Mail and Easy Chat UI reuse the selected battle bank and its single-pass control adapter.</summary>
public sealed class SourceMailTextPresentation
{
    private static readonly ImmutableDictionary<string, string> NoValues = ImmutableDictionary<string, string>.Empty;
    private readonly IReadOnlyDictionary<string, string> source;
    public ImmutableArray<string> EditorActionLabels { get; }

    public SourceMailTextPresentation(IReadOnlyDictionary<string, string> source)
    {
        this.source = source;
        var separators = new Dictionary<string, string>
        {
            ["CLEAR_TO 0x57"] = "\n", ["CLEAR_TO 0xA4"] = "\n"
        };
        EditorActionLabels = SourceBattleText.Pages(source, "gText_DelAllCancelOk", separators)
            .Single().Split('\n').ToImmutableArray();
        if (EditorActionLabels.Length != 3 || EditorActionLabels.Any(string.IsNullOrWhiteSpace))
            throw new InvalidDataException("easy_chat_editor_action_labels");
    }

    public string Text(string symbol, string? author = null) => SourceBattleText.Pages(source, symbol,
        author is null ? NoValues : new Dictionary<string, string> { ["STR_VAR_1"] = author }).Single();
}
