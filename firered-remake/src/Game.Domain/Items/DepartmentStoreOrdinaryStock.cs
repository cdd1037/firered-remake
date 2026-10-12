using System.Collections.Immutable;
using Game.Domain.Ids;

namespace Game.Domain.Items;

public sealed record DepartmentStoreOrdinaryItem(ItemId Id, int SourceItemId, string Name, int Price, string Pocket);

/// <summary>The one authenticated external shelf. It neither extends nor relabels the five-Mart item catalog.</summary>
public sealed record DepartmentStoreOrdinaryStock(string SourceCommit, string ContractSha256, string MapId,
    string ClerkScript, ImmutableArray<DepartmentStoreOrdinaryItem> Entries, OpeningItemSourceRef Source) : AuthenticatedShopStock
{
    public const string ExpectedMapId = "map.celadon-city-department-store-2f";
    public const string ExpectedStockId = "CeladonCity_DepartmentStore_2F_Items";
    public const string ExpectedClerkScript = "CeladonCity_DepartmentStore_2F_EventScript_ClerkItems";
    public const string ExpectedSourcePath = "data/maps/CeladonCity_DepartmentStore_2F/scripts.inc";
    public const string ExpectedContractSha256 = "8dc7477a49cabd7e1d21955cdb2cc2aaabc31891ca76a23f3b7c101a60493f84";
    private static readonly OpeningItemSourceRef ExpectedSource = new(ExpectedSourcePath, ExpectedStockId, 28, 41,
        "3c3838130444cd6c1bbae4b21cdf7869d5d0fa585c8a5ded6343ba98b3fbf17e");
    private static readonly ImmutableArray<DepartmentStoreOrdinaryItem> ExpectedEntries = [
        new(new("pret:ITEM_GREAT_BALL"), 3, "GREAT BALL", 600, "POCKET_POKE_BALLS"),
        new(new("pret:ITEM_SUPER_POTION"), 22, "SUPER POTION", 700, "POCKET_ITEMS"),
        new(new("pret:ITEM_REVIVE"), 24, "REVIVE", 1500, "POCKET_ITEMS"),
        new(new("pret:ITEM_ANTIDOTE"), 14, "ANTIDOTE", 100, "POCKET_ITEMS"),
        new(new("pret:ITEM_PARALYZE_HEAL"), 18, "PARLYZ HEAL", 200, "POCKET_ITEMS"),
        new(new("pret:ITEM_AWAKENING"), 17, "AWAKENING", 250, "POCKET_ITEMS"),
        new(new("pret:ITEM_BURN_HEAL"), 15, "BURN HEAL", 250, "POCKET_ITEMS"),
        new(new("pret:ITEM_ICE_HEAL"), 16, "ICE HEAL", 250, "POCKET_ITEMS"),
        new(new("pret:ITEM_SUPER_REPEL"), 83, "SUPER REPEL", 500, "POCKET_ITEMS")
    ];

    public override string Id => Source.Symbol;
    public override ImmutableArray<ItemId> Items => Entries.Select(item => item.Id).ToImmutableArray();

    public bool IsClerk(string mapId, string sourcePath, string script, string stockId) =>
        mapId == ExpectedMapId && sourcePath == ExpectedSourcePath && script == ExpectedClerkScript && stockId == ExpectedStockId;

    public override void Validate(EarlyShopCatalog catalog)
    {
        // This authenticates every inherited definition and the original five stock tables before
        // resolving these nine IDs. Full item effects/metadata come only from that unchanged catalog.
        EarlyShopInventoryRules.ValidateCatalog(catalog);
        if (catalog.Profile is not (PokemonTowerItemRules.Profile or FuchsiaGymItemRules.Profile or SafariShopCatalog.Profile or SilphShopCatalog.Profile or SaffronGymShopCatalog.Profile) || SourceCommit != OpeningInventoryRules.SourceCommit ||
            ContractSha256 != ExpectedContractSha256 || MapId != ExpectedMapId || ClerkScript != ExpectedClerkScript ||
            Source != ExpectedSource || Entries.IsDefault || !Entries.SequenceEqual(ExpectedEntries))
            throw new NotSupportedException("Exact Department Store 2F ordinary source shelf required.");
        foreach (var entry in Entries)
        {
            var definition = EarlyShopInventoryRules.Find(catalog, entry.Id);
            if (definition is null || definition.SourceItemSymbol != entry.Id.Value[5..] ||
                definition.SourceItemId != entry.SourceItemId || definition.Name != entry.Name ||
                definition.Price != entry.Price || definition.Pocket != entry.Pocket || definition.Importance != 0)
                throw new NotSupportedException("Department Store item differs from the authenticated item catalog.");
        }
    }
}
