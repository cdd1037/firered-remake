using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;
using Game.Domain.World;

namespace Game.Content;

public sealed record SixIslandMachineRows(ItemId Item, ImmutableArray<MachineCompatibility> Compatibility);
public sealed record SixIslandItemsDocument(ImmutableArray<MachineItem> Items,
    ImmutableArray<EarlyShopItemDefinition> Additions, ImmutableArray<SixIslandMachineRows> MachineAdditions,
    EarlyShopStock Shop, ImmutableArray<FieldPickupDefinition> Pickups);
public sealed record SixIslandItemContent(ImmutableArray<MachineItem> Items, ImmutableArray<FieldPickupDefinition> Pickups,
    MachineCatalog Machines);

/// <summary>Source Sun Stone, Dragon Scale, Dream Mail, source pickups and complete inherited-machine compatibility.</summary>
public static partial class PretSixIslandItems
{
    public static SixIslandItemsDocument LoadDocument(string root, SharedMachineCatalog? shared = null)
    {
        var bytes = File.ReadAllBytes(Path.Combine(root, "content/generated/data/items/six-island.json"));
        var document = JsonSerializer.Deserialize<SixIslandItemsDocument>(bytes,
            (shared ?? SharedMachineCatalog.LoadProject(root)).CreateJsonOptions(new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase,Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } }))
            ?? throw new InvalidDataException("six_island_items_missing");
        if (document.Items.IsDefaultOrEmpty || document.Additions.IsDefaultOrEmpty || document.MachineAdditions.IsDefaultOrEmpty ||
            document.Pickups.IsDefaultOrEmpty || document.Pickups.Select(p => p.Id).Distinct().Count() != document.Pickups.Length)
            throw new InvalidDataException("six_island_items_scope");
        return document;
    }
    public static SixIslandItemContent LoadCatalogs(string root, GamePokemonCatalog pokemon,
        EarlyShopCatalog previousShop, MachineCatalog previousMachines, SharedMachineCatalog? shared = null) =>
        LoadCatalogs(LoadDocument(root, shared), pokemon, previousShop, previousMachines, shared);
    private static SixIslandItemContent LoadCatalogs(SixIslandItemsDocument document, GamePokemonCatalog pokemon,
        EarlyShopCatalog previousShop, MachineCatalog previousMachines, SharedMachineCatalog? shared = null)
    {
        var pickups = document.Pickups.Select(pickup =>
        {
            var item = previousShop.Items.SingleOrDefault(value => value.Id == pickup.Item)?.Item;
            if (item is null)
                throw new InvalidDataException("six_island_pickup_item_missing");
            if (item.SourceItemId != pickup.SourceItemId || item.SourceItemSymbol != pickup.ItemSymbol ||
                item.Pocket != pickup.Pocket)
                throw new InvalidDataException("six_island_pickup_metadata");
            return pickup with { ItemName = item.Name };
        }).ToImmutableArray();
        return new(document.Items, pickups, previousMachines);
    }
}
