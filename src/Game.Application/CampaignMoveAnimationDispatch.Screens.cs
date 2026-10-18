using Game.Domain.Battle;
using Game.Domain.Pokemon;

namespace Game.Application;

public static partial class CampaignMoveAnimationPresentation
{
    private static CampaignMoveAnimationDecision BrokenScreenAnimation(Invocation battle,SourceMoveUsedEvent used,
        SourceAttackStringEvent attack,PokemonMoveDefinition definition,SourceBattleEvent[] action,SourcePpCommandEvent pp)
    {
        CampaignMoveAnimationDecision Reject()=>new(null,"unmatched-source-screen-break-result");
        if(definition.Target!="MOVE_TARGET_SELECTED"||definition.Power<=0||used.Actor==used.Target||
            used.Actor.Value<0||used.Actor.Value>=battle.Slots||used.Target.Value<0||used.Target.Value>=battle.Slots||action[0]!=pp)
            return Reject();
        var removed=action.OfType<SourceMtMoonVolatileEvent>().Where(e=>e.Reason=="brick-break").ToArray();
        var opposing=(BattleSide)((int)used.Actor.Side^1);
        var affected=used.Target.Side==opposing?used.Target:new BattlerSlot((int)opposing);
        if(removed.Length is <1 or >2||removed.Any(e=>e.Side!=affected||e.Volatile is not ("reflect" or "light-screen")||e.Before is <1 or >5||e.After!=0)||
            removed.Length==2&&(removed[0].Volatile!="reflect"||removed[1].Volatile!="light-screen")||
            !action.Skip(1).Take(removed.Length).SequenceEqual(removed))return Reject();
        // Removal precedes typecalc. The source temporarily clears immunity for
        // attackanimation, then restores it before damage; a Ghost still sees walls shatter.
        if(action.Any(e=>e is SourceMoveMissedEvent m&&m.Actor==used.Actor&&m.Move==used.Move||
            e is SourceMoveFailedEvent f&&f.Actor==used.Actor&&f.Move==used.Move||
            e is SourceAbilityPreventedEvent a&&a.Actor==used.Actor&&a.Move==used.Move))return Reject();
        var hits=action.Where(e=>e is SourceHpChangedEvent h&&h.Actor==used.Actor&&h.Move==used.Move||
            e is SourceSubstituteDamagedEvent s&&s.Actor==used.Actor&&s.Move==used.Move).ToArray();
        if(hits.Length!=1||hits[0].Sequence<=removed[^1].Sequence)return Reject();
        bool substitute=hits[0] is SourceSubstituteDamagedEvent;
        bool valid=hits[0] switch
        {
            SourceHpChangedEvent h=>h.Target==used.Target&&h.HpBefore>0&&h.HpAfter>=0&&h.HpAfter<=h.HpBefore&&h.Damage>=0,
            SourceSubstituteDamagedEvent h=>h.Target==used.Target&&h.HpBefore>0&&h.HpAfter>=0&&h.HpAfter<=h.HpBefore&&h.Damage>=0,
            _=>false
        };
        if(!valid)return Reject();
        return new(new(battle.Id,used.Sequence,attack.Sequence,pp.Sequence,removed[0].Sequence,definition.SourceMoveId,
            used.Actor,used.Target,substitute){MoveTurn=1},"committed-source-screen-break-animation");
    }
}
