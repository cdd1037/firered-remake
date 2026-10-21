using Game.Domain.Battle;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Application;

public static partial class CampaignMoveAnimationPresentation
{
    private static CampaignMoveAnimationDecision PsychUpAnimation(Invocation battle,SourceMoveUsedEvent used,
        SourceAttackStringEvent attack,PokemonMoveDefinition definition,SourceBattleEvent[] action,SourcePpCommandEvent pp)
    {
        if(definition.Power!=0||definition.Target!="MOVE_TARGET_SELECTED"||used.Actor.Value<0||used.Actor.Value>=battle.Slots||
            used.Target.Value<0||used.Target.Value>=battle.Slots||used.Actor==used.Target||action.Length<2||action[0]!=pp)
            return new(null,"unmatched-source-psych-up-invocation");
        var markers=action.OfType<SourceMtMoonVolatileEvent>().Where(e=>e.Volatile=="psych-up").ToArray();
        if(markers.Length!=1||markers[0] is not {Before:0,After:0,Reason:"all-stat-stages-copied"} marker||marker.Side!=used.Actor)
            return new(null,"unmatched-source-psych-up-result");
        int end=Array.IndexOf(action,marker);if(end is <1 or >8)return new(null,"unmatched-source-psych-up-prefix");
        int previous=-1;
        for(int i=1;i<end;i++)
        {
            if(action[i] is not SourceStatChangedEvent stat||stat.Actor!=used.Actor||stat.Target!=used.Actor||
                stat.Before is <0 or >12||stat.After is <0 or >12||stat.Before==stat.After||stat.Sequence!=action[i-1].Sequence+1)
                return new(null,"unmatched-source-psych-up-stats");
            int order=stat.Stat switch{WildBattleStat.Attack=>0,WildBattleStat.Defense=>1,WildBattleStat.Speed=>2,
                WildBattleStat.SpecialAttack=>3,WildBattleStat.SpecialDefense=>4,WildBattleStat.Accuracy=>5,WildBattleStat.Evasion=>6,_=>-1};
            if(order<=previous)return new(null,"unmatched-source-psych-up-order");previous=order;
        }
        if(marker.Sequence!=action[end-1].Sequence+1)return new(null,"unmatched-source-psych-up-marker");
        // Source copyfoestats always succeeds, including identical stages. Only
        // changed assignments emit rows; the committed marker authenticates the
        // completed command without reading either battler's later stat stages.
        return new(new(battle.Id,used.Sequence,attack.Sequence,pp.Sequence,marker.Sequence,
            definition.SourceMoveId,used.Actor,used.Target,false),"committed-source-animation");
    }
}
