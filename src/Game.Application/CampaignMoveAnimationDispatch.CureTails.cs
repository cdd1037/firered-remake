using Game.Domain.Battle;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Application;

public static partial class CampaignMoveAnimationPresentation
{
    // These non-damaging self scripts finish with the common immunity phase,
    // complete held-item groups, then active-party synchronization. Unknown rows
    // cannot become a harmless tail merely because a valid PP command precedes them.
    private static bool CureMoveEndTail(SourceBattleEvent[] rows,int start,SourceMoveUsedEvent used,int slots,
        Func<SourceBattleEvent[],int,SourceCalledPartyChangedEvent,SourceMoveUsedEvent,bool>? activeSync=null,CampaignAnimationInvocation? invocation=null)
    {
        int i=start,lastImmunity=-1,lastItem=-1;
        while(i<rows.Length&&rows[i] is SourceAbilityActivatedEvent ability)
        {
            if(ability.Side.Value<=lastImmunity||ability.Side.Value>=slots)return false;
            lastImmunity=ability.Side.Value;i++;
            if(i<rows.Length&&rows[i] is SourceTowerVolatileEvent nightmare)
            {
                if(ability.Ability is not ("ABILITY_INSOMNIA" or "ABILITY_VITAL_SPIRIT")||
                    nightmare.Side!=ability.Side||nightmare.Effect!="nightmare"||nightmare.Applied||
                    i+1>=rows.Length||rows[i+1] is not SourceStatusChangedEvent status||status.Side!=ability.Side||
                    !WildBattleStatus.IsSleeping(status.Before)||status.Reason!=nightmare.Reason)return false;
                i++;
            }
            if(i>=rows.Length||!ImmunityResult(ability,rows[i],slots))return false;
            i++;
        }
        while(i<rows.Length&&rows[i] is not SourceCalledPartyChangedEvent)
        {
            int end=i;
            while(end<rows.Length&&rows[end] is SourceConfusionChangedEvent or SourceStatusChangedEvent or
                SourceInfatuationChangedEvent or SourceStatChangedEvent or SourceFocusEnergyEvent)end++;
            if(end>=rows.Length||rows[end] is not SourceHeldItemConsumedEvent item||
                item.Side.Value<=lastItem||item.Side.Value>=slots||
                CampaignHeldItemAnimationPresentation.ConsumedGroupStart(rows,end)!=i)return false;
            // The common move-end phase only consumes cures/White Herb; healing,
            // PP restoration and pinch boosts belong to another source phase.
            if(item.HpRestored!=0||item.PpRecovery is not null||item.FocusEnergyRaised==true||item.Flavor is not null||
                item.StatChanges is {} changes&&changes.Any(change=>change.Before>=6||change.After!=6))return false;
            lastItem=item.Side.Value;i=end+1;
        }
        var synchronized=new HashSet<(BattleSide,int)>();
        for(;i<rows.Length;i++)
        {
            if(rows[i] is not SourceCalledPartyChangedEvent {Reason:"active-sync"} sync||
                !Enum.IsDefined(sync.Side)||sync.PartyIndex is <0 or >=6||!synchronized.Add((sync.Side,sync.PartyIndex))||
                synchronized.Count(p=>p.Item1==sync.Side)>slots/2||!(activeSync is null?CureActiveSync(rows,i,sync,used,invocation):activeSync(rows,i,sync,used)))return false;
        }
        return true;
    }

    private static bool ImmunityResult(SourceAbilityActivatedEvent ability,SourceBattleEvent row,int slots)=>row switch
    {
        SourceStatusChangedEvent status when status.Side==ability.Side&&status.After==0=>ability.Ability switch
        {
            "ABILITY_IMMUNITY"=>status.Reason=="immunity"&&(status.Before==WildBattleStatus.Poison||WildBattleSourceFieldEffects.IsValidToxic(status.Before)),
            "ABILITY_LIMBER"=>status.Reason=="limber"&&status.Before==WildBattleStatus.Paralysis,
            "ABILITY_INSOMNIA" or "ABILITY_VITAL_SPIRIT"=>status.Reason=="transform-immunity"&&WildBattleStatus.IsSleeping(status.Before),
            "ABILITY_WATER_VEIL"=>status.Reason=="transform-immunity"&&status.Before==WildBattleStatus.Burn,
            "ABILITY_MAGMA_ARMOR"=>status.Reason=="transform-immunity"&&status.Before==WildBattleStatus.Freeze,
            _=>false
        },
        SourceConfusionChangedEvent confusion=>ability.Ability=="ABILITY_OWN_TEMPO"&&confusion.Side==ability.Side&&
            confusion.Reason=="own-tempo"&&confusion.Before is >=1 and <=5&&confusion.After==0,
        SourceInfatuationChangedEvent infatuation=>ability.Ability=="ABILITY_OBLIVIOUS"&&infatuation.Side==ability.Side&&
            infatuation.Reason=="oblivious"&&infatuation.Before is {} source&&source!=ability.Side&&source.Value>=0&&source.Value<slots&&infatuation.After is null,
        _=>false
    };

