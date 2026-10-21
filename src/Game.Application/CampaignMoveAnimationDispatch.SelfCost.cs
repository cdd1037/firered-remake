using Game.Domain.Battle;
using Game.Domain.WildBattle;
namespace Game.Application;

public static partial class CampaignMoveAnimationPresentation
{
    private sealed record CommittedSelfCost(BattlerSlot Actor,int Before,int After,int Damage);
    private static bool SelfCostTail(SourceBattleEvent[] action,int tail,SourceMoveUsedEvent used,int slots,
        SourcePpCommandEvent[] commands,CommittedSelfCost cost,Func<int,bool> validateCost,CampaignAnimationInvocation? invocation)
    {
        if(cost.Actor!=used.Actor||cost.Before<1||cost.Damage<1||cost.After!=Math.Max(0,cost.Before-cost.Damage))return false;
        SourceFaintedEvent? faintReceipt=null;
        if(cost.After==0)
        {
            if(tail<action.Length&&action[tail] is SourceStatusChangedEvent {Reason:"faint",After:0} clear)
            {if(clear.Side!=used.Actor||!CureStatus(clear.Before))return false;tail++;}
            if(tail>=action.Length||action[tail] is not SourceFaintedEvent faint||faint.Side!=used.Actor||faint.Turn!=used.Turn||
                faint.Sequence<=used.Sequence||used.Actor.Side!=BattleSide.Player&&faint.Friendship is not null)return false;
            faintReceipt=faint;
            tail++;
        }
        int end=action.Length;
        if(end>tail&&action[end-1] is SourceEndedEvent ended)
        {if(cost.After!=0||ended.Outcome!=(used.Actor.Side==BattleSide.Player?WildBattleOutcome.Lost:WildBattleOutcome.Won))return false;end--;}
        else if(end>tail&&action[end-1] is SourceCalledPartySwitchRequiredEvent required)
        {
            var pending=required.Pending;
            if(cost.After!=0||pending.Slot!=used.Actor||pending.Kind!=WildBattleCalledSwitchKind.FaintedReplacement||pending.Invocation is not null||
                pending.LegalPartyIndices.IsDefaultOrEmpty||pending.LegalPartyIndices.Any(i=>i is <0 or >=6)||
                pending.LegalPartyIndices.Distinct().Count()!=pending.LegalPartyIndices.Length)return false;
            end--;
        }
        int costSyncs=0;
        bool Sync(SourceBattleEvent[] rows,int index,SourceCalledPartyChangedEvent sync,SourceMoveUsedEvent owner)
        {
            if(!CommittedCostPartySync(rows,index,sync,owner,commands,cost,validateCost,invocation,faintReceipt))return false;
            if(sync.Before.Pokemon.CurrentHp!=sync.After.Pokemon.CurrentHp)costSyncs++;
            return true;
        }
        return CureMoveEndTail(action[..end],tail,used,slots,Sync)&&costSyncs==1;
    }
    private static bool CommittedCostPartySync(SourceBattleEvent[] rows,int index,SourceCalledPartyChangedEvent sync,
        SourceMoveUsedEvent used,SourcePpCommandEvent[] commands,CommittedSelfCost? cost,Func<int,bool>? validateCost,CampaignAnimationInvocation? invocation,
        SourceFaintedEvent? faint=null)
    {
        var before=sync.Before;var after=sync.After;
        if(sync.SourceSlot is not {} source||source.Value is <0 or >3||source.Side!=sync.Side||
            sync.TransientMoveSlots is not {} transient||transient>15)return false;
        if(before?.Pokemon is null||after?.Pokemon is null||before.Pokemon.Moves.IsDefault||after.Pokemon.Moves.IsDefault||
            before.Pokemon.Moves.Length!=after.Pokemon.Moves.Length||before.Pokemon.Moves.Any(m=>m is null)||after.Pokemon.Moves.Any(m=>m is null))return false;
        int friendship=before.Pokemon.Friendship;
        if(source==used.Actor&&cost?.After==0&&source.Side==BattleSide.Player&&
            (friendship!=after.Pokemon.Friendship||faint?.Friendship is not null))
        {
            // The faint command owns this permanent delta. Match its captured
            // member and values; presentation never reruns friendship rules.
            if(faint?.Friendship is not {} applied||faint.Side!=source||faint.Turn!=used.Turn||sync.Turn!=faint.Turn||
                faint.Sequence>=sync.Sequence||applied.Member!=new PartyMemberKey(sync.Side,sync.PartyIndex)||
                applied.Before is <0 or >255||applied.After<0||applied.After>applied.Before||
                applied.Before!=friendship||applied.After!=after.Pokemon.Friendship)return false;
            friendship=applied.After;
        }
        else if(friendship!=after.Pokemon.Friendship)return false;
        bool explained=false;
        if(before.Pokemon.CurrentHp!=after.Pokemon.CurrentHp)
        {
            if(cost is null||source!=used.Actor||before.Pokemon.CurrentHp!=cost.Before||after.Pokemon.CurrentHp!=cost.After||
                cost.Before>before.Pokemon.Stats.MaxHp||validateCost is null||!validateCost(before.Pokemon.Stats.MaxHp))return false;
            explained=true;
        }
        if(source==used.Actor)
        {
            if(commands.Any(p=>(transient&(1<<p.PpSlot))==0&&p.PpSlot>=before.Pokemon.Moves.Length))return false;
            // With an entirely persistent moveset the party snapshot proves the
            // source's first matching move position, including nonzero selection.
            if(transient==0&&commands.Length==2&&
                commands[1].PpSlot!=Array.FindIndex(before.Pokemon.Moves.ToArray(),m=>m.Move==used.Move))return false;
        }
        for(int slot=0;slot<before.Pokemon.Moves.Length;slot++)
        {
            var a=before.Pokemon.Moves[slot];var b=after.Pokemon.Moves[slot];
            if((transient&(1<<slot))!=0){if(a!=b)return false;continue;}
            if(source!=used.Actor){if(a!=b)return false;continue;}
            if(a with{CurrentPp=b.CurrentPp}!=b||
                !InvocationPpDelta(rows,used,invocation,slot,a.Move,a.CurrentPp,b.CurrentPp,allowUnchanged:true))return false;
            explained|=a!=b;
        }
        var normalized=sync with{Before=before with{Pokemon=before.Pokemon with{CurrentHp=after.Pokemon.CurrentHp,Moves=after.Pokemon.Moves,Friendship=friendship}}};
        if(CureActiveSync(rows,index,normalized,used,invocation))return true;
        // Transform may preserve party move slots unchanged. A pure committed
        // HP cost is still a real synchronization, not an invented PP change.
        return explained&&normalized.Before with{Pokemon=normalized.After.Pokemon}==normalized.After&&
            normalized.Before.Pokemon with{Moves=[]}==normalized.After.Pokemon with{Moves=[]}&&
            normalized.Before.Pokemon.Moves.SequenceEqual(normalized.After.Pokemon.Moves);
    }
}
