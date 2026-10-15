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

}