    // Caller PP is part of this same action, even when the leaf suppresses its
    // own deduction or uses another learned slot. Keep each original command.
    private static IEnumerable<CampaignAnimationCallerPp> InvocationPpLedger(SourceBattleEvent[] rows,
        SourceMoveUsedEvent used,CampaignAnimationInvocation? invocation)
    {
        if(invocation is not null)
            foreach(var payment in invocation.CallerPpLedger)yield return payment;
        foreach(var command in rows.OfType<SourcePpCommandEvent>().Where(p=>p.Actor==used.Actor&&p.CurrentMove==used.Move))
            yield return new(command,invocation is not null&&command.PpSlot==invocation.PpSlot
                ?invocation.LearnedPpMove:used.Move,invocation?.NoPpDeduct==true);
    }

    private static bool InvocationPpDelta(SourceBattleEvent[] rows,SourceMoveUsedEvent used,
        CampaignAnimationInvocation? invocation,int slot,Game.Domain.Ids.MoveId learned,int before,int after,long afterSequence=long.MinValue,bool allowUnchanged=false)
    {
        var all=InvocationPpLedger(rows,used,invocation).Where(p=>p.Command.PpOwner==used.Actor&&p.Command.PpSlot==slot).ToArray();
        if(all.Any(p=>p.LearnedMove!=learned))return false;
        var applied=all.Where(p=>p.Command.Sequence<=afterSequence).ToArray();
        if(applied.Length>0&&applied[^1].Command.After!=before)return false;
        var payments=all.Where(p=>p.Command.Sequence>afterSequence).ToArray();
        if(payments.Length==0)return allowUnchanged&&before==after;
        return
            payments[0].Command.Before==before&&payments[^1].Command.After==after&&
            payments.Zip(payments.Skip(1)).All(p=>p.First.Command.After==p.Second.Command.Before);
    }

    private static bool CureActiveSync(SourceBattleEvent[] rows,int index,SourceCalledPartyChangedEvent sync,SourceMoveUsedEvent used,CampaignAnimationInvocation? invocation=null)
    {
        var before=sync.Before;var after=sync.After;
        if(before?.Pokemon is null||after?.Pokemon is null||before.Pokemon.Moves.IsDefault||after.Pokemon.Moves.IsDefault||
            before.Pokemon.Moves.Length!=after.Pokemon.Moves.Length||before.Pokemon with{Moves=[]}!=after.Pokemon with{Moves=[]}||
            before with{Pokemon=after.Pokemon,Status=after.Status,HeldItem=after.HeldItem,PersistentHeldItemOverride=after.PersistentHeldItemOverride}!=after)
            return false;
        bool changed=false;
        for(int slot=0;slot<before.Pokemon.Moves.Length;slot++)
        {
            var a=before.Pokemon.Moves[slot];var b=after.Pokemon.Moves[slot];if(a==b)continue;
            if(a is null||b is null||sync.Side!=used.Actor.Side||sync.SourceSlot is {} source&&source!=used.Actor||
                !InvocationPpDelta(rows,used,invocation,slot,a.Move,a.CurrentPp,b.CurrentPp)||
                a with{CurrentPp=b.CurrentPp}!=b)return false;
            changed=true;
        }
        if(before.Status!=after.Status)
        {
            if(!rows.Take(index).OfType<SourceStatusChangedEvent>().Any(e=>e.Side.Side==sync.Side&&
                SourcePartyStatusRules.FromBattle(e.Before)==before.Status&&SourcePartyStatusRules.FromBattle(e.After)==after.Status))return false;
            changed=true;
        }
        if(before.HeldItem!=after.HeldItem||before.PersistentHeldItemOverride!=after.PersistentHeldItemOverride)
        {
            if(after.HeldItem!="ITEM_NONE"||after.PersistentHeldItemOverride is not (null or "ITEM_NONE")||
                !rows.Take(index).OfType<SourceHeldItemConsumedEvent>().Any(e=>e.Side.Side==sync.Side&&e.Item==before.HeldItem))return false;
            changed=true;
        }
        return changed;
    }
}
