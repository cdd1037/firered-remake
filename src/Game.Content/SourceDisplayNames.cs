using System.Collections.Immutable;
using Game.Domain.Pokemon;

namespace Game.Content;

internal sealed record SourceDisplayNameDocument(int SchemaVersion,
    ImmutableDictionary<string, string> Types, ImmutableDictionary<string, string> Abilities,
    string NoHeldItem);

/// <summary>Source display names keyed by gameplay identity; item definitions remain in SharedItemCatalog.</summary>
public sealed class SourceDisplayNames
{
    public const string RelativePath = "content/generated/ui/summary-screen/display-names.json";
    private readonly ImmutableDictionary<string, string> types, abilities;
    private readonly SharedItemCatalog items;
    private readonly string noHeldItem;

    private SourceDisplayNames(SourceDisplayNameDocument document, SharedItemCatalog items, PokemonCatalog pokemon)
    {
        if (document.SchemaVersion != 1 || !ValidNames(document.Types, "TYPE_") ||
            !ValidNames(document.Abilities, "ABILITY_") || string.IsNullOrWhiteSpace(document.NoHeldItem))
            throw new InvalidDataException("display_name_catalog_shape");
        types = document.Types; abilities = document.Abilities; noHeldItem = document.NoHeldItem;
        this.items = items;
        if (pokemon.Species.Values.SelectMany(s => new[] { s.Type1, s.Type2 })
                .Concat(pokemon.Moves.Values.Select(m => m.Type)).Any(id => !types.ContainsKey(id)) ||
            pokemon.Species.Values.SelectMany(s => new[] { s.Ability1, s.Ability2 })
                .Append("ABILITY_NONE").Any(id => !abilities.ContainsKey(id)))
            throw new InvalidDataException("display_name_reference_missing");
    }

    private static bool ValidNames(ImmutableDictionary<string, string> names, string prefix) =>
        names.Count != 0 && names.All(pair => pair.Key.StartsWith(prefix, StringComparison.Ordinal) &&
            pair.Key.Length > prefix.Length && pair.Key.All(c => c is >= 'A' and <= 'Z' or '_') &&
            !string.IsNullOrWhiteSpace(pair.Value));

    public static SourceDisplayNames LoadProject(string root, SharedItemCatalog items, PokemonCatalog pokemon) =>
        new(ContentJson.ParseStrict<SourceDisplayNameDocument>(File.ReadAllText(Path.Combine(root, RelativePath)),
            ContentJson.CreateOptions()), items, pokemon);

    public string TypeName(string id) => types.TryGetValue(id, out var name)
        ? name : throw new InvalidDataException("type_display_name_missing: " + id);
    public string AbilityName(string id) => abilities.TryGetValue(id, out var name)
        ? name : throw new InvalidDataException("ability_display_name_missing: " + id);
    public string ItemName(string symbol) => items.Definition(new("pret:" + symbol)).Name;
    public string HeldItemName(string symbol) => symbol == "ITEM_NONE" ? noHeldItem : ItemName(symbol);
}
