using System.Collections.Immutable;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Options;
using Game.Domain.WildBattle;
namespace Game.Application;

public sealed record CampaignCastformAnimationDispatch(string BattleId,long Sequence,long AbilitySequence,
    BattlerSlot Affected,WildBattleCombatant BeforeBody,WildBattleCombatant AfterBody,bool Immediate,ImmutableArray<bool> SpriteVisibility);

/// <summary>Authenticates the source Forecast form receipt using the event-time display body.
/// Its general animation is independent of the ordinary Battle Scene option.</summary>
public static class CampaignCastformAnimationPresentation
{
    private static void ValidateEntry(IReadOnlyList<MoveId?> entry,int count)
    {ArgumentNullException.ThrowIfNull(entry);if(entry.Count!=count)throw new ArgumentException("One pre-command charge move is required for each battle position.",nameof(entry));}
    private static CampaignCastformAnimationDispatch? Match(string id,IReadOnlyList<SourceBattleEvent> events,int index,
        WildBattleCombatant display,bool applied,IReadOnlyList<MoveId?> entry)
    {
        if(index<1||index>=events.Count||events[index] is not SourceCalledOverlayChangedEvent receipt||
            events[index-1] is not SourceAbilityActivatedEvent {Ability:"ABILITY_FORECAST"} ability||
            ability.Side!=receipt.Side||ability.Turn!=receipt.Turn||ability.Sequence+1!=receipt.Sequence||
            receipt.Move.Value!="pret:MOVE_NONE"||receipt.Source is not null||receipt.Side.Value>=entry.Count||
            receipt.After?.CastformForm is not {} form||!Enum.IsDefined(form)||
            receipt.Before==receipt.After||display.Pokemon.Species.Value!="pret:SPECIES_CASTFORM"||display.Pokemon.CurrentHp<=0)
            return null;
        for(int i=1;i<=index;i++)if(events[i].Sequence<=events[i-1].Sequence)return null;
        string expected=form switch{SourceCastformForm.Normal=>"TYPE_NORMAL",SourceCastformForm.Sunny=>"TYPE_FIRE",SourceCastformForm.Rainy=>"TYPE_WATER",_=>"TYPE_ICE"};
        var before=receipt.Before;
        var expectedAfter=(before??new WildBattleCalledOverlay()) with{Type1=expected,Type2=expected,CastformForm=form};
        if(receipt.After!=expectedAfter||display.CalledOverlay!=(applied?receipt.After:before))return null;
        var oldBody=display with{CalledOverlay=before};
        if(WildBattleEffective.Ability(oldBody)!="ABILITY_FORECAST"||oldBody.CalledVolatile?.SubstituteHp>0)return null;
        var visibility=ImmutableArray.CreateBuilder<bool>(entry.Count);
        for(int slot=0;slot<entry.Count;slot++)
        {
            bool? visible=CampaignHeldItemAnimationPresentation.SpriteVisibleAtResult(events,index,new(slot),entry);
            if(visible is null)return null;
            visibility.Add(visible.Value);
        }
        var snapshot=visibility.MoveToImmutable();
        return new(id,receipt.Sequence,ability.Sequence,receipt.Side,oldBody,oldBody with{CalledOverlay=receipt.After},!snapshot[receipt.Side.Value],snapshot);
    }
    public static CampaignCastformAnimationDispatch? AtResult(CampaignWildBattle battle,bool sourceRules,SourceBattleScene scene,IReadOnlyList<MoveId?> entryChargeMoves)
    {
        ValidateEntry(entryChargeMoves,2);
        if(!sourceRules||battle.Phase!=CampaignBattlePhase.Events||battle.EventIndex<0||battle.EventIndex>=battle.Events.Length||battle.DisplayBatch is not {} batch)return null;
        if(battle.Events[battle.EventIndex] is not WildBattleCalledOverlayChangedEvent legacy)return null;
        long sequence=legacy.Sequence;int index=-1;
        for(int i=0;i<battle.SourceEvents.Length;i++)if(battle.SourceEvents[i].Sequence==sequence){index=i;break;}
        if(index<0||batch.NextEventIndex!=index+1||battle.SourceEvents[index] is not SourceCalledOverlayChangedEvent receipt||receipt.Side.Value>=batch.Display.Bodies.Length)return null;
        if(legacy.Turn!=receipt.Turn||(int)legacy.Side!=receipt.Side.Value||legacy.Move!=receipt.Move||legacy.Before!=receipt.Before||legacy.After!=receipt.After)return null;
        return Match(battle.Id,battle.SourceEvents,index,batch.Display.Bodies[receipt.Side.Value],true,entryChargeMoves);
    }
    public static CampaignCastformAnimationDispatch? AtResult(CampaignDoubleBattle battle,SourceBattleScene scene,IReadOnlyList<MoveId?> entryChargeMoves)
    {
        ValidateEntry(entryChargeMoves,4);
        if(battle.Stage!=CampaignDoubleBattleStage.Combat||battle.EventIndex<0||battle.EventIndex>=battle.Events.Length||
            battle.Events[battle.EventIndex] is not SourceCalledOverlayChangedEvent receipt||receipt.Side.Value>=battle.DisplayedSlots.Length)return null;
        return Match(battle.Id,battle.Events,battle.EventIndex,battle.DisplayedSlots[receipt.Side.Value],false,entryChargeMoves);
    }
}
