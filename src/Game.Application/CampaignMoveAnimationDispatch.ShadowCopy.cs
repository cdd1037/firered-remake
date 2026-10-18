using Game.Domain.Battle;
using Game.Domain.Pokemon;

namespace Game.Application;

public static partial class CampaignMoveAnimationPresentation
{
    private static CampaignMoveAnimationDecision NightmareAnimation(Invocation battle,SourceMoveUsedEvent used,
        SourceAttackStringEvent attack,PokemonMoveDefinition definition,SourceBattleEvent[] action,SourcePpCommandEvent[] pp)
    {
        if(definition.Power!=0||definition.Target!="MOVE_TARGET_SELECTED"||used.Actor.Value<0||used.Actor.Value>=battle.Slots||
            used.Target.Value<0||used.Target.Value>=battle.Slots||used.Actor==used.Target||
            !ValidSingleInvocationPp(pp,used,battle.Slots)||action.Length<2||action[0]!=pp[0]||
            action[1] is not SourceTowerVolatileEvent {Effect:"nightmare",Applied:true,Reason:"move"} result||
            result.Side!=used.Target||result.Sequence!=pp[0].Sequence+1||
            action.OfType<SourceTowerVolatileEvent>().Count(e=>e.Effect=="nightmare"&&e.Applied)!=1)
            return new(null,"unmatched-source-nightmare-result");
        // The committed result authenticates the successful branch whose animation
        // precedes seteffectprimary. Later damage/removal cannot replay that root.
        return new(new(battle.Id,used.Sequence,attack.Sequence,pp[0].Sequence,result.Sequence,
            definition.SourceMoveId,used.Actor,used.Target,false),"committed-source-animation");
    }
}
