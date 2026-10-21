using Game.Domain.Battle;
using Game.Domain.Pokemon;

namespace Game.Application;

public static partial class CampaignMoveAnimationPresentation
{
    private static CampaignMoveAnimationDecision FutureAttackSetup(Invocation battle,SourceMoveUsedEvent used,
        SourceAttackStringEvent attack,PokemonMoveDefinition definition,SourceBattleEvent[] action,SourcePpCommandEvent pp)
    {
        // Both delayed-attack moves share trysetfutureattack before attackanimation.
        // The later impact belongs to a separate general animation and never
        // authorizes replay of this setup. The effect's first animTurn is zero.
        if((used.Actor.Value<0||used.Actor.Value>=battle.Slots)||(used.Target.Value<0||used.Target.Value>=battle.Slots)||used.Actor==used.Target||
            action.Length<2||action[0]!=pp||action[1] is not SourceCalledDelayedEvent {Before:0,After:3,Reason:"scheduled",SourcePartyIndex:null} scheduled||
            scheduled.Target!=used.Target||scheduled.Move!=used.Move||scheduled.Sequence<=pp.Sequence||
            action.OfType<SourceCalledDelayedEvent>().Count(e=>e.Reason=="scheduled")!=1)
            return new(null,"unmatched-source-future-setup");
        return new(new(battle.Id,used.Sequence,attack.Sequence,pp.Sequence,scheduled.Sequence,definition.SourceMoveId,
            used.Actor,used.Target,false){MoveTurn=0},"committed-source-animation");
    }
}
