using Game.Domain.Battle;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Application;

public static partial class CampaignMoveAnimationPresentation
{
    private static bool ValidSingleInvocationPp(SourcePpCommandEvent[] commands,SourceMoveUsedEvent used,int maximumCost)
    {
        if(commands.Length!=1)return false;
        var pp=commands[0];
        return pp.Actor==used.Actor&&pp.PpOwner==used.Actor&&pp.Target==used.Target&&
            pp.CurrentMove==used.Move&&pp.ChosenMove==used.Move&&pp.PpSlot==used.Slot&&
            pp.Before==used.PpBefore&&pp.After==used.PpAfter&&pp.Before is >=1 and <=255&&
            pp.After is >=0 and <=255&&pp.CommandCost>=1&&pp.CommandCost<=maximumCost&&
            pp.After==Math.Max(0,pp.Before-pp.CommandCost);
    }

    private static bool PreventedTarget(IEnumerable<SourceBattleEvent> action,SourceMoveUsedEvent used,BattlerSlot target)=>action.Any(e=>
        e is SourceMoveMissedEvent miss&&miss.Actor==used.Actor&&miss.Target==target&&miss.Move==used.Move||
        e is SourceMoveFailedEvent failed&&failed.Actor==used.Actor&&failed.Target==target&&failed.Move==used.Move||
        e is SourceAbilityPreventedEvent ability&&ability.Actor==used.Actor&&ability.Target==target&&ability.Move==used.Move||
        e is SourceEffectivenessEvent {Effectiveness:WildBattleEffectiveness.NoEffect} effect&&effect.Actor==used.Actor&&effect.Target==target&&effect.Move==used.Move);

    private static CampaignMoveAnimationDecision GroundSpreadAnimation(Invocation battle,SourceMoveUsedEvent used,
        SourceAttackStringEvent attack,PokemonMoveDefinition definition,SourceBattleEvent[] action,SourcePpCommandEvent[] commands)
    {
        CampaignMoveAnimationDecision Reject(string reason)=>new(null,reason);
        if(definition.Target!="MOVE_TARGET_FOES_AND_ALLY"||definition.Power<=0||used.Actor.Value<0||used.Actor.Value>=battle.Slots||used.Target.Value<0||used.Target.Value>=battle.Slots||used.Actor==used.Target||
            !ValidSingleInvocationPp(commands,used,battle.Slots)||action.Length<2||action[0]!=commands[0])
            return Reject("unmatched-source-ground-spread-pp");
        var pp=commands[0];
        ushort? power=null;
        var magnitudes=action.OfType<SourceMagnitudeEvent>().ToArray();
        if(definition.Effect=="EFFECT_MAGNITUDE")
        {
            if(magnitudes.Length!=1||magnitudes[0].Actor!=used.Actor||action[1]!=magnitudes[0]||
                magnitudes[0].Power!=(magnitudes[0].Magnitude switch{4=>10,5=>30,6=>50,7=>70,8=>90,9=>110,10=>150,_=>-1}))
                return Reject("unmatched-source-magnitude-result");
            power=checked((ushort)magnitudes[0].Power);
        }
        else if(magnitudes.Length!=0)return Reject("unexpected-source-magnitude-result");
        // HitsAllWithUndergroundBonus spends PP once, then visits every other
        // present slot in numeric order. Its first eligible pass owns the visual.
        var hits=action.Where(e=>e is SourceHpChangedEvent h&&h.Actor==used.Actor&&h.Move==used.Move||
            e is SourceSubstituteDamagedEvent s&&s.Actor==used.Actor&&s.Move==used.Move).Select(e=>e switch
            {
                SourceHpChangedEvent h=>(Row:e,h.Target,h.HpBefore,h.HpAfter,h.Damage,Substitute:false),
                SourceSubstituteDamagedEvent s=>(Row:e,s.Target,s.HpBefore,s.HpAfter,s.Damage,Substitute:true),
                _=>throw new InvalidOperationException()
            }).ToArray();
        if(hits.Length==0)return Reject("source-no-animation-result");
        if(hits.Length>=battle.Slots||hits.Select(h=>h.Target).Distinct().Count()!=hits.Length||
            hits.Any(h=>h.Target.Value<0||h.Target.Value>=battle.Slots||h.Target==used.Actor||h.Row.Sequence<=pp.Sequence||
                h.HpBefore<1||h.HpAfter<0||h.HpAfter>h.HpBefore||h.Damage<0)||
            battle.Slots==4&&!hits.Select(h=>h.Target.Value).SequenceEqual(hits.Select(h=>h.Target.Value).Order()))
            return Reject("unmatched-source-ground-spread-hit");
        foreach(var hit in hits)
        {
            if(PreventedTarget(action,used,hit.Target))continue;
            return new(new(battle.Id,used.Sequence,attack.Sequence,pp.Sequence,hit.Row.Sequence,definition.SourceMoveId,
                used.Actor,hit.Target,hit.Substitute){MovePower=power},"committed-source-animation");
        }
        return Reject("source-no-animation-result");
    }

    private static CampaignMoveAnimationDecision WithCommittedHitInputs(Invocation battle,SourceMoveUsedEvent used,
        SourceAttackStringEvent attack,PokemonMoveDefinition definition,SourceBattleEvent hit,CampaignMoveAnimationDispatch dispatch)
    {
        if(definition.Effect is "EFFECT_RAGE" or "EFFECT_PURSUIT"&&
            (definition.Target!="MOVE_TARGET_SELECTED"||definition.Power<=0||used.Actor==used.Target||
             used.Actor.Value<0||used.Actor.Value>=battle.Slots||used.Target.Value<0||used.Target.Value>=battle.Slots))
            return new(null,"unmatched-source-animation-hit-input");
        if(definition.Effect=="EFFECT_RAGE")
        {
            int damage=hit switch{SourceHpChangedEvent h=>h.Damage,SourceSubstituteDamagedEvent s=>s.Damage,_=>-1};
            if(damage<0)return new(null,"unmatched-source-animation-damage");
            dispatch=dispatch with{MoveDamage=damage};
        }
        if(definition.Effect=="EFFECT_PURSUIT")
        {
            byte turn=0;
            int markerIndex=battle.EventIndex-2;
            if(markerIndex>=0&&battle.Events[markerIndex] is SourcePursuitInterceptedEvent intercepted)
            {
                if(intercepted.Actor!=used.Actor||intercepted.SwitchingSide!=used.Target||intercepted.Slot!=used.Slot||
                    intercepted.Turn!=used.Turn||intercepted.Sequence+1!=attack.Sequence||
                    markerIndex>0&&battle.Events[markerIndex-1] is SourcePursuitInterceptedEvent)
                    return new(null,"unmatched-source-pursuit-interception");
                turn=1;
            }
            // Pursuit's doubled interception damage does not change the source
            // move-power register. Animation turn is independent of ACK ownership.
            dispatch=dispatch with{MovePower=checked((ushort)definition.Power),MoveTurn=turn};
        }
        return new(dispatch,"committed-source-animation");
    }
}
