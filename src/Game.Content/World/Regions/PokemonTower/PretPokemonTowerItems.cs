using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Domain.Ids;
using Game.Domain.Items;
namespace Game.Content;

public sealed record PretPokemonTowerItemsDocument(int SchemaVersion, ImmutableArray<PretBillSourceItem> Items, ImmutableArray<ItemIcon> Icons);
public sealed record PretPokemonTowerPickup(string Map, string Kind, int? LocalId, int? EventIndex,
    int X, int Y, int Elevation, string Flag, int SourceFlagId, bool InitiallyHidden, bool Underfoot,
    bool RequiresItemfinder, bool AllowsOrdinaryInteraction, ItemId Item, int Quantity, string? Script,
    string? ScriptCommand, OpeningItemSourceRef MapSource, OpeningItemSourceRef? ScriptSource);
public sealed record PretPokemonTowerPickupsDocument(int SchemaVersion, ImmutableArray<PretPokemonTowerPickup> Pickups)
{
    /// <summary>Only these nine records may be registered with ordinary adjacent-A interaction.</summary>
    [JsonIgnore] public ImmutableArray<PretPokemonTowerPickup> OrdinaryPickups =>
        Pickups.Where(p => p.AllowsOrdinaryInteraction && !p.RequiresItemfinder && !p.Underfoot).ToImmutableArray();
}
/// <summary>Loads Tower item metadata, inventory icons and pickup declarations.</summary>
public static class PretPokemonTowerItems
{
    public const string Profile = PokemonTowerItemRules.Profile;
    private const string Stem = "data/items/pokemon-tower";
    private static readonly UTF8Encoding Utf8 = new(false, true);
    private static void Require(bool condition, string message) => PretCeladonApproachSource.Require(condition, message);
    public static PretPokemonTowerItemsDocument Load(string json, SharedMachineCatalog machines)
    {
        var options = machines.CreateJsonOptions(); options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, false));
        var document = ContentJson.ParseStrict<PretPokemonTowerItemsDocument>(json, options);
        Require(document.SchemaVersion == 1 && document.Items.Length == 5 && document.Icons.Length == 5, "Pokemon Tower item scope differs.");
        foreach (var item in document.Items)
        {
            PokemonTowerItemRules.ValidateSourceItem(item.Metadata, item.EffectBytes);
        }
        Require(document.Items.Select(i => i.Metadata.Item.Id).Distinct().Count() == 5, "Duplicate Pokemon Tower source items.");
        foreach (var icon in document.Icons)
        {
            Require(document.Items.Any(i => i.Metadata.Item.Id == icon.Item) &&
                icon.Region is not null, "Pokemon Tower icon identity differs.");
            ItemIconAssets.Validate(icon);
        }
        Require(document.Icons.Select(i => i.Item).Distinct().Count() == 5, "Duplicate Pokemon Tower icons.");
        return document;
    }
    public static PretPokemonTowerItemsDocument LoadProject(string root, SharedMachineCatalog? shared = null)
    {
        var outputs = PretCeladonApproachSource.Outputs(root,Stem);
        var document = Load(Utf8.GetString(outputs["pokemon-tower-items.json"]), (shared ?? SharedMachineCatalog.LoadProject(root)));
        return document;
    }
    /// <summary>Preserves all prior 56 entries and all five stocks. Metadata validation is explicit; generic inventory admission is separate.</summary>

    public static EarlyShopItemDefinition? Definition(PretPokemonTowerItemsDocument document, ItemId id)
    {
        var source = document.Items.SingleOrDefault(i => i.Metadata.Item.Id == id);
        return source is null ? null : PretSsAnneItems.Definition(source);
    }
    public static PretPokemonTowerPickupsDocument LoadPickupsProject(string root)
    {
        var bytes = PretCeladonApproachSource.Read(root,"content/generated/" + Stem + "/pokemon-tower-pickups.json");
        var document = ContentJson.ParseStrict<PretPokemonTowerPickupsDocument>(Utf8.GetString(bytes), ContentJson.CreateOptions());
        Require(document.SchemaVersion == 1 &&
            document.Pickups.Length == 10 && document.Pickups.Count(p => p.Kind == "visible") == 8 &&
            document.Pickups.Count(p => p.Kind == "hidden") == 2 && document.OrdinaryPickups.Length == 9 &&
            document.Pickups.Select(p => p.Flag).Distinct().Count() == 10,
            "Pokemon Tower pickup scope differs.");
        foreach (var pickup in document.Pickups)
        {
            Require(pickup.Quantity == 1 && pickup.Map is "MAP_POKEMON_TOWER_3F" or "MAP_POKEMON_TOWER_4F" or
                "MAP_POKEMON_TOWER_5F" or "MAP_POKEMON_TOWER_6F" or "MAP_POKEMON_TOWER_7F" &&
                pickup.X >= 0 && pickup.Y >= 0 && pickup.Elevation == 3 && !pickup.InitiallyHidden && pickup.SourceFlagId > 0,
                "Pokemon Tower pickup shape differs.");
            Require(pickup.Kind == "visible" ? pickup.LocalId is > 0 && pickup.EventIndex is null &&
                pickup.Script is not null && pickup.ScriptCommand == "finditem" && pickup.ScriptSource is not null :
                pickup.Kind == "hidden" && pickup.LocalId is null && pickup.EventIndex is >= 0 && pickup.Script is null &&
                pickup.ScriptCommand is null && pickup.ScriptSource is null, "Pokemon Tower pickup event differs.");
            bool special = pickup.Item == PokemonTowerItemRules.SootheBellId;
            Require(pickup.Underfoot == special && pickup.RequiresItemfinder == special && pickup.AllowsOrdinaryInteraction == !special,
                "Pokemon Tower Itemfinder-only pickup boundary differs.");
            if (special) Require(pickup.Map == "MAP_POKEMON_TOWER_7F" && pickup.X == 11 && pickup.Y == 4 &&
                pickup.EventIndex == 0 && pickup.Flag == "FLAG_HIDDEN_ITEM_POKEMON_TOWER_7F_SOOTHE_BELL" &&
                pickup.SourceFlagId == 1159, "Soothe Bell location differs.");
        }
        return document;
    }
}
