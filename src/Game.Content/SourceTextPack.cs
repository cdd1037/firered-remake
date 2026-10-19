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
        "item-descriptions", "machine-descriptions", "display-labels", "summary-moves", "summary-abilities", "summary-memos", "summary-locations"
    };
    private readonly ImmutableDictionary<string, ImmutableDictionary<string, string>> groups;
    private SourceTextPack(ImmutableDictionary<string, ImmutableDictionary<string, string>> groups) => this.groups = groups;

    public static SourceTextPack Load(string? packDirectory)
    {
        var groups = ImmutableDictionary.CreateBuilder<string, ImmutableDictionary<string, string>>(StringComparer.Ordinal);
        if (packDirectory is null) return new(groups.ToImmutable());
        if (!Directory.Exists(packDirectory)) throw new DirectoryNotFoundException("Text pack does not exist: " + packDirectory);
        string source = Path.Combine(packDirectory, "source");
        if (!Directory.Exists(source)) return new(groups.ToImmutable());
        foreach (string path in Directory.EnumerateFiles(source, "*.json"))
        {
            string group = Path.GetFileNameWithoutExtension(path);
            if (!Groups.Contains(group)) throw new InvalidDataException("Unknown source text group: " + group);
            groups.Add(group, ContentJson.ParseStrict<ImmutableDictionary<string, string>>(
                File.ReadAllText(path), ContentJson.CreateOptions()));
        }
        return new(groups.ToImmutable());
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
            if (preserveControls) ValidateControls(original, text, group + "/" + id);
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

    private static readonly Regex Tokens = new(@"\{[^{}\\$\r\n]+\}|\\[npl]|\$|\n", RegexOptions.CultureInvariant);
    private static void ValidateControls(string original, string replacement, string id)
    {
        (string Token, bool EmptyBefore)[] Signature(string text)
        {
            var signature = new List<(string Token, bool EmptyBefore)>();
            int offset = 0;
            foreach (Match match in Tokens.Matches(text))
            {
                string literal = text[offset..match.Index];
                ValidateLiteral(literal);
                if (match.Value == "$" && match.Index != text.Length - 1 ||
                    match.Value != "\n" && match.Value.Any(char.IsControl))
                    throw new InvalidDataException("Invalid source text control: " + id);
                signature.Add((match.Value, literal.Length == 0));
                offset = match.Index + match.Length;
            }
            string tail = text[offset..];
            ValidateLiteral(tail);
            signature.Add(("", tail.Length == 0));
            return signature.ToArray();
        }
        void ValidateLiteral(string text)
        {
            if (text.Any(c => c is '{' or '}' or '\\' or '$' || char.IsControl(c)))
                throw new InvalidDataException("Invalid source text control: " + id);
        }
        // Keep exact operand spelling, source escapes, actual LF, and page-end/adjacency
        // anchors. This conservative contract also keeps placeholder order unchanged.
        if (!Signature(original).SequenceEqual(Signature(replacement)))
            throw new InvalidDataException("Source text controls differ: " + id);
    }
}
