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

    public string Get(string id, params (string Name, string Value)[] arguments)
    {
        if (!_messages.TryGetValue(id, out var template))
            throw new InvalidDataException("Missing presentation text: " + id);
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (name, value) in arguments)
            if (value is null || !values.TryAdd(name, value))
                throw new ArgumentException("Duplicate or null presentation text argument: " + name, nameof(arguments));
        var expected = Parameter.Matches(template).Select(m => m.Groups[1].Value).ToHashSet(StringComparer.Ordinal);
        if (!expected.SetEquals(values.Keys))
            throw new ArgumentException("Presentation text arguments differ: " + id, nameof(arguments));
        // Replace only template tokens; player-entered braces are literal data, never another template.
        return Parameter.Replace(template, match => values[match.Groups[1].Value]);
    }
}
