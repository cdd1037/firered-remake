using Game.Domain.Battle;
using Game.Domain.Pokemon;

namespace Game.Application;

public static partial class CampaignMoveAnimationPresentation
{
    private static bool SoundHitDefinition(PokemonMoveDefinition definition)=>definition.Power>0&&
        definition.Target==(definition.Effect=="EFFECT_UPROAR"?"MOVE_TARGET_RANDOM":"MOVE_TARGET_SELECTED");

    private static CampaignMoveAnimationDecision TargetMarkAnimation(Invocation battle,SourceMoveUsedEvent used,
        SourceAttackStringEvent attack,PokemonMoveDefinition definition,SourceBattleEvent[] action,SourcePpCommandEvent[] commands)
    {
        // Both Mind Reader and Lock-On execute setalwayshitflag before the
        // animation. The later countdown is never an invocation owner, and a
        // refresh of an existing mark still exports the source command's 0→2.
        if(definition.Power!=0||definition.Target!="MOVE_TARGET_SELECTED"||
            used.Actor.Value<0||used.Actor.Value>=battle.Slots||used.Target.Value<0||used.Target.Value>=battle.Slots||
            used.Actor==used.Target||!ValidSingleInvocationPp(commands,used,2)||
            action.Length<2||action[0]!=commands[0]||attack.Sequence+1!=used.Sequence||commands[0].Sequence!=used.Sequence+1||
            action.Any(e=>e.Turn!=used.Turn)||action.Zip(action.Skip(1)).Any(pair=>pair.Second.Sequence!=pair.First.Sequence+1)||
            action[1] is not SourceMtMoonVolatileEvent {Volatile:"sure-hit",Before:0,After:2,Reason:"move"} marked||
            marked.Side!=used.Target||!CureMoveEndTail(action,2,used,battle.Slots))
            return new(null,"unmatched-source-target-mark");
        return new(new(battle.Id,used.Sequence,attack.Sequence,commands[0].Sequence,marked.Sequence,
            definition.SourceMoveId,used.Actor,used.Target,false),"committed-source-animation");
    }
}
