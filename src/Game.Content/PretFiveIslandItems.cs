using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;
using Game.Domain.World;

namespace Game.Content;

public sealed record FiveIslandMachineRows(ItemId Item, ImmutableArray<MachineCompatibility> Compatibility);
public sealed record FiveIslandItemsDocument(ImmutableArray<MachineItem> Items,
    ImmutableArray<EarlyShopItemDefinition> Additions, ImmutableArray<FiveIslandMachineRows> MachineAdditions,
    MachineDefinition Tm36, ImmutableArray<FieldPickupDefinition> Pickups);
public sealed record FiveIslandItemContent(ImmutableArray<MachineItem> Items, ImmutableArray<FieldPickupDefinition> Pickups,
    MachineCatalog Machines);

/// <summary>Exact Up-Grade, Sapphire, TM36, eight ordinary pickups and all135 species compatibility; no new shop stock.</summary>
public static partial class PretFiveIslandItems
{
    public static FiveIslandItemsDocument LoadDocument(string root, SharedMachineCatalog? shared = null)
    {
        var bytes = File.ReadAllBytes(Path.Combine(root, "content/generated/data/regions/five-island/items.json"));
        var document = JsonSerializer.Deserialize<FiveIslandItemsDocument>(bytes,
            (shared ?? SharedMachineCatalog.LoadProject(root)).CreateJsonOptions(new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase,Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } }))
            ?? throw new InvalidDataException("five_island_items_missing");
        if (            document.Items.Length != 9 || document.Additions.Length != 3 || document.MachineAdditions.Length != 38 ||
            document.Pickups.Length != 8 || document.Pickups.Count(p => p.Hidden) != 2)
            throw new InvalidDataException("five_island_items_scope");
        return document;
    }
    public static FiveIslandItemContent LoadCatalogs(string root, GamePokemonCatalog pokemon,
        EarlyShopCatalog previousShop, MachineCatalog previousMachines, SharedMachineCatalog? shared = null) =>
        LoadCatalogs(LoadDocument(root, shared), pokemon, previousShop, previousMachines, shared);
    private static FiveIslandItemContent LoadCatalogs(FiveIslandItemsDocument document, GamePokemonCatalog pokemon,
        EarlyShopCatalog previousShop, MachineCatalog previousMachines, SharedMachineCatalog? shared = null)
    {
        var pickups = document.Pickups.Select(pickup =>
        {
            var item = pickup.Item == new ItemId("pret:ITEM_TM36") ? document.Tm36.Machine.Item :
                previousShop.Items.SingleOrDefault(value => value.Id == pickup.Item)?.Item;
            if (item is null)
                throw new InvalidDataException("five_island_pickup_item_missing:" + pickup.Item.Value);
            if (item.SourceItemId != pickup.SourceItemId || item.SourceItemSymbol != pickup.ItemSymbol ||
                item.Pocket != pickup.Pocket)
                throw new InvalidDataException("five_island_pickup_metadata");
            return pickup with { ItemName = item.Name };
        }).ToImmutableArray();
        return new(document.Items, pickups, previousMachines);
    }
}
