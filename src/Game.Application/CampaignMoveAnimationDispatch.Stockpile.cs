using Game.Domain.Battle;
using Game.Domain.Pokemon;

namespace Game.Application;

public static partial class CampaignMoveAnimationPresentation
{
    private static CampaignMoveAnimationDecision StockpileAnimation(Invocation battle,SourceMoveUsedEvent used,
        SourceAttackStringEvent attack,PokemonMoveDefinition definition,SourceBattleEvent[] action,SourcePpCommandEvent pp)
    {
        bool swallow=definition.Effect=="EFFECT_SWALLOW";
        if(definition.Power!=0||definition.Target!="MOVE_TARGET_USER"||used.Target!=used.Actor||used.Actor.Value<0||used.Actor.Value>=battle.Slots||
            action.Length<(swallow?3:2)||action[0]!=pp||action[1] is not SourceMtMoonVolatileEvent changed||
            changed.Side!=used.Actor||changed.Volatile!="stockpile"||changed.Sequence!=pp.Sequence+1||
            action.OfType<SourceMtMoonVolatileEvent>().Count(e=>e.Volatile=="stockpile")!=1)
            return new(null,"unmatched-source-stockpile-result");
        if(!swallow)
        {
            if(changed.Before is <0 or >2||changed.After!=changed.Before+1||changed.Reason!="move")
                return new(null,"unmatched-source-stockpile-result");
            return new(new(battle.Id,used.Sequence,attack.Sequence,pp.Sequence,changed.Sequence,definition.SourceMoveId,used.Actor,used.Actor,false),"committed-source-animation");
        }
        // stockpiletohpheal freezes animTurn before consuming the counter.
        // Full HP consumes it too, but has no recovery and must not animate.
        if(changed.Before is <1 or >3||changed.After!=0||changed.Reason!="swallow"||
            action[2] is not SourceRecoveredEvent recovered||recovered.Side!=used.Actor||recovered.Move!=used.Move||
            recovered.Sequence!=changed.Sequence+1||recovered.HpBefore<=0||recovered.HpAfter<=recovered.HpBefore||
            recovered.Recovered!=recovered.HpAfter-recovered.HpBefore||action.OfType<SourceRecoveredEvent>().Count(e=>e.Move==used.Move)!=1)
            return new(null,"unmatched-source-swallow-result");
        return new(new(battle.Id,used.Sequence,attack.Sequence,pp.Sequence,recovered.Sequence,definition.SourceMoveId,used.Actor,used.Actor,false)
            {MoveTurn=checked((byte)changed.Before)},"committed-source-animation");
    }
}
