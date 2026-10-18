using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Application;

public static partial class CampaignMoveAnimationPresentation
{
    private static CampaignMoveAnimationDecision SpiteAnimation(Invocation battle,SourceMoveUsedEvent used,
        SourceAttackStringEvent attack,PokemonMoveDefinition definition,SourceBattleEvent[] action,
        SourcePpCommandEvent[] commands,IReadOnlyDictionary<MoveId,PokemonMoveDefinition>? moves)
    {
        CampaignMoveAnimationDecision Reject(string reason)=>new(null,reason);
        if(definition.Power!=0||definition.Target!="MOVE_TARGET_SELECTED"||used.Actor==used.Target||
            used.Actor.Value<0||used.Actor.Value>=battle.Slots||used.Target.Value<0||used.Target.Value>=battle.Slots||
            !ValidSingleInvocationPp(commands,used,2,invocation:battle.Context)||action.Length<2||action[0]!=commands[0]||
            attack.Sequence+1!=used.Sequence||commands[0].Sequence!=used.Sequence+1||
            action.Any(e=>e.Turn!=used.Turn)||action.Zip(action.Skip(1)).Any(pair=>pair.Second.Sequence!=pair.First.Sequence+1))
            return Reject("unmatched-source-spite-invocation");
        int following=battle.EventIndex+1+action.Length;
        if(following<battle.Events.Length&&battle.Events[following] is not
            (SourceAttackStringEvent or SourceMoveUsedEvent or SourceOrderEvent or SourceTurnReadyEvent or SourcePpCommandEvent)&&
            battle.Events[following].Turn!=used.Turn)return Reject("unmatched-source-spite-turn-boundary");
        // tryspiteppreduce commits before attackanimation. Move/Slot identify
        // the victim's reduced learned move, not the attacking Spite/PP slot.
        if(action[1] is not SourceSpiteEvent result||result.Actor!=used.Actor||result.Target!=used.Target||
            result.Slot is <0 or >3||result.PpBefore is <2 or >255||result.RolledReduction is <2 or >5||
            result.PpAfter!=Math.Max(0,result.PpBefore-result.RolledReduction)||
            result.Move.Value is "pret:MOVE_NONE" or "pret:MOVE_UNAVAILABLE"||Definition(result.Move,moves) is null)
            return Reject("unmatched-source-spite-result");
        int tail=2;
        if(tail<action.Length&&action[tail] is SourceMtMoonVolatileEvent {Reason:"spite-empty-pp",Volatile:"fury-cutter"} fury)
        {
            if(result.PpAfter!=0||fury.Side!=used.Target||fury.Before is <1 or >5||fury.After!=0)
                return Reject("unmatched-source-spite-cancellation");
            tail++;
        }
        if(tail<action.Length&&action[tail] is SourceMtMoonVolatileEvent {Reason:"spite-empty-pp"} cancelled)
        {
            int maximum=cancelled.Volatile switch{"called-bide"=>2,"called-rampage"=>3,"called-uproar"=>5,
                "called-charge"=>1,"called-rollout"=>5,_=>0};
            if(result.PpAfter!=0||cancelled.Side!=used.Target||cancelled.Before<1||cancelled.Before>maximum||cancelled.After!=0)
                return Reject("unmatched-source-spite-cancellation");
            tail++;
        }
        int lastSync=-1;
        bool Sync(SourceBattleEvent[] rows,int index,SourceCalledPartyChangedEvent sync,SourceMoveUsedEvent owner)
        {
            if(sync.SourceSlot is not {} source||source.Value<=lastSync||source.Value>=battle.Slots||source.Side!=sync.Side||
                sync.TransientMoveSlots is not {} transient||transient>15||sync.Before?.Pokemon is null||sync.After?.Pokemon is null||
                sync.Before.Pokemon.Moves.IsDefault||sync.After.Pokemon.Moves.IsDefault||
                sync.Before.Pokemon.Moves.Length!=sync.After.Pokemon.Moves.Length)return false;
            lastSync=source.Value;
            var before=sync.Before;var after=sync.After;bool victimPp=false;
            for(int slot=0;slot<before.Pokemon.Moves.Length;slot++)
            {
                var a=before.Pokemon.Moves[slot];var b=after.Pokemon.Moves[slot];
                if(a is null||b is null)return false;
                if((transient&(1<<slot))!=0){if(a!=b)return false;continue;}
                if(source==used.Target&&slot==result.Slot)
                {
                    if(a.Move!=result.Move||a.CurrentPp!=result.PpBefore||b.CurrentPp!=result.PpAfter||a with{CurrentPp=b.CurrentPp}!=b)return false;
                    victimPp=true;
                    before=before with{Pokemon=before.Pokemon with{Moves=before.Pokemon.Moves.SetItem(slot,b)}};
                }
                else if(source==used.Actor)
                {
                    if(a with{CurrentPp=b.CurrentPp}!=b||
                        !InvocationPpDelta(action,owner,battle.Context,slot,a.Move,a.CurrentPp,b.CurrentPp,allowUnchanged:true))return false;
                }
                else if(a!=b)return false;
            }
            // Keep every status/item tail tied to the exact source slot. The
            // shared tail parser still owns immunity and complete cure groups.
            if(before.Status!=after.Status&&!rows.Take(index).OfType<SourceStatusChangedEvent>().Any(e=>e.Side==source&&
                SourcePartyStatusRules.FromBattle(e.Before)==before.Status&&SourcePartyStatusRules.FromBattle(e.After)==after.Status))return false;
            if((before.HeldItem!=after.HeldItem||before.PersistentHeldItemOverride!=after.PersistentHeldItemOverride)&&
                !rows.Take(index).OfType<SourceHeldItemConsumedEvent>().Any(e=>e.Side==source&&e.Item==before.HeldItem))return false;
            var normalized=sync with{Before=before};
            return CureActiveSync(rows,index,normalized,owner,battle.Context)||victimPp&&ItemSameMember(before,after);
        }
        // Transform/Mimic victims can have no persistent PP delta, hence no
        // victim sync row. When exported, that delta must match the command;
        // never inspect the final body or invent a victim PP command.
        if(!CureMoveEndTail(action,tail,used,battle.Slots,Sync))return Reject("unmatched-source-spite-tail");
        return new(new(battle.Id,used.Sequence,attack.Sequence,commands[0].Sequence,result.Sequence,
            definition.SourceMoveId,used.Actor,used.Target,false),"committed-source-animation");
    }
}
