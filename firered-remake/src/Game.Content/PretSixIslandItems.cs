using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;
using Game.Domain.World;

namespace Game.Content;

public sealed record SixIslandMachineRows(ItemId Item, ImmutableArray<PewterMachineCompatibility> Compatibility);
public sealed record SixIslandItemsDocument(string SourceCommit, string Profile, ImmutableArray<PewterMachineItem> Items,
    ImmutableArray<EarlyShopItemDefinition> Additions, ImmutableArray<SixIslandMachineRows> MachineAdditions,
    EarlyShopStock Shop, ImmutableArray<FieldPickupDefinition> Pickups);
public sealed record SixIslandItemContent(ImmutableArray<PewterMachineItem> Items, ImmutableArray<FieldPickupDefinition> Pickups,
    SixIslandShopCatalog Shop, SixIslandMachineCatalog Machines);

/// <summary>Source Sun Stone, Dragon Scale, Dream Mail, nine pickups and complete inherited-machine compatibility.</summary>
public static partial class PretSixIslandItems
{
    public static SixIslandItemsDocument LoadDocument(string root)
    {
        var bytes = File.ReadAllBytes(Path.Combine(root, "content/generated/six-island-items/items.json"));
        var document = JsonSerializer.Deserialize<SixIslandItemsDocument>(bytes,
            SharedMachineCatalog.Options(root, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase,Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } }))
            ?? throw new InvalidDataException("six_island_items_missing");
        if (            document.Items.Length != 3 || document.Additions.Length != 3 || document.MachineAdditions.Length != 38 ||
            document.Pickups.Length != 9 || document.Pickups.Count(p => p.Hidden) != 4)
            throw new InvalidDataException("six_island_items_scope");
        return document;
    }
    public static SixIslandItemContent LoadCatalogs(string root, SixIslandOwnedPokemonCatalog pokemon,
        EarlyShopCatalog previousShop, PewterMachineCatalog previousMachines) =>
        LoadCatalogs(LoadDocument(root), pokemon, previousShop, previousMachines);
    private static SixIslandItemContent LoadCatalogs(SixIslandItemsDocument document, SixIslandOwnedPokemonCatalog pokemon,
        EarlyShopCatalog previousShop, PewterMachineCatalog previousMachines)
    {
        var previousShopProof = FourIslandShopCatalog.For(previousShop) ?? throw new InvalidDataException("six_island_items_shop_predecessor");
        var shop = SixIslandShopCatalog.Authenticate(previousShopProof, previousShop with {
            Profile = SixIslandShopCatalog.Profile,Items = previousShop.Items.AddRange(document.Additions),Shops = previousShop.Shops.Add(document.Shop) });
        var previousMachineProof = FourIslandMachineCatalog.For(previousMachines) ?? throw new InvalidDataException("six_island_items_machine_predecessor");
        var additions = document.MachineAdditions.ToDictionary(m => m.Item);
        var machines = SixIslandMachineCatalog.Authenticate(previousMachineProof, pokemon, previousMachines with {
            Profile = SixIslandMachineCatalog.Profile,Compatibility = previousMachines.Compatibility.AddRange(additions[previousMachines.Tm39.Item.Id].Compatibility),AdditionalMachines = previousMachines.AdditionalMachines!.Value.Select(m => m with {
                Compatibility = m.Compatibility.AddRange(additions[m.Machine.Item.Id].Compatibility)
            }).ToImmutableArray() });
        foreach (var pickup in document.Pickups)
        {
            var item = shop.Catalog.Items.SingleOrDefault(i => i.Id == pickup.Item)?.SourceMetadata;
            if (item is not null)
            {
                if (item.SourceItemId != pickup.SourceItemId || item.SourceItemSymbol != pickup.ItemSymbol ||
                    item.Name != pickup.ItemName || item.Pocket != pickup.Pocket)
                    throw new InvalidDataException("six_island_pickup_metadata");
            }
            else if (shop.Catalog.Items.SingleOrDefault(i => i.Id == pickup.Item) is not {} ordinary ||
                ordinary.SourceItemId != pickup.SourceItemId || ordinary.SourceItemSymbol != pickup.ItemSymbol ||
                ordinary.Name != pickup.ItemName || ordinary.Pocket != pickup.Pocket)
                throw new InvalidDataException("six_island_pickup_item_missing");
        }
        return new(document.Items, document.Pickups, shop, machines);
    }
}
