using Game.Domain.Battle;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Application;

public static partial class CampaignMoveAnimationPresentation
{
    // Damaging Present uses the ordinary shared hit route. Only its distinct
    // selected-target healing branch requires this recovery-owned admission.
    private static CampaignMoveAnimationDecision PresentRecoveryAnimation(Invocation battle,SourceMoveUsedEvent used,
        SourceAttackStringEvent attack,PokemonMoveDefinition definition,SourceBattleEvent[] action,SourcePpCommandEvent[] pp)
    {
        CampaignMoveAnimationDecision Reject(string reason)=>new(null,reason);
        int end=battle.EventIndex+1+action.Length;
        if(definition.Effect!="EFFECT_PRESENT"||definition.Power!=1||definition.Type!="TYPE_NORMAL"||definition.Target!="MOVE_TARGET_SELECTED"||
            used.Actor==used.Target||used.Actor.Value<0||used.Actor.Value>=battle.Slots||used.Target.Value<0||used.Target.Value>=battle.Slots||
            !ValidSingleInvocationPp(pp,used,2)||action.Length<2||action[0]!=pp[0]||pp[0].Sequence!=used.Sequence+1||
            attack.Sequence+1!=used.Sequence||action.Any(e=>e.Turn!=used.Turn)||
            action.Zip(action.Skip(1)).Any(pair=>pair.Second.Sequence!=pair.First.Sequence+1)||
            end<battle.Events.Length&&battle.Events[end].Turn!=used.Turn&&!InvocationBoundary(battle.Events[end]))
            return Reject("unmatched-source-present-invocation");
        // This path cannot contain a foreign result or another move's failure.
        // Wonder Guard keeps MISSED and emits zero recovery: no animation.
        if(action[1] is not SourceRecoveredEvent recovery||recovery.Side!=used.Target||recovery.Move!=used.Move||
            recovery.HpBefore<=0||recovery.HpAfter<=recovery.HpBefore||recovery.Recovered!=recovery.HpAfter-recovery.HpBefore||
            action.Count(e=>e is SourceRecoveredEvent or SourceHpChangedEvent or SourceSubstituteDamagedEvent)!=1||
            PreventedTarget(action,used,used.Target))return Reject("unmatched-source-present-recovery");
        var synchronized=new HashSet<BattlerSlot>();int lastSource=-1;
        bool Sync(SourceBattleEvent[] rows,int index,SourceCalledPartyChangedEvent sync,SourceMoveUsedEvent owner)
        {
            if(sync.SourceSlot is not {} source||source.Value<=lastSource||source.Value>=battle.Slots||source.Side!=sync.Side||
                sync.TransientMoveSlots is not {} transient||transient>15||sync.Before?.Pokemon is null||sync.After?.Pokemon is null||
                !synchronized.Add(source))return false;
            lastSource=source.Value;
            var before=sync.Before;var after=sync.After;bool hpChanged=before.Pokemon.CurrentHp!=after.Pokemon.CurrentHp;
            if(before.Pokemon.Stats.MaxHp<=0||before.Pokemon.CurrentHp<0||before.Pokemon.CurrentHp>before.Pokemon.Stats.MaxHp||
                after.Pokemon.CurrentHp<0||after.Pokemon.CurrentHp>after.Pokemon.Stats.MaxHp)return false;
            if(source==used.Target)
            {
                if(before.Pokemon.CurrentHp!=recovery.HpBefore||after.Pokemon.CurrentHp!=recovery.HpAfter||
                    recovery.HpAfter!=Math.Min((long)before.Pokemon.Stats.MaxHp,recovery.HpBefore+Math.Max(1L,before.Pokemon.Stats.MaxHp/4)))return false;
            }
            else if(hpChanged)return false;
            if(source!=used.Actor&&!before.Pokemon.Moves.SequenceEqual(after.Pokemon.Moves))return false;
            if(before.Status!=after.Status&&!rows.Take(index).OfType<SourceStatusChangedEvent>().Any(e=>e.Side==source&&
                SourcePartyStatusRules.FromBattle(e.Before)==before.Status&&SourcePartyStatusRules.FromBattle(e.After)==after.Status))return false;
            if((before.HeldItem!=after.HeldItem||before.PersistentHeldItemOverride!=after.PersistentHeldItemOverride)&&
                !rows.Take(index).OfType<SourceHeldItemConsumedEvent>().Any(e=>e.Side==source&&e.Item==before.HeldItem))return false;
            var normalized=sync with{Before=before with{Pokemon=before.Pokemon with{CurrentHp=after.Pokemon.CurrentHp}}};
            return CureActiveSync(rows,index,normalized,owner)||hpChanged&&ItemSameMember(normalized.Before,normalized.After);
        }
        // The mandatory changed target snapshot proves the quarter-MaxHp cap.
        // Common immunity/cure groups and other party writes keep their existing
        // shared validator instead of being ignored as an arbitrary result tail.
        if(!CureMoveEndTail(action,2,used,battle.Slots,Sync)||!synchronized.Contains(used.Target))
            return Reject("unmatched-source-present-tail");
        // The actual register remains negative even when applied recovery is
        // capped. Carry only its exact predicate; never fabricate MoveDamage.
        return new(new(battle.Id,used.Sequence,attack.Sequence,pp[0].Sequence,recovery.Sequence,
            definition.SourceMoveId,used.Actor,used.Target,false){IsHealingMove=true},"committed-source-animation");
    }
}
