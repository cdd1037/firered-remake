using System.Collections.Immutable;
using System.Text.RegularExpressions;

namespace Game.Content;

/// <summary>Authored presentation messages. Source script controls remain in their existing text readers.</summary>
public sealed class PresentationTextCatalog
{
    private static readonly Regex Parameter = new(@"\{([A-Za-z][A-Za-z0-9]*)\}", RegexOptions.CultureInvariant);
    private readonly ImmutableDictionary<string, string> _messages;

    private PresentationTextCatalog(ImmutableDictionary<string, string> messages)
    {
        if (messages.IsEmpty) throw new InvalidDataException("Presentation text catalog is empty.");
        foreach (var (id, text) in messages)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(text))
                throw new InvalidDataException("Presentation text ID and value must be nonempty.");
            var literal = Parameter.Replace(text, "");
            if (literal.Contains('{') || literal.Contains('}'))
                throw new InvalidDataException("Invalid presentation text parameter: " + id);
        }
        _messages = messages;
    }

    public static PresentationTextCatalog Parse(string json) => new(
        ContentJson.ParseStrict<ImmutableDictionary<string, string>>(json, ContentJson.CreateOptions()));

    public static PresentationTextCatalog English(string group)
    {
        using var stream = typeof(PresentationTextCatalog).Assembly.GetManifestResourceStream("Game.Content.Text.en." + group + ".json")
            ?? throw new InvalidDataException("Missing English presentation text group: " + group);
        using var reader = new StreamReader(stream);
        return Parse(reader.ReadToEnd());
    }

    /// <summary>Optional local overrides are partial; English remains the authority for IDs and parameter names.</summary>
    public static PresentationTextCatalog Load(string group, string? overridesDirectory = null)
    {
        var english = English(group);
        if (overridesDirectory is null) return english;
        if (!Directory.Exists(overridesDirectory))
            throw new DirectoryNotFoundException("Presentation text pack does not exist: " + overridesDirectory);
        foreach (string file in Directory.EnumerateFiles(overridesDirectory, "*.json"))
            if (typeof(PresentationTextCatalog).Assembly.GetManifestResourceInfo(
                "Game.Content.Text.en." + Path.GetFileNameWithoutExtension(file) + ".json") is null)
                throw new InvalidDataException("Unknown presentation text group: " + Path.GetFileName(file));
        string path = Path.Combine(overridesDirectory, group + ".json");
        if (!File.Exists(path)) return english;
        var replacements = ContentJson.ParseStrict<ImmutableDictionary<string, string>>(
            File.ReadAllText(path), ContentJson.CreateOptions());
        if (replacements.IsEmpty) return english;
        var overrides = new PresentationTextCatalog(replacements);
        foreach (var (id, text) in overrides._messages)
        {
            if (!english._messages.TryGetValue(id, out var original))
                throw new InvalidDataException("Unknown presentation text ID: " + group + "/" + id);
            if (!ParameterNames(original).SetEquals(ParameterNames(text)))
                throw new InvalidDataException("Presentation text parameters differ: " + group + "/" + id);
        }
        return new(english._messages.SetItems(overrides._messages));
    }

    private static HashSet<string> ParameterNames(string text) =>
        Parameter.Matches(text).Select(match => match.Groups[1].Value).ToHashSet(StringComparer.Ordinal);

    public string Get(string id, params (string Name, string Value)[] arguments)
    {
        if (!_messages.TryGetValue(id, out var template))
            throw new InvalidDataException("Missing presentation text: " + id);
        // Most menu labels have no parameters and are read every draw; no binding objects are needed.
        if (arguments.Length == 0 && !template.Contains('{')) return template;
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (name, value) in arguments)
            if (value is null || !values.TryAdd(name, value))
                throw new ArgumentException("Duplicate or null presentation text argument: " + name, nameof(arguments));
        var expected = ParameterNames(template);
        if (!expected.SetEquals(values.Keys))
            throw new ArgumentException("Presentation text arguments differ: " + id, nameof(arguments));
        // Replace only template tokens; player-entered braces are literal data, never another template.
        return Parameter.Replace(template, match => values[match.Groups[1].Value]);
    }
}
