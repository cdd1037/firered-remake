using Game.Domain.Battle;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Application;

public static partial class CampaignMoveAnimationPresentation
{
    private static CampaignMoveAnimationDecision UtilityAnimation(Invocation battle,SourceMoveUsedEvent used,
        SourceAttackStringEvent attack,PokemonMoveDefinition definition,SourceBattleEvent[] action,SourcePpCommandEvent[] commands)
    {
        CampaignMoveAnimationDecision Reject(string reason)=>new(null,reason);
        bool field=definition.Effect=="EFFECT_SPIKES",self=definition.Effect=="EFFECT_GRUDGE";
        if(definition.Power!=0||definition.Target!=(field?"MOVE_TARGET_OPPONENTS_FIELD":self?"MOVE_TARGET_USER":"MOVE_TARGET_SELECTED")||
            used.Actor.Value<0||used.Actor.Value>=battle.Slots||used.Target.Value<0||used.Target.Value>=battle.Slots||
            (self?used.Target!=used.Actor:used.Target==used.Actor)||field&&used.Target.Side==used.Actor.Side||
            !ValidSingleInvocationPp(commands,used,field?1+battle.Slots/2:self?1:2)||
            action.Length<2||action[0]!=commands[0]||commands[0].Sequence!=used.Sequence+1||
            action.Any(e=>e.Turn!=used.Turn)||action.Zip(action.Skip(1)).Any(pair=>pair.Second.Sequence!=pair.First.Sequence+1))
            return Reject("unmatched-source-utility-invocation");
        int following=battle.EventIndex+1+action.Length;
        if(following<battle.Events.Length&&battle.Events[following] is not
            (SourceAttackStringEvent or SourceMoveUsedEvent or SourceOrderEvent or SourceTurnReadyEvent or SourcePpCommandEvent)&&
            battle.Events[following].Turn!=used.Turn)
            return Reject("unmatched-source-utility-turn-boundary");
        var pp=commands[0];SourceBattleEvent result;int tail;
        switch(definition.Effect)
        {
            case "EFFECT_ENCORE":
                // trysetencore succeeds before attackanimation. Countdown and
                // exhaustion/overwrite expiry are not a new command success.
                if(action[1] is not SourceEncoreChangedEvent {Before:0,After:>=3 and <=6,Slot:>=0 and <=3,Reason:"move"} encore||
                    encore.Side!=used.Target)return Reject("unmatched-source-encore-result");
                result=encore;tail=2;break;
            case "EFFECT_YAWN":
                // setyawn succeeds before attackanimation; countdown and later
                // sleep are different source phases and never own this root.
                if(action[1] is not SourceYawnEvent {Before:0,After:2,Reason:"move"} yawn||yawn.Side!=used.Target)
                    return Reject("unmatched-source-yawn-result");
                result=yawn;tail=2;break;
            case "EFFECT_GRUDGE":
                // The later action-attempt clear and knockout PP loss cannot
                // replace the successful trysetgrudge command receipt.
                if(action[1] is not SourceMtMoonVolatileEvent {Volatile:"grudge",Before:0,After:1,Reason:"move"} grudge||grudge.Side!=used.Actor)
                    return Reject("unmatched-source-grudge-result");
                result=grudge;tail=2;break;
            case "EFFECT_SKILL_SWAP":
                if(action.Length<3||action[1] is not SourceCalledOverlayChangedEvent own||
                    action[2] is not SourceCalledOverlayChangedEvent target||
                    !UtilityAbilityWrite(own,used.Actor,used)||!UtilityAbilityWrite(target,used.Target,used)||
                    own.After!.Ability=="ABILITY_NONE"&&target.After!.Ability=="ABILITY_NONE"||
                    own.Before?.Ability is {} oldOwn&&target.After!.Ability!=oldOwn||
                    target.Before?.Ability is {} oldTarget&&own.After!.Ability!=oldTarget)
                    return Reject("unmatched-source-skill-swap-result");
                // Both assignments finish before the source animation. Equal
                // abilities and one ABILITY_NONE are legitimate successful swaps.
                result=target;tail=3;break;
            case "EFFECT_SPIKES":
                // trysetspikes precedes attackstring/ppreduce. The canonical
                // result marker follows PP; neither receipt alone proves the
                // complete source command. Rapid Spin and entry damage differ.
                int beforeAttack=battle.EventIndex-2;
                if(beforeAttack<0||battle.Events[beforeAttack] is not SourceCalledSpikesChangedEvent layers||
                    layers.Turn!=used.Turn||layers.Side==used.Actor.Side||!Enum.IsDefined(layers.Side)||
                    layers.Before is <0 or >2||layers.After!=layers.Before+1||layers.Sequence+1!=attack.Sequence||
                    beforeAttack>0&&battle.Events[beforeAttack-1] is SourceCalledSpikesChangedEvent previous&&previous.Turn==used.Turn||
                    attack.Sequence+1!=used.Sequence||
                    action[1] is not SourceMtMoonVolatileEvent {Volatile:"spikes",Before:0,After:1,Reason:"move"} spikes||spikes.Side!=used.Actor)
                    return Reject("unmatched-source-spikes-result");
                result=spikes;tail=2;break;
            default:return Reject("not-source-utility-animation");
        }
        // These successful non-damaging commands share the already-reviewed
        // immunity/cure-item/active-party tail, not arbitrary later outcomes.
        if(!CureMoveEndTail(action,tail,used,battle.Slots))return Reject("unmatched-source-utility-tail");
        return new(new(battle.Id,used.Sequence,attack.Sequence,pp.Sequence,result.Sequence,
            definition.SourceMoveId,used.Actor,used.Target,false),"committed-source-animation");
    }

    private static bool UtilityAbilityWrite(SourceCalledOverlayChangedEvent row,BattlerSlot owner,SourceMoveUsedEvent used)
    {
        if(row.Side!=owner||row.Source is not null||row.Move!=used.Move||row.After is not {} after||
            after.RulesVersion!=WildBattleRules.CalledMoveKernelRulesVersion||after.Ability is not {} ability||
            !UtilityAbility(ability)||ability=="ABILITY_WONDER_GUARD"||
            row.Before is {} before&&(before.RulesVersion!=WildBattleRules.CalledMoveKernelRulesVersion||
                before.Ability is {} old&&!UtilityAbility(old)))return false;
        return after==((row.Before??new WildBattleCalledOverlay()) with{Ability=ability});
    }
    private static bool UtilityAbility(string value)=>value.StartsWith("ABILITY_",StringComparison.Ordinal)&&value.Length>8&&
        value[8..].All(c=>c is >= 'A' and <= 'Z' or >= '0' and <= '9' or '_');
}
