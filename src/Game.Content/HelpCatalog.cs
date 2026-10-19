using System.Collections.Immutable;
using System.Text.RegularExpressions;

namespace Game.Content;

public sealed record HelpProgress(IReadOnlySet<string> Flags, IReadOnlySet<string> ItemIds,
    int KantoCaughtCount, string PlayerName);
public sealed record HelpEntry(string Id, string Question, string Answer);
public sealed record HelpTopic(string Id, string Label, string Description, ImmutableArray<HelpEntry> Entries);
internal sealed record HelpCondition(string Kind, string Value);
internal sealed record HelpEntryDefinition(string Id, string Question, string Answer, HelpCondition Condition);
internal sealed record HelpTopicDefinition(string Label, string Description, ImmutableArray<HelpEntryDefinition> Entries);
internal sealed record HelpContextTopic(string Id, ImmutableArray<string> Entries);
internal sealed record HelpMapContext(string Context, bool Dungeon, string DungeonFlag);
internal sealed record HelpDocument(int SchemaVersion, ImmutableDictionary<string, string> Texts,
    ImmutableArray<string> TopicOrder, ImmutableDictionary<string, HelpTopicDefinition> Topics,
    ImmutableDictionary<string, ImmutableArray<HelpContextTopic>> Contexts,
    ImmutableDictionary<string, HelpMapContext> Maps, ImmutableArray<string> BasicTerms,
    string BasicTermsFlag, ImmutableArray<string> HmFlags);

/// <summary>Immutable generated-only HELP catalog. Progression is supplied by the authoritative session.</summary>
public sealed class HelpCatalog
{
    public const string ResourcePath = "content/generated/ui/help/help.json";
    private readonly HelpDocument _document;
    private readonly string _billName, _someoneName;
    private readonly ImmutableDictionary<string, ImmutableDictionary<string, HelpEntryDefinition>> _entries;
    public string Greeting => _document.Texts["Help_Text_Greetings"];
    public string CancelLabel => _document.Texts["Help_Text_Cancel"];
    public IEnumerable<string> Contexts => _document.Contexts.Keys;

    private HelpCatalog(HelpDocument document, IReadOnlyDictionary<string, string> battleText)
    {
        _document = document;
        _billName = battleText["gString_Bill"];
        _someoneName = battleText["gString_Someone"];
        _entries = document.Topics.ToImmutableDictionary(p => p.Key,
            p => p.Value.Entries.ToImmutableDictionary(e => e.Id, StringComparer.Ordinal), StringComparer.Ordinal);
    }

    public static HelpCatalog Load(string projectRoot, SourceTextPack? pack = null,
        IReadOnlyDictionary<string, string>? battleText = null)
    {
        var d = ContentJson.ParseStrict<HelpDocument>(GeneratedResourceFile.Read(projectRoot, ResourcePath), ContentJson.CreateOptions());
        Require(d.SchemaVersion == 1 && d.Texts is { Count: > 0 } && d.Topics is { Count: > 0 } &&
            d.Contexts is { Count: > 0 } && d.Maps is { Count: > 0 }, "Incomplete HELP document.");
        Require(!d.TopicOrder.IsDefaultOrEmpty && d.TopicOrder.Distinct().Count() == d.TopicOrder.Length &&
            d.TopicOrder.ToHashSet(StringComparer.Ordinal).SetEquals(d.Topics.Keys), "Invalid HELP topic order.");
        Require(d.Texts.ContainsKey("Help_Text_Greetings") && d.Texts.ContainsKey("Help_Text_Cancel") &&
            d.Texts.All(p => !string.IsNullOrWhiteSpace(p.Key) && !string.IsNullOrWhiteSpace(p.Value)), "Invalid HELP text.");
        foreach (var (id, topic) in d.Topics)
        {
            Require(d.Texts.ContainsKey(topic.Label) && d.Texts.ContainsKey(topic.Description) && !topic.Entries.IsDefault,
                "Missing HELP topic text.");
            Require(topic.Entries.Select(e => e.Id).Distinct().Count() == topic.Entries.Length, "Duplicate HELP entry.");
            foreach (var e in topic.Entries)
            {
                Require(!string.IsNullOrWhiteSpace(e.Id) && d.Texts.ContainsKey(e.Question) && d.Texts.ContainsKey(e.Answer), "Missing HELP answer.");
                Require(e.Condition is not null && (e.Condition.Kind switch
                {
                    "always" or "anyHmFlag" => e.Condition.Value.Length == 0,
                    "flag" => e.Condition.Value.StartsWith("FLAG_", StringComparison.Ordinal),
                    "item" => e.Condition.Value.StartsWith("ITEM_", StringComparison.Ordinal),
                    "caughtAbove" => int.TryParse(e.Condition.Value, out var count) && count >= 0,
                    _ => false
                }), "Unsupported HELP progression condition.");
            }
        }
        foreach (var topics in d.Contexts.Values)
        {
            Require(!topics.IsDefault && topics.Select(t => t.Id).Distinct().Count() == topics.Length, "Invalid HELP context.");
            foreach (var topic in topics)
                Require(d.Topics.TryGetValue(topic.Id, out var definition) && !topic.Entries.IsDefault &&
                    topic.Entries.Distinct().Count() == topic.Entries.Length &&
                    topic.Entries.All(id => definition.Entries.Any(e => e.Id == id)), "Unknown HELP context entry.");
        }
        Require(!d.BasicTerms.IsDefaultOrEmpty && d.BasicTerms.Distinct().Count() == d.BasicTerms.Length &&
            d.BasicTerms.All(id => d.Topics["TOPIC_TERMS"].Entries.Any(e => e.Id == id)) &&
            d.BasicTermsFlag.StartsWith("FLAG_", StringComparison.Ordinal) && !d.HmFlags.IsDefaultOrEmpty &&
            d.HmFlags.All(f => f.StartsWith("FLAG_", StringComparison.Ordinal)), "Invalid HELP term progression.");
        Require(d.Maps.All(p => p.Key.StartsWith("MAP_", StringComparison.Ordinal) && d.Contexts.ContainsKey(p.Value.Context) &&
            (p.Value.DungeonFlag.Length == 0 || p.Value.Dungeon && p.Value.DungeonFlag.StartsWith("FLAG_", StringComparison.Ordinal))), "Invalid HELP map context.");
        var references = d.Topics.Values.SelectMany(topic => new[] { topic.Label, topic.Description }
                .Concat(topic.Entries.SelectMany(entry => new[] { entry.Question, entry.Answer })))
            .Append("Help_Text_Greetings").Append("Help_Text_Cancel").Distinct(StringComparer.Ordinal)
            .ToImmutableDictionary(id => id, id => d.Texts[id], StringComparer.Ordinal);
        var selected = pack ?? SourceTextPack.Current;
        d = d with { Texts = d.Texts.SetItems(selected.Apply("help", references, preserveControls: true)) };
        return new(d, battleText ?? PretBattleText.Load(projectRoot, selected));
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidDataException(message);
    }

