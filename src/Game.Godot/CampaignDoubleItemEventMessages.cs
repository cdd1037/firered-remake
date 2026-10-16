using System.Collections.Immutable;
using Game.Application;
using Game.Domain.Battle;
using Game.Domain.WildBattle;

public partial class CampaignView
{
    private SourceTrainerEventDisplay? DoubleItemEventDisplay(CampaignDoubleBattle b,SourceBattleEvent e)
    {
        SourceHeldItemConsumedEvent? Following(BattlerSlot slot)=>b.Events.Where(next=>next.Sequence>e.Sequence)
            .TakeWhile(next=>next is not (SourceAttackStringEvent or SourceMoveUsedEvent or SourceMedicineAppliedEvent))
            .OfType<SourceHeldItemConsumedEvent>().FirstOrDefault(next=>next.Side==slot);
        switch(e)
        {
            case SourceStatChangedEvent x when Following(x.Target)?.StatChanges is {} changes&&changes.Any(c=>c.Stat==x.Stat&&c.Before==x.Before&&c.After==x.After):return DoubleSilent;
            case SourceFocusEnergyEvent x when Following(x.Side)?.FocusEnergyRaised==true:return DoubleSilent;
            case SourceConfusionChangedEvent {Reason:"flavor-berry"} x when Following(x.Side)?.Flavor is not null:return DoubleSilent;
            case SourceConfusionChangedEvent {Reason:"item",After:0} x:return DoubleText("gText_PkmnSnappedOutOfConfusion",("STR_VAR_1",DoubleName(x.Side.Value)));
            case SourceInfatuationChangedEvent {Reason:"item",After:null} x:return DoubleText("gText_PkmnGotOverInfatuation",("STR_VAR_1",DoubleName(x.Side.Value)));
            case SourceMtMoonVolatileEvent {Volatile:"mist",Reason:"item"} x:return DoubleText("gBattleText_MistShroud",("B_ATK_PREFIX2",x.Side.Side==BattleSide.Player?"Ally":"Foe"));
            case SourceMedicineAppliedEvent {Actor:{} actor} x:
                var pages=ImmutableArray.CreateBuilder<SourceTrainerText>();
                pages.Add(new(actor.Side==BattleSide.Player?"sText_PlayerUsedItem":"sText_Trainer1UsedItem",("B_LAST_ITEM",DoubleItem(x.Item.Value.Replace("pret:","",StringComparison.Ordinal)))));
                string name=DoublePartyName(b,x.TargetSide??BattleSide.Player,x.PartyIndex);
                if(x.HpAfter>x.HpBefore)pages.Add(new("gText_PkmnHPRestoredByVar2",("STR_VAR_1",name),("STR_VAR_2",(x.HpAfter-x.HpBefore).ToString())));
                else if(x.StatusBefore!=x.StatusAfter&&x.StatusAfter==0)pages.Add(new("gText_PkmnBecameHealthy",("STR_VAR_1",name)));
                if(x.PpRecovery is {IsEmpty:false})pages.Add(new("gText_PPWasRestored"));
                return new(pages.ToImmutable());
            case SourceHeldItemConsumedEvent x:
                SourceTrainerText Text(string symbol,params (string Key,string Value)[] values)=>new(symbol,
                    new[]{("B_SCR_ACTIVE_NAME_WITH_PREFIX",DoublePrefixed(x.Side)),("B_LAST_ITEM",DoubleItem(x.Item))}.Concat(values).ToArray());
                if(x.PpRecovery is {} pp)return new([Text("sText_PkmnsItemRestoredPP",("B_BUFF1",MoveName(pp.Move)))]);
                if(x.Item=="ITEM_WHITE_HERB")return new([Text("sText_PkmnsItemRestoredStatus")]);
                if(x.Item=="ITEM_MENTAL_HERB")return new([Text("sText_PkmnsItemCuredProblem",("B_BUFF1","love"))]);
                if(x.FocusEnergyRaised==true)return new([Text("sText_PkmnUsedXToGetPumped")]);
                if(x.StatChanges is {} stats&&!stats.IsEmpty)return new(stats.Where(c=>c.After>c.Before).Select(c=>Text("sText_UsingItemTheStatOfPkmnRose",
                    ("B_BUFF1",WildStatName(c.Stat)),("B_BUFF2",x.Item=="ITEM_STARF_BERRY"?"rose sharply!":"rose!"))).ToImmutableArray());
                if(x.Flavor is {} flavor&&DoublePrior(b,x,p=>p is SourceConfusionChangedEvent {Reason:"flavor-berry"} c&&c.Side==x.Side))
                {
                    string symbol=flavor switch {"spicy"=>"sText_PokeblockWasTooSpicy","dry"=>"sText_PokeblockWasTooDry","sweet"=>"sText_PokeblockWasTooSweet","bitter"=>"sText_PokeblockWasTooBitter","sour"=>"sText_PokeblockWasTooSour",_=>throw new InvalidDataException("Held berry flavor differs.")};
                    string sourceFlavor=string.Join(' ',DoubleSource(symbol));
                    return new([Text("sText_PkmnsItemRestoredHealth"),Text("sText_ForXCommaYZ",("B_BUFF1",sourceFlavor)),
                        new("sText_PkmnWasConfused",("B_EFF_NAME_WITH_PREFIX",DoublePrefixed(x.Side)))]);
                }
                return null;
            default:return null;
        }
    }
}
