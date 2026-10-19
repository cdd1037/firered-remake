using System.Collections.Immutable;
using System.Globalization;
using Game.Domain.Pokemon;
using Game.Domain.Rules;

namespace Game.Content;

internal sealed record SourceDisplayNameDocument(int SchemaVersion,
    ImmutableDictionary<string, string> Types, ImmutableDictionary<string, string> Abilities,
    ImmutableDictionary<string, string> Natures, string NoHeldItem, string EggName);

/// <summary>Source display names keyed by gameplay identity; item definitions remain in SharedItemCatalog.</summary>
public sealed class SourceDisplayNames
{
    public const string RelativePath = "content/generated/ui/summary-screen/display-names.json";
    private readonly ImmutableDictionary<string, string> types, abilities;
    private readonly ImmutableDictionary<Nature, string> natures;
    private readonly SharedItemCatalog items;
    private readonly string noHeldItem;
    public string EggName { get; }

    private SourceDisplayNames(SourceDisplayNameDocument document, SharedItemCatalog items, PokemonCatalog pokemon, SourceTextPack pack)
    {
        if (document.SchemaVersion != 1 || !ValidNames(document.Types, "TYPE_") ||
            !ValidNames(document.Abilities, "ABILITY_") || string.IsNullOrWhiteSpace(document.NoHeldItem) ||
            string.IsNullOrWhiteSpace(document.EggName) ||
            document.Natures.Count != Enum.GetValues<Nature>().Length || Enum.GetValues<Nature>().Any(nature =>
                !document.Natures.TryGetValue(((int)nature).ToString(CultureInfo.InvariantCulture), out var name) ||
                string.IsNullOrWhiteSpace(name)))
            throw new InvalidDataException("display_name_catalog_shape");
        types = pack.Apply("type-names", document.Types);
        abilities = pack.Apply("ability-names", document.Abilities);
        var labels = pack.Apply("display-labels", new Dictionary<string, string>
        {
            ["NoHeldItem"] = document.NoHeldItem, ["EggName"] = document.EggName
        });
        noHeldItem = labels["NoHeldItem"]; EggName = labels["EggName"];
        natures = pack.Apply("nature-names", document.Natures).ToImmutableDictionary(
            pair => (Nature)int.Parse(pair.Key, CultureInfo.InvariantCulture), pair => pair.Value);
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

    public static SourceDisplayNames LoadProject(string root, SharedItemCatalog items, PokemonCatalog pokemon, SourceTextPack? pack = null) =>
        new(ContentJson.ParseStrict<SourceDisplayNameDocument>(File.ReadAllText(Path.Combine(root, RelativePath)),
            ContentJson.CreateOptions()), items, pokemon, pack ?? items.TextPack);

    public string TypeName(string id) => types.TryGetValue(id, out var name)
        ? name : throw new InvalidDataException("type_display_name_missing: " + id);
    public string AbilityName(string id) => abilities.TryGetValue(id, out var name)
        ? name : throw new InvalidDataException("ability_display_name_missing: " + id);
    public string NatureName(Nature nature) => natures.TryGetValue(nature, out var name)
        ? name : throw new InvalidDataException("nature_display_name_missing: " + (int)nature);
    public string ItemName(string symbol) => items.Definition(new("pret:" + symbol)).Name;
    public string HeldItemName(string symbol) => symbol == "ITEM_NONE" ? noHeldItem : ItemName(symbol);
}
