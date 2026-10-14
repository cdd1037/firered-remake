using System.Collections.Immutable;
using Game.Domain.Ids;

namespace Game.Domain.Items;

/// <summary>The original 4F Doll row. The remaining five original rows are explicitly deferred.</summary>
public sealed record PokeDollShopStock : AuthenticatedShopStock
{
    public const string ExpectedMapId = "map.celadon-city-department-store-4f";
    public const string ExpectedStockId = "CeladonCity_DepartmentStore_4F_Items";
    public const string ExpectedClerkScript = "CeladonCity_DepartmentStore_4F_EventScript_Clerk";
    public const string ExpectedSourcePath = "data/maps/CeladonCity_DepartmentStore_4F/scripts.inc";
    public const string SourceSha256 = "5b7518184e31879a034d326a9cf954fb8e081ec975895d3f441dd98de076c81a";
    public override string Id => ExpectedStockId;
    public override ImmutableArray<ItemId> Items => [PokeDollInventoryCatalog.PokeDollId];
    public override void Validate(EarlyShopCatalog catalog)
    {
        PokeDollInventoryCatalog.ValidateCatalog(catalog);
        var item = EarlyShopInventoryRules.Find(catalog, PokeDollInventoryCatalog.PokeDollId);
        if (item is not { SourceItemId: 80, Price: 1000, Pocket: "POCKET_ITEMS", Importance: 0 } ||
            item.SourceItemSymbol != "ITEM_POKE_DOLL")
            throw new NotSupportedException("poke_doll_source_shelf");
    }
}