    public bool SupportsContext(string contextId) => _document.Contexts.TryGetValue(contextId, out var topics) && !topics.IsEmpty;

    public string ResolveMapContext(string mapId, bool surfing, IReadOnlySet<string> flags)
    {
        if (surfing) return "HELPCONTEXT_SURFING";
        if (!_document.Maps.TryGetValue(mapId, out var map)) throw new ArgumentException("Unknown HELP map: " + mapId, nameof(mapId));
        return map.Dungeon && (map.DungeonFlag.Length == 0 || flags.Contains(map.DungeonFlag))
            ? "HELPCONTEXT_DUNGEON" : map.Context;
    }

    public ImmutableArray<HelpTopic> Topics(string contextId, HelpProgress progress)
    {
        ArgumentNullException.ThrowIfNull(progress);
        if (progress.KantoCaughtCount < 0) throw new ArgumentOutOfRangeException(nameof(progress));
        if (!_document.Contexts.TryGetValue(contextId, out var contexts)) throw new ArgumentException("Unknown HELP context: " + contextId, nameof(contextId));
        bool basicTerms = progress.Flags.Contains(_document.BasicTermsFlag);
        string Text(string key) => Regex.Replace(_document.Texts[key], @"\{(PLAYER|STR_VAR_1)\}",
            match => match.Groups[1].Value == "PLAYER" ? progress.PlayerName :
                progress.Flags.Contains("FLAG_SYS_NOT_SOMEONES_PC") ? _billName : _someoneName);
        bool Enabled(HelpCondition condition) => condition.Kind switch
        {
            "always" => true,
            "flag" => progress.Flags.Contains(condition.Value),
            "item" => progress.ItemIds.Contains(condition.Value),
            "anyHmFlag" => _document.HmFlags.Any(progress.Flags.Contains),
            "caughtAbove" => progress.KantoCaughtCount > int.Parse(condition.Value, System.Globalization.CultureInfo.InvariantCulture),
            _ => throw new InvalidOperationException("Unvalidated HELP condition.")
        };
        return contexts.Select(context =>
        {
            var definition = _document.Topics[context.Id];
            var entries = _entries[context.Id];
            var ids = context.Entries.Where(id => !(basicTerms && context.Id == "TOPIC_TERMS" && _document.BasicTerms.Contains(id)) && Enabled(entries[id].Condition));
            if (basicTerms && context.Id == "TOPIC_TERMS") ids = ids.Concat(_document.BasicTerms);
            return new HelpTopic(context.Id, Text(definition.Label), Text(definition.Description),
                ids.Select(id => new HelpEntry(id, Text(entries[id].Question), Text(entries[id].Answer))).ToImmutableArray());
        }).ToImmutableArray();
    }
}
