using Game.Domain.Battle;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Application;

public static partial class CampaignMoveAnimationPresentation
{
    private sealed record ItemAnimationOwner(BattlerSlot Slot,string Before,string After);

    private static CampaignMoveAnimationDecision ItemTransferAnimation(Invocation battle,SourceMoveUsedEvent used,
        SourceAttackStringEvent attack,PokemonMoveDefinition definition,SourceBattleEvent[] action,SourcePpCommandEvent[] commands)
    {
        CampaignMoveAnimationDecision Reject(string reason)=>new(null,reason);
        bool recycle=definition.Effect=="EFFECT_RECYCLE";
        if(definition.Power!=0||definition.Target!=(recycle?"MOVE_TARGET_USER":"MOVE_TARGET_SELECTED")||
            used.Actor.Value<0||used.Actor.Value>=battle.Slots||used.Target.Value<0||used.Target.Value>=battle.Slots||
            (recycle?used.Target!=used.Actor:used.Target==used.Actor||used.Actor.Side!=BattleSide.Player)||
            !ValidSingleInvocationPp(commands,used,recycle?1:2,invocation:battle.Context)||action.Length<2||action[0]!=commands[0]||
            commands[0].Sequence!=used.Sequence+1||attack.Sequence+1!=used.Sequence||
            action.Any(e=>e.Turn!=used.Turn)||action.Zip(action.Skip(1)).Any(pair=>pair.Second.Sequence!=pair.First.Sequence+1))
            return Reject("unmatched-source-item-invocation");
        int following=battle.EventIndex+1+action.Length;
        if(following<battle.Events.Length&&battle.Events[following] is not
            (SourceAttackStringEvent or SourceMoveUsedEvent or SourceOrderEvent or SourceTurnReadyEvent or SourcePpCommandEvent)&&
            battle.Events[following].Turn!=used.Turn)return Reject("unmatched-source-item-turn-boundary");

        var successes=action.Where(e=>e is SourceHeldItemRecycledEvent or SourceHeldItemsSwappedEvent).ToArray();
        if(successes.Length!=1)return Reject("unmatched-source-item-command-result");
        var result=successes[0];ItemAnimationOwner[] owners;
        if(recycle)
        {
            if(result is not SourceHeldItemRecycledEvent restored||restored.Actor!=used.Actor||restored.Move!=used.Move||
                !ItemAnimationSymbol(restored.Item)||restored.Item=="ITEM_NONE")return Reject("unmatched-source-recycle-result");
            owners=[new(used.Actor,"ITEM_NONE",restored.Item)];
        }
        else
        {
            if(result is not SourceHeldItemsSwappedEvent swapped||swapped.Actor!=used.Actor||swapped.Target!=used.Target||
                swapped.Move!=used.Move||!ItemAnimationSymbol(swapped.ActorItemBefore)||!ItemAnimationSymbol(swapped.TargetItemBefore)||
                swapped.ActorItemBefore=="ITEM_NONE"&&swapped.TargetItemBefore=="ITEM_NONE")return Reject("unmatched-source-trick-result");
            owners=[new(used.Actor,swapped.ActorItemBefore,swapped.TargetItemBefore),new(used.Target,swapped.TargetItemBefore,swapped.ActorItemBefore)];
        }

        int marker=Array.IndexOf(action,result),role=0;
        var identities=new Dictionary<int,PartyMemberKey>();
        bool Bind(ItemAnimationOwner owner,SourceCalledPartyChangedEvent row)
        {
            var key=new PartyMemberKey(row.Side,row.PartyIndex);
            if(identities.TryGetValue(owner.Slot.Value,out var previous))return previous==key;
            if(identities.Values.Contains(key))return false;
            identities.Add(owner.Slot.Value,key);return true;
        }
        // Controller writes may already be reflected in party storage. When a
        // write is exported, it must be the exact ordered command-owned change.
        for(int i=1;i<marker;i++)
        {
            if(action[i] is not SourceCalledPartyChangedEvent write||write.Reason!=(recycle?"recycle-party-write":"trick-party-write"))
                return Reject("unmatched-source-item-party-prefix");
            while(role<owners.Length&&!ItemCommandWrite(write,owners[role],recycle||role>0?owners[role].After:"ITEM_NONE"))role++;
            if(role>=owners.Length||!Bind(owners[role++],write))return Reject("unmatched-source-item-party-prefix");
        }

        int firstItem=action.Length;
        for(int i=marker+1;i<action.Length;i++)if(action[i] is SourceHeldItemConsumedEvent)
        {
            int start=CampaignHeldItemAnimationPresentation.ConsumedGroupStart(action,i);
            if(start<=marker)return Reject("unmatched-source-item-consumption");
            firstItem=Math.Min(firstItem,start);
        }
        var filtered=new List<SourceBattleEvent>();int changed=0;bool itemPhase=false;
        for(int i=0;i<action.Length;i++)
        {
            var row=action[i];
            if(i>marker&&row is SourceHeldItemConsumedEvent item)
            {
                itemPhase=true;
                var owner=owners.SingleOrDefault(o=>o.Slot==item.Side);
                if(owner is not null&&item.Item!=owner.After)return Reject("unmatched-source-item-consumption");
            }
            if(i>marker&&row is SourceCalledPartyChangedEvent {Reason:"active-sync"})itemPhase=true;
            if(row is SourceCalledPartyChangedEvent {Reason:"changed-item-applied"} applied)
            {
                if(recycle||i<=marker||i>=firstItem||itemPhase||++changed!=1||owners[0].After=="ITEM_NONE"||
                    !ItemCommandWrite(applied,owners[0],owners[0].After)||
                    !CureMoveEndTail(action.Take(i).ToArray(),marker+1,used,battle.Slots,invocation:battle.Context)||
                    action.Skip(i+1).Any(e=>e is SourceAbilityActivatedEvent)||
                    applied.Before.PersistentHeldItemOverride!=owners[0].After||applied.After.PersistentHeldItemOverride is not null||
                    !Bind(owners[0],applied))return Reject("unmatched-source-item-application");
                continue;
            }
            filtered.Add(row);
        }
        if(changed!=(recycle||owners[0].After=="ITEM_NONE"?0:1))return Reject("unmatched-source-item-application");
        var synchronizedSlots=new HashSet<BattlerSlot>();
        bool Sync(SourceBattleEvent[] rows,int i,SourceCalledPartyChangedEvent sync,SourceMoveUsedEvent invocation)
        {
            var before=sync.Before;var after=sync.After;
            if(before is null||after is null||sync.SourceSlot is not {} source||source.Value<0||source.Value>=battle.Slots||
                source.Side!=sync.Side||!synchronizedSlots.Add(source)||sync.TransientMoveSlots is not {} mask||mask>15||before.Pokemon is null||after.Pokemon is null||
                before.Pokemon.Moves.IsDefault||after.Pokemon.Moves.IsDefault||
                !before.Pokemon.Moves.SequenceEqual(after.Pokemon.Moves)&&source!=invocation.Actor)return false;
            if(source==invocation.Actor)
            {
                var preceding=action.OfType<SourceCalledPartyChangedEvent>().LastOrDefault(e=>
                    e.Sequence<sync.Sequence&&e.SourceSlot==source&&e.Side==sync.Side&&e.PartyIndex==sync.PartyIndex&&
                    e.Reason is "trick-party-write" or "recycle-party-write" or "changed-item-applied");
                if(preceding is not null&&!ItemSameMember(before,preceding.After))return false;
                if(before.Pokemon.Moves.Length!=after.Pokemon.Moves.Length)return false;
                for(int slot=0;slot<before.Pokemon.Moves.Length;slot++)
                {
                    var a=before.Pokemon.Moves[slot];var b=after.Pokemon.Moves[slot];
                    if(a is null||b is null)return false;
                    if((mask&(1<<slot))!=0){if(a!=b)return false;continue;}
                    if(a with{CurrentPp=b.CurrentPp}!=b||
                        !InvocationPpDelta(action,invocation,battle.Context,slot,a.Move,a.CurrentPp,b.CurrentPp,
                            preceding?.Sequence??long.MinValue,allowUnchanged:true))return false;
                }
            }
            var commandOwner=owners.SingleOrDefault(o=>o.Slot==source);
            if(commandOwner is not null&&!Bind(commandOwner,sync))return false;
            if(before.Status!=after.Status&&!rows.Take(i).OfType<SourceStatusChangedEvent>().Any(e=>e.Side==source&&
                SourcePartyStatusRules.FromBattle(e.Before)==before.Status&&SourcePartyStatusRules.FromBattle(e.After)==after.Status))return false;
            bool heldChanged=before.HeldItem!=after.HeldItem||before.PersistentHeldItemOverride!=after.PersistentHeldItemOverride;
            bool ownsConsumption=rows.Take(i).OfType<SourceHeldItemConsumedEvent>().Any(e=>e.Side==source&&e.Item==before.HeldItem);
            // Ordinary status/PP synchronization and actual same-slot consumption
            // keep their existing parser, including unrelated active battlers.
            if((!heldChanged||ownsConsumption)&&CureActiveSync(rows,i,sync,invocation,battle.Context))return true;
            if(!heldChanged)return false;
            foreach(var owner in owners)
            {
                if(source!=owner.Slot||before.HeldItem!=owner.Before)continue;
                bool consumed=rows.Take(i).OfType<SourceHeldItemConsumedEvent>().Any(e=>e.Side==owner.Slot&&e.Item==owner.After);
                string expected=consumed?"ITEM_NONE":owner.After;
                if(after.HeldItem!=expected||WildBattleSourcePartyEffects.PersistentHeldItem(after)!=expected||
                    before.KnockedOffItem!=after.KnockedOffItem||!Bind(owner,sync))continue;
                // Existing cure/PP authority validates every other field. Only
                // this command's authenticated held-item delta is removed.
                var normalized=sync with{Before=before with{HeldItem=after.HeldItem,PersistentHeldItemOverride=after.PersistentHeldItemOverride}};
                return ItemSameMember(normalized.Before,after)||CureActiveSync(rows,i,normalized,invocation,battle.Context);
            }
            return false;
        }
        // Changed-item installation is item-specific; the common immunity,
        // complete cure groups and active synchronization still share one parser.
        if(!CureMoveEndTail(filtered.ToArray(),marker+1,used,battle.Slots,Sync))return Reject("unmatched-source-item-tail");
        return new(new(battle.Id,used.Sequence,attack.Sequence,commands[0].Sequence,result.Sequence,
            definition.SourceMoveId,used.Actor,used.Target,false),"committed-source-animation");
    }

