using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;
using Game.Domain.World;

namespace Game.Content;

public sealed record FourIslandMachineRows(ItemId Item, ImmutableArray<PewterMachineCompatibility> Compatibility);
public sealed record FourIslandItemsDocument(string SourceCommit, string Profile, ImmutableArray<PewterMachineItem> Items,
    ImmutableArray<EarlyShopItemDefinition> Additions, ImmutableArray<FourIslandMachineRows> MachineAdditions,
    PewterAdditionalMachine Hm07, EarlyShopStock Shop, ImmutableArray<FieldPickupDefinition> Pickups);
public sealed record FourIslandItemContent(ImmutableArray<PewterMachineItem> Items, ImmutableArray<FieldPickupDefinition> Pickups,
    FourIslandShopCatalog Shop, FourIslandMachineCatalog Machines);

/// <summary>Exact NeverMeltIce, HM07, eight pickups and source Mart shelf; shared item/teaching rules execute them.</summary>
public static partial class PretFourIslandItems
{
    public const string ItemsSha256 = "9cb68121352e6d3372baece227d7486b630f32baf053a3164ce10ebe11350b02";
    public const string PickupsSha256 = "c46e181b894dddcbca6773fdae0c9367477f56e68e3d0ec34719b03929eb7b88";
    public static FourIslandItemsDocument LoadDocument(string root)
    {
        var bytes = File.ReadAllBytes(Path.Combine(root, "content/generated/four-island-items/items.json"));
        if (Convert.ToHexStringLower(SHA256.HashData(bytes)) != ItemsSha256)
            throw new InvalidDataException("four_island_items_source");
        var document = JsonSerializer.Deserialize<FourIslandItemsDocument>(bytes,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } })
            ?? throw new InvalidDataException("four_island_items_missing");
        if (document.SourceCommit != PokemonFactory.SourceCommit || document.Profile != "firered-four-island-items-v1" ||
            document.Items.Length != 2 || document.Additions.Length != 1 || document.MachineAdditions.Length != 37 ||
            document.Pickups.Length != 8 || document.Pickups.Count(p => p.Hidden) != 2)
            throw new InvalidDataException("four_island_items_scope");
        return document;
    }
    public static FourIslandItemContent LoadCatalogs(string root, FourIslandOwnedPokemonCatalog pokemon,
        EarlyShopCatalog previousShop, PewterMachineCatalog previousMachines) =>
        LoadCatalogs(LoadDocument(root), pokemon, previousShop, previousMachines);
    private static FourIslandItemContent LoadCatalogs(FourIslandItemsDocument document, FourIslandOwnedPokemonCatalog pokemon,
        EarlyShopCatalog previousShop, PewterMachineCatalog previousMachines)
    {
        var previousShopProof = SeviiRubyShopCatalog.For(previousShop) ?? throw new InvalidDataException("four_island_items_shop_predecessor");
        var shop = FourIslandShopCatalog.Authenticate(previousShopProof, previousShop with {
            Profile = FourIslandShopCatalog.Profile, Items = previousShop.Items.AddRange(document.Additions),
            Shops = previousShop.Shops.Add(document.Shop) });
        var previousMachineProof = SeviiRubyMachineCatalog.For(previousMachines) ?? throw new InvalidDataException("four_island_items_machine_predecessor");
        var additions = document.MachineAdditions.ToDictionary(m => m.Item);
        var machines = FourIslandMachineCatalog.Authenticate(previousMachineProof, pokemon, previousMachines with {
            Profile = FourIslandMachineCatalog.Profile,
            Compatibility = previousMachines.Compatibility.AddRange(additions[previousMachines.Tm39.Item.Id].Compatibility),
            AdditionalMachines = previousMachines.AdditionalMachines!.Value.Select(m => m with {
                Compatibility = m.Compatibility.AddRange(additions[m.Machine.Item.Id].Compatibility)
            }).Append(document.Hm07).ToImmutableArray() });
        foreach (var pickup in document.Pickups)
        {
            // HMs are source machine definitions and must not be fabricated as ordinary bag medicine.
            var item = pickup.Item == FourIslandMachineCatalog.Hm07Id ? document.Hm07.Machine.Item :
                shop.Catalog.Items.SingleOrDefault(i => i.Id == pickup.Item)?.SourceMetadata;
            if (item is not null)
            {
                if (item.SourceItemId != pickup.SourceItemId || item.SourceItemSymbol != pickup.ItemSymbol ||
                    item.Name != pickup.ItemName || item.Pocket != pickup.Pocket)
                    throw new InvalidDataException("four_island_pickup_metadata");
            }
            else if (shop.Catalog.Items.SingleOrDefault(i => i.Id == pickup.Item) is not {} ordinary ||
                ordinary.SourceItemId != pickup.SourceItemId || ordinary.SourceItemSymbol != pickup.ItemSymbol ||
                ordinary.Name != pickup.ItemName || ordinary.Pocket != pickup.Pocket)
                throw new InvalidDataException("four_island_pickup_item_missing");
        }
        return new(document.Items, document.Pickups, shop, machines);
    }
}
