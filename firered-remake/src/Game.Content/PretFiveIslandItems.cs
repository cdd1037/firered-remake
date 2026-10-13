using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;
using Game.Domain.World;

namespace Game.Content;

public sealed record FiveIslandMachineRows(ItemId Item, ImmutableArray<PewterMachineCompatibility> Compatibility);
public sealed record FiveIslandItemsDocument(string SourceCommit, string Profile, ImmutableArray<PewterMachineItem> Items,
    ImmutableArray<EarlyShopItemDefinition> Additions, ImmutableArray<FiveIslandMachineRows> MachineAdditions,
    PewterAdditionalMachine Tm36, ImmutableArray<FieldPickupDefinition> Pickups);
public sealed record FiveIslandItemContent(ImmutableArray<PewterMachineItem> Items, ImmutableArray<FieldPickupDefinition> Pickups,
    FiveIslandShopCatalog Shop, FiveIslandMachineCatalog Machines);

/// <summary>Exact Up-Grade, Sapphire, TM36, eight ordinary pickups and all135 species compatibility; no new shop stock.</summary>
public static partial class PretFiveIslandItems
{
    public const string ItemsSha256 = "c45233322a3b0bd5c2efe55063ec6066907a6e1a24ff0cfe3088a7b33d4d3968";
    public const string PickupsSha256 = "21fad57fbb36fd8b83e4f9ba186885e4dc12ba53013b540b602eb81e2c1cdafb";
    public static FiveIslandItemsDocument LoadDocument(string root)
    {
        var bytes = File.ReadAllBytes(Path.Combine(root, "content/generated/five-island-rules/items.json"));
        if (Convert.ToHexStringLower(SHA256.HashData(bytes)) != ItemsSha256)
            throw new InvalidDataException("five_island_items_source");
        var document = JsonSerializer.Deserialize<FiveIslandItemsDocument>(bytes,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } })
            ?? throw new InvalidDataException("five_island_items_missing");
        if (document.SourceCommit != PokemonFactory.SourceCommit || document.Profile != "firered-five-island-items-v1" ||
            document.Items.Length != 9 || document.Additions.Length != 3 || document.MachineAdditions.Length != 38 ||
            document.Pickups.Length != 8 || document.Pickups.Count(p => p.Hidden) != 2)
            throw new InvalidDataException("five_island_items_scope");
        return document;
    }
    public static FiveIslandItemContent LoadCatalogs(string root, FiveIslandOwnedPokemonCatalog pokemon,
        EarlyShopCatalog previousShop, PewterMachineCatalog previousMachines) =>
        LoadCatalogs(LoadDocument(root), pokemon, previousShop, previousMachines);
    private static FiveIslandItemContent LoadCatalogs(FiveIslandItemsDocument document, FiveIslandOwnedPokemonCatalog pokemon,
        EarlyShopCatalog previousShop, PewterMachineCatalog previousMachines)
    {
        var previousShopProof = SixIslandShopCatalog.For(previousShop) ?? throw new InvalidDataException("five_island_items_shop_predecessor");
        var shop = FiveIslandShopCatalog.Authenticate(previousShopProof, previousShop with {
            Profile = FiveIslandShopCatalog.Profile, Items = previousShop.Items.AddRange(document.Additions) });
        var previousMachineProof = SixIslandMachineCatalog.For(previousMachines) ?? throw new InvalidDataException("five_island_items_machine_predecessor");
        var additions = document.MachineAdditions.ToDictionary(m => m.Item);
        var machines = FiveIslandMachineCatalog.Authenticate(previousMachineProof, pokemon, previousMachines with {
            Profile = FiveIslandMachineCatalog.Profile,
            Compatibility = previousMachines.Compatibility.AddRange(additions[previousMachines.Tm39.Item.Id].Compatibility),
            AdditionalMachines = previousMachines.AdditionalMachines!.Value.Select(m => m with {
                Compatibility = m.Compatibility.AddRange(additions[m.Machine.Item.Id].Compatibility)
            }).Append(document.Tm36).ToImmutableArray() });
        foreach (var pickup in document.Pickups)
        {
            // TM36 remains a source machine definition, distinct from ordinary bag items.
            var item = pickup.Item == FiveIslandMachineCatalog.Tm36Id ? document.Tm36.Machine.Item :
                shop.Catalog.Items.SingleOrDefault(i => i.Id == pickup.Item)?.SourceMetadata;
            if (item is not null)
            {
                if (item.SourceItemId != pickup.SourceItemId || item.SourceItemSymbol != pickup.ItemSymbol ||
                    item.Name != pickup.ItemName || item.Pocket != pickup.Pocket)
                    throw new InvalidDataException("five_island_pickup_metadata");
            }
            else if (shop.Catalog.Items.SingleOrDefault(i => i.Id == pickup.Item) is not {} ordinary ||
                ordinary.SourceItemId != pickup.SourceItemId || ordinary.SourceItemSymbol != pickup.ItemSymbol ||
                ordinary.Name != pickup.ItemName || ordinary.Pocket != pickup.Pocket)
                throw new InvalidDataException("five_island_pickup_item_missing:" + pickup.Item.Value);
        }
        return new(document.Items, document.Pickups, shop, machines);
    }
}
