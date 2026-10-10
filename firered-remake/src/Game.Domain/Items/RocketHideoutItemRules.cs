using System.Collections.Immutable;
using Game.Domain.Ids;
namespace Game.Domain.Items;

/// <summary>Exact additive Rocket Hideout inventory metadata. Executable uses are independently admitted.</summary>
public static class RocketHideoutItemRules
{
    public const string Profile = "firered-rocket-hideout-items-v1";
    public static ItemId LiftKeyId { get; } = new("pret:ITEM_LIFT_KEY");
    public static ItemId SilphScopeId { get; } = new("pret:ITEM_SILPH_SCOPE");
    public static ItemId BlackGlassesId { get; } = new("pret:ITEM_BLACK_GLASSES");
    public static ItemId XSpeedId { get; } = new("pret:ITEM_X_SPEED");
    public static ItemId CalciumId { get; } = new("pret:ITEM_CALCIUM");
    public static ItemId NetBallId { get; } = new("pret:ITEM_NET_BALL");
    public static ItemId NestBallId { get; } = new("pret:ITEM_NEST_BALL");
    public static ItemId Tm12Id { get; } = new("pret:ITEM_TM12");
    public static ItemId Tm21Id { get; } = new("pret:ITEM_TM21");
    public static ItemId Tm49Id { get; } = new("pret:ITEM_TM49");
    private sealed record Expected(PewterMachineItem Metadata, ImmutableArray<int> Effects, OpeningItemSourceRef? EffectSource);
    private static readonly Expected[] Additions = [
        new(new(new(new("pret:ITEM_LIFT_KEY"),"ITEM_LIFT_KEY",356,"LIFT KEY",0,1,0,"POCKET_KEY_ITEMS","ITEM_TYPE_BAG_MENU","HOLD_EFFECT_NONE",0,"FieldUseFunc_OakStopsYou",0,"NULL",0,false,false,false,new("src/data/items.json","ITEM_LIFT_KEY",5757,5772,"d962ec3ea31391f9a30fa7d12bfb47f6519407bc2aad395c99baec52430c1489")),"A key that operates the elevator\nin TEAM ROCKET's HIDEOUT.\nIt bears the TEAM ROCKET logo.","A key that operates the elevator\nin TEAM ROCKET's HIDEOUT.\nIt bears the TEAM ROCKET logo.",new("src/data/items.json","ITEM_LIFT_KEY",5757,5772,"d962ec3ea31391f9a30fa7d12bfb47f6519407bc2aad395c99baec52430c1489")),[],null),
        new(new(new(new("pret:ITEM_SILPH_SCOPE"),"ITEM_SILPH_SCOPE",359,"SILPH SCOPE",0,1,0,"POCKET_KEY_ITEMS","ITEM_TYPE_BAG_MENU","HOLD_EFFECT_NONE",0,"FieldUseFunc_OakStopsYou",0,"NULL",0,false,false,false,new("src/data/items.json","ITEM_SILPH_SCOPE",5805,5820,"d962ec3ea31391f9a30fa7d12bfb47f6519407bc2aad395c99baec52430c1489")),"A scope that makes unseeable\nPOKéMON visible.\nIt is made by SILPH CO.","A scope that makes unseeable\nPOKéMON visible.\nIt is made by SILPH CO.",new("src/data/items.json","ITEM_SILPH_SCOPE",5805,5820,"d962ec3ea31391f9a30fa7d12bfb47f6519407bc2aad395c99baec52430c1489")),[],null),
        new(new(new(new("pret:ITEM_BLACK_GLASSES"),"ITEM_BLACK_GLASSES",206,"BLACKGLASSES",100,0,0,"POCKET_ITEMS","ITEM_TYPE_BAG_MENU","HOLD_EFFECT_DARK_POWER",10,"FieldUseFunc_OakStopsYou",0,"NULL",0,true,true,true,new("src/data/items.json","ITEM_BLACK_GLASSES",3299,3314,"d962ec3ea31391f9a30fa7d12bfb47f6519407bc2aad395c99baec52430c1489")),"An item to be held by a POKéMON.\nA shady-looking pair of glasses\nthat boosts DARK-type moves.","An item to be held by a POKéMON.\nA shady-looking pair of glasses\nthat boosts DARK-type moves.",new("src/data/items.json","ITEM_BLACK_GLASSES",3299,3314,"d962ec3ea31391f9a30fa7d12bfb47f6519407bc2aad395c99baec52430c1489")),[],null),
        new(new(new(new("pret:ITEM_X_SPEED"),"ITEM_X_SPEED",77,"X SPEED",350,0,0,"POCKET_ITEMS","ITEM_TYPE_BAG_MENU","HOLD_EFFECT_NONE",0,"FieldUseFunc_OakStopsYou",2,"BattleUseFunc_StatBooster",0,true,true,true,new("src/data/items.json","ITEM_X_SPEED",1235,1250,"d962ec3ea31391f9a30fa7d12bfb47f6519407bc2aad395c99baec52430c1489")),"Raises the SPEED stat of POKéMON\nin battle. Wears off if the POKéMON\nis withdrawn.","Raises the SPEED stat of POKéMON\nin battle. Wears off if the POKéMON\nis withdrawn.",new("src/data/items.json","ITEM_X_SPEED",1235,1250,"d962ec3ea31391f9a30fa7d12bfb47f6519407bc2aad395c99baec52430c1489")),[0,1,0,0,0,96,1,1],new("src/data/pokemon/item_effects.h","sItemEffect_XSpeed",253,257,"d249420ab4751c60b1ae246895dcee5bdfb5bae18f52a9c37975c6f7fb5d0d25")),
        new(new(new(new("pret:ITEM_CALCIUM"),"ITEM_CALCIUM",67,"CALCIUM",9800,0,0,"POCKET_ITEMS","ITEM_TYPE_PARTY_MENU","HOLD_EFFECT_NONE",0,"FieldUseFunc_Medicine",0,"NULL",0,true,true,true,new("src/data/items.json","ITEM_CALCIUM",1075,1090,"d962ec3ea31391f9a30fa7d12bfb47f6519407bc2aad395c99baec52430c1489")),"A nutritious drink for POKéMON.\nIt raises the base SP. ATK stat\nof one POKéMON.","A nutritious drink for POKéMON.\nIt raises the base SP. ATK stat\nof one POKéMON.",new("src/data/items.json","ITEM_CALCIUM",1075,1090,"d962ec3ea31391f9a30fa7d12bfb47f6519407bc2aad395c99baec52430c1489")),[0,0,0,0,0,232,10,5,3,2],new("src/data/pokemon/item_effects.h","sItemEffect_Calcium",194,198,"d249420ab4751c60b1ae246895dcee5bdfb5bae18f52a9c37975c6f7fb5d0d25")),
        new(new(new(new("pret:ITEM_NET_BALL"),"ITEM_NET_BALL",6,"NET BALL",1000,0,0,"POCKET_POKE_BALLS","5","HOLD_EFFECT_NONE",0,"NULL",2,"BattleUseFunc_PokeBallEtc",5,true,true,true,new("src/data/items.json","ITEM_NET_BALL",99,114,"d962ec3ea31391f9a30fa7d12bfb47f6519407bc2aad395c99baec52430c1489")),"A somewhat different BALL that\nworks especially well on WATER- and\nBUG-type POKéMON.","A somewhat different BALL that\nworks especially well on WATER- and\nBUG-type POKéMON.",new("src/data/items.json","ITEM_NET_BALL",99,114,"d962ec3ea31391f9a30fa7d12bfb47f6519407bc2aad395c99baec52430c1489")),[],null),
        new(new(new(new("pret:ITEM_NEST_BALL"),"ITEM_NEST_BALL",8,"NEST BALL",1000,0,0,"POCKET_POKE_BALLS","7","HOLD_EFFECT_NONE",0,"NULL",2,"BattleUseFunc_PokeBallEtc",7,true,true,true,new("src/data/items.json","ITEM_NEST_BALL",131,146,"d962ec3ea31391f9a30fa7d12bfb47f6519407bc2aad395c99baec52430c1489")),"A somewhat different BALL that\nworks especially well on weaker\nPOKéMON.","A somewhat different BALL that\nworks especially well on weaker\nPOKéMON.",new("src/data/items.json","ITEM_NEST_BALL",131,146,"d962ec3ea31391f9a30fa7d12bfb47f6519407bc2aad395c99baec52430c1489")),[],null),
        new(new(new(new("pret:ITEM_TM12"),"ITEM_TM12",300,"TM12",3000,0,0,"POCKET_TM_CASE","ITEM_TYPE_PARTY_MENU","HOLD_EFFECT_NONE",0,"NULL",0,"NULL",0,true,true,true,new("src/data/items.json","ITEM_TM12",4814,4830,"d962ec3ea31391f9a30fa7d12bfb47f6519407bc2aad395c99baec52430c1489")),"The foe is taunted\ninto a rage that\nallows it to use\nonly attack moves.","A taunted foe may become enraged.\nIt will then only be able to use\nattack moves.",new("src/move_descriptions.c","gMoveDescription_Taunt",272,272,"1d6227e39ba85d694822a6b3a158c36b8ac3313ccf9e8d3264c67199bebaea0b")),[],null),
        new(new(new(new("pret:ITEM_TM21"),"ITEM_TM21",309,"TM21",1000,0,0,"POCKET_TM_CASE","ITEM_TYPE_PARTY_MENU","HOLD_EFFECT_NONE",0,"NULL",0,"NULL",0,true,true,true,new("src/data/items.json","ITEM_TM21",4967,4983,"d962ec3ea31391f9a30fa7d12bfb47f6519407bc2aad395c99baec52430c1489")),"This attack move\ngrows more powerful\nthe less the user\nlikes its TRAINER.","This attack move grows more\npowerful the more the POKéMON\ndislikes its TRAINER.",new("src/move_descriptions.c","gMoveDescription_Frustration",221,221,"1d6227e39ba85d694822a6b3a158c36b8ac3313ccf9e8d3264c67199bebaea0b")),[],null),
        new(new(new(new("pret:ITEM_TM49"),"ITEM_TM49",337,"TM49",3000,0,0,"POCKET_TM_CASE","ITEM_TYPE_PARTY_MENU","HOLD_EFFECT_NONE",0,"NULL",0,"NULL",0,true,true,true,new("src/data/items.json","ITEM_TM49",5443,5459,"d962ec3ea31391f9a30fa7d12bfb47f6519407bc2aad395c99baec52430c1489")),"Steals the effects\nof the foe's\nhealing or status-\nchanging move.","A move that steals the effects of\nany status-changing or healing move\nthat the foe tries to use.",new("src/move_descriptions.c","gMoveDescription_Snatch",292,292,"1d6227e39ba85d694822a6b3a158c36b8ac3313ccf9e8d3264c67199bebaea0b")),[],null),
    ];
    public static void ValidateSourceItem(PewterMachineItem metadata, ImmutableArray<int> effects, OpeningItemSourceRef? effectSource)
    {
        var expected = Additions.FirstOrDefault(e => e.Metadata.Item.Id == metadata?.Item.Id);
        if (expected is null || metadata != expected.Metadata || effects.IsDefault || !effects.SequenceEqual(expected.Effects) || effectSource != expected.EffectSource)
            throw new NotSupportedException("Rocket Hideout source item metadata differs.");
    }
    public static void ValidateMachine(PewterMachineItem machine)
    {
        if (machine?.Item.Id != Tm12Id && machine?.Item.Id != Tm21Id && machine?.Item.Id != Tm49Id)
            throw new NotSupportedException("Rocket Hideout machine identity differs.");
        ValidateSourceItem(machine!, [], null);
    }
    public static void ValidateShop(EarlyShopCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (catalog.Profile != Profile || catalog.Items.IsDefault || catalog.Items.Length != 56 ||
            catalog.Items.Any(i => i is null) || catalog.Items.Select(i => i.Id).Distinct().Count() != 56)
            throw new NotSupportedException("Exact Rocket Hideout 56-item profile required.");
        CeladonGymItemRules.ValidateShop(catalog with { Profile = CeladonGymItemRules.Profile, Items = catalog.Items.Take(46).ToImmutableArray() });
        for (int index = 0; index < Additions.Length; index++)
        {
            var expected = Additions[index]; var item = expected.Metadata.Item; var actual = catalog.Items[index + 46];
            var use = item.Id == NetBallId || item.Id == NestBallId ? EarlyShopUseSupport.SeparateCapture : EarlyShopUseSupport.NoFieldUse;
            if (actual.Id != item.Id || actual.SourceItemSymbol != item.SourceItemSymbol || actual.SourceItemId != item.SourceItemId ||
                actual.Name != item.Name || actual.Description != expected.Metadata.Description || actual.Price != item.Price ||
                actual.Pocket != item.Pocket || actual.Importance != item.Importance || actual.Type != item.Type ||
                actual.FieldUseFunction != item.FieldUseFunction || actual.BattleUsage != item.BattleUsage ||
                actual.BattleUseFunction != item.BattleUseFunction || actual.HoldEffectParam != item.HoldEffectParam ||
                actual.HealHp != 0 || actual.CureStatus != 0 || actual.EffectBytes.IsDefault || !actual.EffectBytes.SequenceEqual(expected.Effects) ||
                actual.UseSupport != use || actual.Source != item.Source || actual.EffectSource != expected.EffectSource || actual.SourceMetadata != item)
                throw new NotSupportedException("Rocket Hideout source item differs: " + item.SourceItemSymbol);
        }
    }
    public static bool IsBerry(ItemId item) => CeladonGymItemRules.IsBerry(item);
    public static string? ValidateBerries(InventoryState state) => CeladonGymItemRules.ValidateBerries(state);
}
