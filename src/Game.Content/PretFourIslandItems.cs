using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;
using Game.Domain.World;

namespace Game.Content;

public sealed record FourIslandMachineRows(ItemId Item, ImmutableArray<MachineCompatibility> Compatibility);
public sealed record FourIslandItemsDocument(ImmutableArray<MachineItem> Items,
    ImmutableArray<EarlyShopItemDefinition> Additions, ImmutableArray<FourIslandMachineRows> MachineAdditions,
    MachineDefinition Hm07, EarlyShopStock Shop, ImmutableArray<FieldPickupDefinition> Pickups);
public sealed record FourIslandItemContent(ImmutableArray<MachineItem> Items, ImmutableArray<FieldPickupDefinition> Pickups,
    MachineCatalog Machines);

/// <summary>Exact NeverMeltIce, HM07, eight pickups and source Mart shelf; shared item/teaching rules execute them.</summary>
public static partial class PretFourIslandItems
{
    public static FourIslandItemsDocument LoadDocument(string root, SharedMachineCatalog? shared = null)
    {
        var bytes = File.ReadAllBytes(Path.Combine(root, "content/generated/data/items/four-island.json"));
        var document = JsonSerializer.Deserialize<FourIslandItemsDocument>(bytes,
            (shared ?? SharedMachineCatalog.LoadProject(root)).CreateJsonOptions(new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase,Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } }))
            ?? throw new InvalidDataException("four_island_items_missing");
        if (            document.Items.Length != 2 || document.Additions.Length != 1 || document.MachineAdditions.Length != 37 ||
            document.Pickups.Length != 8 || document.Pickups.Count(p => p.Hidden) != 2)
            throw new InvalidDataException("four_island_items_scope");
        return document;
    }
    public static FourIslandItemContent LoadCatalogs(string root, GamePokemonCatalog pokemon,
        EarlyShopCatalog previousShop, MachineCatalog previousMachines, SharedMachineCatalog? shared = null) =>
        LoadCatalogs(LoadDocument(root, shared), pokemon, previousShop, previousMachines, shared);
    private static FourIslandItemContent LoadCatalogs(FourIslandItemsDocument document, GamePokemonCatalog pokemon,
        EarlyShopCatalog previousShop, MachineCatalog previousMachines, SharedMachineCatalog? shared = null)
    {
        var pickups = document.Pickups.Select(pickup =>
        {
            var item = pickup.Item == new ItemId("pret:ITEM_HM07") ? document.Hm07.Machine.Item :
                previousShop.Items.SingleOrDefault(value => value.Id == pickup.Item)?.Item;
            if (item is null)
                throw new InvalidDataException("four_island_pickup_item_missing");
            if (item.SourceItemId != pickup.SourceItemId || item.SourceItemSymbol != pickup.ItemSymbol ||
                item.Pocket != pickup.Pocket)
                throw new InvalidDataException("four_island_pickup_metadata");
            return pickup with { ItemName = item.Name };
        }).ToImmutableArray();
        return new(document.Items, pickups, previousMachines);
    }
}