    private static bool ItemAnimationSymbol(string? item)=>item is not null&&item.StartsWith("ITEM_",StringComparison.Ordinal)&&
        item.Length>5&&item[5..].All(c=>c is >= 'A' and <= 'Z' or >= '0' and <= '9' or '_')&&
        item!="ITEM_ENIGMA_BERRY"&&!item.EndsWith("_MAIL",StringComparison.Ordinal);
    private static bool ItemSameMember(WildBattleCalledPartyMember before,WildBattleCalledPartyMember after)=>
        before.Pokemon is not null&&after.Pokemon is not null&&!before.Pokemon.Moves.IsDefault&&!after.Pokemon.Moves.IsDefault&&
        before with{Pokemon=after.Pokemon}==after&&before.Pokemon with{Moves=[]}==after.Pokemon with{Moves=[]}&&
        before.Pokemon.Moves.SequenceEqual(after.Pokemon.Moves);
    private static bool ItemCommandWrite(SourceCalledPartyChangedEvent row,ItemAnimationOwner owner,string held)=>
        row.SourceSlot==owner.Slot&&row.Side==owner.Slot.Side&&row.PartyIndex is >=0 and <6&&row.Before is {} before&&row.After is {} after&&
        before.HeldItem==held&&after.HeldItem==held&&before.PersistentHeldItemOverride!=after.PersistentHeldItemOverride&&
        WildBattleSourcePartyEffects.PersistentHeldItem(after)==owner.After&&
        ItemSameMember(before with{PersistentHeldItemOverride=after.PersistentHeldItemOverride},after);
}
