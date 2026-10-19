using System.Collections.Immutable;
namespace Game.Content;

/// <summary>Loads the imported battle text bank.</summary>
public static class PretBattleText
{
    public static ImmutableSortedDictionary<string,string> Load(string root, SourceTextPack? pack = null)
    {
        using var stream = File.OpenRead(Path.Combine(root, "content/generated/battle/text.json"));
        var texts = System.Text.Json.JsonSerializer.Deserialize<SortedDictionary<string, string>>(stream)
            ?? throw new InvalidDataException("Battle text catalog is empty.");
        return (pack ?? SourceTextPack.Current).Apply("battle", texts, preserveControls: true)
            .ToImmutableSortedDictionary(StringComparer.Ordinal);
    }
}
