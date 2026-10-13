using System.Collections.Immutable;
using Game.Domain.Ids;

namespace Game.Domain.Items;

/// <summary>Original Indigo lobby shelf. All effects and inventory admission come from the unchanged Victory Road catalog.</summary>
public sealed record PokemonLeagueMartStock(string SourceCommit, ImmutableArray<ItemId> StockItems,
    OpeningItemSourceRef Source) : AuthenticatedShopStock
{
    public const string ExpectedMapId="map.indigo-plateau-pokemon-center-1f";
    public const string ExpectedStockId="IndigoPlateau_PokemonCenter_1F_Items";
    public const string ExpectedClerkScript="IndigoPlateau_PokemonCenter_1F_EventScript_Clerk";
    public const string ExpectedSourcePath="data/maps/IndigoPlateau_PokemonCenter_1F/scripts.inc";
    public const string ExpectedSourceSha256="4f83364d4a0b4dfeefc93d993f83ed4bab769ccc3e939df06e331bc9ffbc9f81";
    private static readonly OpeningItemSourceRef ExpectedSource=new(ExpectedSourcePath,ExpectedStockId,53,63,ExpectedSourceSha256);
    private static readonly ImmutableArray<(string Symbol,int SourceId,string Name,int Price,string Pocket)> ExpectedEntries=[
        ("ITEM_ULTRA_BALL",2,"ULTRA BALL",1200,"POCKET_POKE_BALLS"),
        ("ITEM_GREAT_BALL",3,"GREAT BALL",600,"POCKET_POKE_BALLS"),
        ("ITEM_FULL_RESTORE",19,"FULL RESTORE",3000,"POCKET_ITEMS"),
        ("ITEM_MAX_POTION",20,"MAX POTION",2500,"POCKET_ITEMS"),
        ("ITEM_REVIVE",24,"REVIVE",1500,"POCKET_ITEMS"),
        ("ITEM_FULL_HEAL",23,"FULL HEAL",600,"POCKET_ITEMS"),
        ("ITEM_MAX_REPEL",84,"MAX REPEL",700,"POCKET_ITEMS")];
    public override string Id=>Source.Symbol;
    public override ImmutableArray<ItemId> Items=>StockItems;

    public override void Validate(EarlyShopCatalog catalog)
    {
        catalog = EventIslandShopCatalog.For(catalog)?.Predecessor.Catalog ?? catalog;
        catalog = SevenIslandShopCatalog.For(catalog)?.Predecessor.Catalog ?? catalog;
        catalog = FiveIslandShopCatalog.For(catalog)?.Predecessor.Catalog ?? catalog;
        catalog = SixIslandShopCatalog.For(catalog)?.Predecessor.Catalog ?? catalog;
        catalog = FourIslandShopCatalog.For(catalog)?.Predecessor.Predecessor.Catalog ?? SeviiRubyShopCatalog.For(catalog)?.Predecessor.Catalog ?? catalog;
        VictoryRoadShopCatalog.ValidateCatalog(catalog);
        if(SourceCommit!=OpeningInventoryRules.SourceCommit||Source!=ExpectedSource||StockItems.IsDefault||
            !StockItems.SequenceEqual(ExpectedEntries.Select(e=>new ItemId("pret:"+e.Symbol))))
            throw new NotSupportedException("pokemon_league_mart_source");
        foreach(var expected in ExpectedEntries)
        {
            var definition=EarlyShopInventoryRules.Find(catalog,new("pret:"+expected.Symbol));
            if(definition is null||definition.SourceItemId!=expected.SourceId||definition.SourceItemSymbol!=expected.Symbol||
                definition.Name!=expected.Name||definition.Price!=expected.Price||definition.Pocket!=expected.Pocket||definition.Importance!=0)
                throw new NotSupportedException("pokemon_league_mart_item");
            var field=expected.Symbol is "ITEM_ULTRA_BALL" or "ITEM_GREAT_BALL"?"NULL":expected.Symbol=="ITEM_MAX_REPEL"?"FieldUseFunc_Repel":"FieldUseFunc_Medicine";
            var battle=expected.Symbol is "ITEM_ULTRA_BALL" or "ITEM_GREAT_BALL"?"BattleUseFunc_PokeBallEtc":expected.Symbol=="ITEM_MAX_REPEL"?"NULL":"BattleUseFunc_Medicine";
            if(definition.FieldUseFunction!=field||definition.BattleUseFunction!=battle)
                throw new NotSupportedException("pokemon_league_mart_item_effect");
        }
    }
}
