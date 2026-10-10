using System.Collections.Immutable;
using Game.Domain.Ids;
namespace Game.Domain.Items;

/// <summary>The nine source additions are opt-in; original shop stock and old serialized records stay unchanged.</summary>
public static class MtMoonItemRules
{
    public static ItemId MoonStoneId { get; } = new("pret:ITEM_MOON_STONE");
    public static ItemId HelixFossilId { get; } = new("pret:ITEM_HELIX_FOSSIL");
    public static ItemId DomeFossilId { get; } = new("pret:ITEM_DOME_FOSSIL");
    private sealed record Expected(string Symbol, int Id, string Name, int Price, EarlyShopUseSupport Use,
        string Field, int Battle, int Param, int[] Effects);
    private static readonly Expected[] Additions = [
        new("REVIVE",24,"REVIVE",1500,EarlyShopUseSupport.FieldRevive,"Medicine",1,0,[0,0,0,0,68,0,254]),
        new("ETHER",34,"ETHER",1200,EarlyShopUseSupport.FieldEther,"Ether",1,10,[0,0,0,0,24,0,10]),
        new("RARE_CANDY",68,"RARE CANDY",4800,EarlyShopUseSupport.DeferredRareCandy,"RareCandy",0,0,[0,0,0,64,68,224,253,5,3,2]),
        new("MOON_STONE",94,"MOON STONE",0,EarlyShopUseSupport.SeparateEvolution,"EvoItem",0,0,[0,0,0,0,128,0]),
        new("TINY_MUSHROOM",103,"TINYMUSHROOM",500,EarlyShopUseSupport.NoFieldUse,"OakStopsYou",0,0,[]),
        new("BIG_MUSHROOM",104,"BIG MUSHROOM",5000,EarlyShopUseSupport.NoFieldUse,"OakStopsYou",0,0,[]),
        new("STAR_PIECE",109,"STAR PIECE",9800,EarlyShopUseSupport.NoFieldUse,"OakStopsYou",0,0,[]),
        new("HELIX_FOSSIL",357,"HELIX FOSSIL",0,EarlyShopUseSupport.Fossil,"OakStopsYou",0,0,[]),
        new("DOME_FOSSIL",358,"DOME FOSSIL",0,EarlyShopUseSupport.Fossil,"OakStopsYou",0,0,[])
    ];
    public static void ValidateShop(EarlyShopCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (catalog.Profile != EarlyShopInventoryRules.MtMoonProfile || catalog.Items.IsDefault || catalog.Items.Length != 17)
            throw new NotSupportedException("Only the pinned MtMoon shop additions are supported.");
        EarlyShopInventoryRules.ValidateCatalog(catalog with { Profile = EarlyShopInventoryRules.Profile, Items = catalog.Items.Take(8).ToImmutableArray() });
        for (int n = 0; n < Additions.Length; n++)
        {
            var e = Additions[n]; var i = catalog.Items[n + 8]; var m = i?.SourceMetadata;
            bool key = e.Use == EarlyShopUseSupport.Fossil, party = e.Effects.Length > 0;
            string symbol = "ITEM_" + e.Symbol;
            if (i is null || m is null || i.Id != new ItemId("pret:" + symbol) || i.SourceItemSymbol != symbol ||
                i.SourceItemId != e.Id || i.Name != e.Name || i.Price != e.Price || i.Importance != (key ? 1 : 0) ||
                i.Pocket != (key ? "POCKET_KEY_ITEMS" : "POCKET_ITEMS") || string.IsNullOrWhiteSpace(i.Description) ||
                i.Type != (party ? "ITEM_TYPE_PARTY_MENU" : "ITEM_TYPE_BAG_MENU") || i.FieldUseFunction != "FieldUseFunc_" + e.Field ||
                i.BattleUsage != e.Battle || i.BattleUseFunction != (e.Battle == 0 ? "NULL" : "BattleUseFunc_" + e.Field) ||
                i.HoldEffectParam != e.Param || i.HealHp != 0 || i.CureStatus != 0 || i.UseSupport != e.Use ||
                i.EffectBytes.IsDefault || !i.EffectBytes.SequenceEqual(e.Effects) || i.Source is null ||
                (party ? i.EffectSource is null : i.EffectSource is not null) ||
                m.Id != i.Id || m.SourceItemSymbol != symbol || m.SourceItemId != e.Id || m.Name != e.Name ||
                m.Price != i.Price || m.Importance != i.Importance || m.Pocket != i.Pocket || m.Type != i.Type ||
                m.FieldUseFunction != i.FieldUseFunction || m.BattleUsage != i.BattleUsage || m.BattleUseFunction != i.BattleUseFunction ||
                m.HoldEffect != "HOLD_EFFECT_NONE" || m.HoldEffectParam != e.Param || m.Registrability != 0 ||
                m.SecondaryId != 0 || m.CanGive != !key || m.CanToss != !key || m.CanSell != (e.Price > 0) || m.Source != i.Source)
                throw new NotSupportedException("Unsupported MtMoon item metadata: " + symbol);
        }
    }
}
