using System.Collections.Immutable;
using Game.Domain.Ids;
namespace Game.Domain.Items;

/// <summary>Source metadata only. Generic inventory and item execution admission remain separate.</summary>
public static class PokemonTowerItemRules
{
    public const string Profile = "firered-pokemon-tower-items-v1";
    public const int InheritedItemCount = 56, ItemCount = 61;
    public static ItemId ElixirId { get; } = new("pret:ITEM_ELIXIR");
    public static ItemId CleanseTagId { get; } = new("pret:ITEM_CLEANSE_TAG");
    public static ItemId XAccuracyId { get; } = new("pret:ITEM_X_ACCURACY");
    public static ItemId BigMushroomId { get; } = new("pret:ITEM_BIG_MUSHROOM");
    public static ItemId SootheBellId { get; } = new("pret:ITEM_SOOTHE_BELL");
    public static ItemId PokeFluteId { get; } = new("pret:ITEM_POKE_FLUTE");
    private sealed record Expected(PewterMachineItem Metadata, ImmutableArray<int> Effects, OpeningItemSourceRef? EffectSource);
    private static readonly Expected[] Additions = [
        new(new(new(new("pret:ITEM_ELIXIR"),"ITEM_ELIXIR",36,"ELIXIR",3000,0,0,"POCKET_ITEMS","ITEM_TYPE_PARTY_MENU","HOLD_EFFECT_NONE",10,"FieldUseFunc_Ether",1,"BattleUseFunc_Ether",0,true,true,true,new("src/data/items.json","ITEM_ELIXIR",579,594)),"Restores the PP of all moves for\none POKéMON by 10 points each.","Restores the PP of all moves for\none POKéMON by 10 points each.",new("src/data/items.json","ITEM_ELIXIR",579,594)),[0,0,0,0,8,0,10],new("src/data/pokemon/item_effects.h","sItemEffect_Elixir",126,129)),
        new(new(new(new("pret:ITEM_CLEANSE_TAG"),"ITEM_CLEANSE_TAG",190,"CLEANSE TAG",200,0,0,"POCKET_ITEMS","ITEM_TYPE_BAG_MENU","HOLD_EFFECT_REPEL",0,"FieldUseFunc_OakStopsYou",0,"NULL",0,true,true,true,new("src/data/items.json","ITEM_CLEANSE_TAG",3043,3058)),"An item to be held by a POKéMON.\nIt repels wild POKéMON if the\nholder is first in the party.","An item to be held by a POKéMON.\nIt repels wild POKéMON if the\nholder is first in the party.",new("src/data/items.json","ITEM_CLEANSE_TAG",3043,3058)),[],null),
        new(new(new(new("pret:ITEM_X_ACCURACY"),"ITEM_X_ACCURACY",78,"X ACCURACY",950,0,0,"POCKET_ITEMS","ITEM_TYPE_BAG_MENU","HOLD_EFFECT_NONE",0,"FieldUseFunc_OakStopsYou",2,"BattleUseFunc_StatBooster",0,true,true,true,new("src/data/items.json","ITEM_X_ACCURACY",1251,1266)),"Raises the accuracy stat of\nPOKéMON in battle. Wears off if the\nPOKéMON is withdrawn.","Raises the accuracy stat of\nPOKéMON in battle. Wears off if the\nPOKéMON is withdrawn.",new("src/data/items.json","ITEM_X_ACCURACY",1251,1266)),[0,0,16,0,0,96,1,1],new("src/data/pokemon/item_effects.h","sItemEffect_XAccuracy",259,263)),
        new(new(new(new("pret:ITEM_SOOTHE_BELL"),"ITEM_SOOTHE_BELL",184,"SOOTHE BELL",100,0,0,"POCKET_ITEMS","ITEM_TYPE_BAG_MENU","HOLD_EFFECT_FRIENDSHIP_UP",0,"FieldUseFunc_OakStopsYou",0,"NULL",0,true,true,true,new("src/data/items.json","ITEM_SOOTHE_BELL",2947,2962)),"An item to be held by a POKéMON.\nA bell with a comforting chime that\nmakes the holder calm and friendly.","An item to be held by a POKéMON.\nA bell with a comforting chime that\nmakes the holder calm and friendly.",new("src/data/items.json","ITEM_SOOTHE_BELL",2947,2962)),[],null),
        new(new(new(new("pret:ITEM_POKE_FLUTE"),"ITEM_POKE_FLUTE",350,"POKé FLUTE",0,1,0,"POCKET_KEY_ITEMS","ITEM_TYPE_BAG_MENU","HOLD_EFFECT_NONE",0,"FieldUseFunc_PokeFlute",2,"BattleUseFunc_PokeFlute",0,false,false,false,new("src/data/items.json","ITEM_POKE_FLUTE",5661,5676)),"A flute that is said to instantly\nawaken any POKéMON. It has a\nlovely tone.","A flute that is said to instantly\nawaken any POKéMON. It has a\nlovely tone.",new("src/data/items.json","ITEM_POKE_FLUTE",5661,5676)),[],null),
    ];
    public static void ValidateSourceItem(PewterMachineItem metadata, ImmutableArray<int> effects, OpeningItemSourceRef? effectSource)
    {
        var expected = Additions.FirstOrDefault(e => e.Metadata.Item.Id == metadata?.Item.Id);
        if (expected is null || metadata != expected.Metadata || effects.IsDefault ||
            !effects.SequenceEqual(expected.Effects) || effectSource != expected.EffectSource)
            throw new NotSupportedException("Pokemon Tower source item metadata differs.");
    }
    public static void ValidateShop(EarlyShopCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (catalog.Profile != Profile || catalog.Items.IsDefault || catalog.Items.Length != ItemCount ||
            catalog.Items.Any(i => i is null) || catalog.Items.Select(i => i.Id).Distinct().Count() != ItemCount)
            throw new NotSupportedException("Exact Pokemon Tower 61-item metadata profile required.");
        RocketHideoutItemRules.ValidateShop(catalog with { Profile = RocketHideoutItemRules.Profile,
            Items = catalog.Items.Take(InheritedItemCount).ToImmutableArray() });
        for (int index = 0; index < Additions.Length; index++)
        {
            var expected = Additions[index]; var item = expected.Metadata.Item; var actual = catalog.Items[index + InheritedItemCount];
            if (actual.Id != item.Id || actual.SourceItemSymbol != item.SourceItemSymbol || actual.SourceItemId != item.SourceItemId ||
                actual.Name != item.Name || actual.Description != expected.Metadata.Description || actual.Price != item.Price ||
                actual.Pocket != item.Pocket || actual.Importance != item.Importance || actual.Type != item.Type ||
                actual.FieldUseFunction != item.FieldUseFunction || actual.BattleUsage != item.BattleUsage ||
                actual.BattleUseFunction != item.BattleUseFunction || actual.HoldEffectParam != item.HoldEffectParam ||
                actual.HealHp != 0 || actual.CureStatus != 0 || actual.EffectBytes.IsDefault || !actual.EffectBytes.SequenceEqual(expected.Effects) ||
                actual.UseSupport != EarlyShopUseSupport.NoFieldUse || actual.Source != item.Source ||
                actual.EffectSource != expected.EffectSource || actual.SourceMetadata != item)
                throw new NotSupportedException("Pokemon Tower source item differs: " + item.SourceItemSymbol);
        }
    }
}
