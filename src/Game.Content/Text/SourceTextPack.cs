using System.Collections.Immutable;
using System.Text.RegularExpressions;

namespace Game.Content;

/// <summary>Optional source text overrides, selected once per process. Generated English owns IDs and controls.</summary>
public sealed class SourceTextPack
{
    private static readonly Lazy<SourceTextPack> Selected = new(() => Load(
        string.IsNullOrEmpty(Environment.GetEnvironmentVariable("FIRERED_TEXT_PACK")) ? null :
        Environment.GetEnvironmentVariable("FIRERED_TEXT_PACK")));
    public static SourceTextPack Current => Selected.Value;
    private static readonly HashSet<string> Groups = new(StringComparer.Ordinal) {
        "story", "story-menus", "battle", "species-names", "species-categories", "species-descriptions",
        "pokedex-ui", "item-names", "move-names", "type-names", "ability-names", "nature-names",
        "item-descriptions", "machine-descriptions", "display-labels", "summary-moves", "summary-abilities", "summary-memos", "summary-locations",
        "trainer-names", "trainer-class-names", "easy-chat-words", "easy-chat-groups",
        "help", "opening", "opening-names", "teachy-tv",
        "quest-log", "quest-log-map-names", "quest-log-departures", "map-section-names", "trainer-tower-names"
    };
    private readonly ImmutableDictionary<string, ImmutableDictionary<string, string>> groups;
    public string? FontDirectory { get; }
    private SourceTextPack(ImmutableDictionary<string, ImmutableDictionary<string, string>> groups, string? directory = null)
    {
        this.groups = groups;
        FontDirectory = directory is not null && Directory.Exists(Path.Combine(directory, "font"))
            ? Path.Combine(directory, "font") : null;
    }

    public static SourceTextPack Load(string? packDirectory)
    {
        var groups = ImmutableDictionary.CreateBuilder<string, ImmutableDictionary<string, string>>(StringComparer.Ordinal);
        if (packDirectory is null) return new(groups.ToImmutable());
        if (!Directory.Exists(packDirectory)) throw new DirectoryNotFoundException("Text pack does not exist: " + packDirectory);
        string source = Path.Combine(packDirectory, "source");
        if (!Directory.Exists(source)) return new(groups.ToImmutable(), packDirectory);
        foreach (string path in Directory.EnumerateFiles(source, "*.json"))
        {
            string group = Path.GetFileNameWithoutExtension(path);
            if (!Groups.Contains(group)) throw new InvalidDataException("Unknown source text group: " + group);
            groups.Add(group, ContentJson.ParseStrict<ImmutableDictionary<string, string>>(
                File.ReadAllText(path), ContentJson.CreateOptions()));
        }
        return new(groups.ToImmutable(), packDirectory);
    }

    public ImmutableDictionary<string, string> Apply(string group, IReadOnlyDictionary<string, string> originals,
        bool preserveControls = false)
    {
        if (!Groups.Contains(group)) throw new ArgumentException("Unknown source text group: " + group, nameof(group));
        var result = originals.ToImmutableDictionary(StringComparer.Ordinal);
        if (!groups.TryGetValue(group, out var replacements)) return result;
        foreach (var (id, text) in replacements)
        {
            if (!originals.TryGetValue(id, out var original)) throw new InvalidDataException("Unknown source text ID: " + group + "/" + id);
            if (string.IsNullOrWhiteSpace(text)) throw new InvalidDataException("Empty source text: " + group + "/" + id);
            if (preserveControls) ValidateControls(original, text, group + "/" + id, allowFormFeed: group == "opening");
        }
        return result.SetItems(replacements);
    }

    public PretStoryCompilation ApplyMenus(PretStoryCompilation compilation)
    {
        var labels = compilation.Menus.Values.SelectMany(menu => menu.Options)
            .GroupBy(option => option.TextSymbol, StringComparer.Ordinal)
            .ToImmutableDictionary(group => group.Key, group => group.Select(option => option.Text).Distinct().Single(), StringComparer.Ordinal);
        var translated = Apply("story-menus", labels, preserveControls: true);
        return compilation with { Menus = compilation.Menus.ToImmutableDictionary(pair => pair.Key,
            pair => pair.Value with { Options = pair.Value.Options.Select(option =>
                option with { Text = translated[option.TextSymbol] }).ToImmutableArray() }, StringComparer.Ordinal) };
    }

    private static readonly Regex Tokens = new(@"\{[^{}\\$\r\n]+\}|\\[npl]|\$|\n|\f", RegexOptions.CultureInvariant);
    private static void ValidateControls(string original, string replacement, string id, bool allowFormFeed)
    {
        string[] Signature(string text)
        {
            var signature = new List<string>();
            var values = new List<string>();
            int offset = 0;
            bool content = false;
            void FlushValues()
            {
                values.Sort(StringComparer.Ordinal);
                signature.Add(string.Join("|", values));
                values.Clear();
            }
            foreach (Match match in Tokens.Matches(text))
            {
                string literal = text[offset..match.Index];
                ValidateLiteral(literal);
                content |= literal.Length != 0;
                string token = match.Value;
                if (token == "$" && match.Index != text.Length - 1 ||
                    token != "\n" && !(allowFormFeed && token == "\f") && token.Any(char.IsControl))
                    throw new InvalidDataException("Invalid source text control: " + id);
                if (token is "\\n" or "\n")
                {
                    // Line wrapping belongs to the translated prose, not the script's effects.
                }
                else if (token is "{PKMN}" or "{PLUS}" or "{POKEBLOCK}" or "{LV_2}")
                {
                    // These are display glyph sequences, not parameter or execution controls.
                    content = true;
                }
                else if (IsValue(token))
                {
                    values.Add(token);
                    content = true;
                }
                else
                {
                    FlushValues();
                    signature.Add(token + (content ? ":text" : ":adjacent"));
                    content = false;
                }
                offset = match.Index + match.Length;
            }
            string tail = text[offset..];
            ValidateLiteral(tail);
            FlushValues();
            signature.Add(content || tail.Length != 0 ? "text" : "empty");
            return signature.ToArray();
        }
        void ValidateLiteral(string text)
        {
            if (text.Any(c => c is '{' or '}' or '\\' or '$' || char.IsControl(c)))
                throw new InvalidDataException("Invalid source text control: " + id);
        }
        // Values may change grammatical order within one control-delimited phrase. Their multiplicity
        // stays exact. Page/scroll waits, effects, operands, terminators and command adjacency stay exact.
        if (!Signature(original).SequenceEqual(Signature(replacement)))
            throw new InvalidDataException("Source text controls differ: " + id);
    }
    private static bool IsValue(string token)
    {
        if (token.Length < 3 || token[0] != '{') return false;
        string name = token[1..^1];
        return name.StartsWith("B_", StringComparison.Ordinal) || name.StartsWith("STR_VAR_", StringComparison.Ordinal) ||
            name.StartsWith("DYNAMIC ", StringComparison.Ordinal) ||
            name is "PLAYER" or "RIVAL" or "KUN" or "VERSION" or "EVIL_TEAM" or "GOOD_TEAM" or
                "EVIL_LEADER" or "GOOD_LEADER" or "EVIL_LEGENDARY" or "GOOD_LEGENDARY" or
                "UNKNOWN_STR" or "ATTACKING_TRAINER" or "DEFENDING_TRAINER" or "ATTACKING_MON" or "DEFENDING_MON";
    }

}
