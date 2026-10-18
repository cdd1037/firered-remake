using Game.Domain.Battle;
using Game.Domain.Pokemon;

namespace Game.Application;

public static partial class CampaignMoveAnimationPresentation
{
    private static CampaignMoveAnimationDecision WithRolloutInputs(Invocation battle,PokemonMoveDefinition definition,
        SourceBattleEvent hit,CampaignMoveAnimationDispatch dispatch,bool retained)
    {
        if(definition.Effect!="EFFECT_ROLLOUT"||definition.Target!="MOVE_TARGET_SELECTED"||definition.Power<=0||
            dispatch.Actor.Value<0||dispatch.Actor.Value>=battle.Slots||dispatch.Target.Value<0||dispatch.Target.Value>=battle.Slots||
            dispatch.Actor==dispatch.Target||CommittedHit(hit,battle.Slots) is not {} committed||
            committed.Actor!=dispatch.Actor||committed.Target!=dispatch.Target||committed.Move!=definition.Id)
            return new(null,"unmatched-source-rollout-hit");
        var prefix=battle.Events.Skip(battle.EventIndex+1).TakeWhile(e=>e.Sequence<hit.Sequence).ToArray();
        var markers=prefix.OfType<SourceMtMoonVolatileEvent>().Where(e=>e.Volatile=="called-rollout").ToArray();
        int markerIndex=retained?0:1;
        if(markers.Length!=1||prefix.Any(e=>e is SourceCallerSelectionEvent or SourceCalledMoveTransitionEvent)||
            markers[0].Side!=dispatch.Actor||markers[0].Turn!=hit.Turn||markers[0].Sequence<=dispatch.SnapshotSequence||
            (retained?markers[0].Before is <1 or >4||markers[0].After!=markers[0].Before-1||
                markers[0].Reason!=(markers[0].After==0?"finished":"countdown"):
                markers[0].Before!=0||markers[0].After!=4||markers[0].Reason!="move")||
            prefix.Length<markerIndex+1||prefix.Length>markerIndex+2||prefix[markerIndex]!=markers[0]||
            !retained&&(prefix[0] is not SourcePpCommandEvent pp||pp.Sequence!=dispatch.PpSequence))
            return new(null,"unmatched-source-rollout-counter");
        if(prefix.Length==markerIndex+2)
        {
            // adjustnormaldamage may preserve a one-HP target through Endure or
            // Focus Band before the HP command. It does not change move power.
            if(hit is not SourceHpChangedEvent hp||prefix[^1] is not SourceDamageAdjustedEvent adjusted||
                adjusted.Target!=dispatch.Target||adjusted.Move!=definition.Id||adjusted.Turn!=hit.Turn||
                adjusted.DamageBefore<hp.HpBefore||adjusted.DamageAfter!=hp.Damage||hp.HpAfter!=1||
                hp.Damage!=hp.HpBefore-1||adjusted.Endured==adjusted.HungOn||
                adjusted.HungOn&&adjusted.Item!="ITEM_FOCUS_BAND")return new(null,"unmatched-source-rollout-adjustment");
        }
        // The source decrements its initialized five-turn timer before animation.
        // Use the owned command receipt, including its 1→0 final hit, never the
        // final frame (whose lock is already removed) or the damage magnitude.
        int counter=5-markers[0].After;
        int? power=hit switch{SourceHpChangedEvent h=>h.PreparedBasePower,SourceSubstituteDamagedEvent s=>s.PreparedBasePower,_=>null};
        int ordinary=definition.Power<<(counter-1);
        if(power is null or <=0 or >ushort.MaxValue||(power!=ordinary&&power!=ordinary*2))
            return new(null,"unmatched-source-rollout-power");
        return new(dispatch with{RolloutCounter=checked((byte)counter),MovePower=checked((ushort)power.Value)},
            retained?"committed-source-retained-animation":"committed-source-animation");
    }
}
