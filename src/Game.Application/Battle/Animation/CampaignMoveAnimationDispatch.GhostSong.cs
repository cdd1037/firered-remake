using Game.Domain.Battle;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Application;

public static partial class CampaignMoveAnimationPresentation
{
    private static CampaignMoveAnimationDecision GhostSongAnimation(Invocation battle,SourceMoveUsedEvent used,
        SourceAttackStringEvent attack,PokemonMoveDefinition definition,SourceBattleEvent[] action,SourcePpCommandEvent[] commands)
    {
        CampaignMoveAnimationDecision Reject(string reason)=>new(null,reason);
        bool curse=definition.Effect=="EFFECT_CURSE",song=definition.Effect=="EFFECT_PERISH_SONG";
        if(definition.Power!=0||definition.Target!=(curse?"MOVE_TARGET_SELECTED":"MOVE_TARGET_USER")||
            used.Actor.Value<0||used.Actor.Value>=battle.Slots||used.Target.Value<0||used.Target.Value>=battle.Slots||
            !curse&&used.Target!=used.Actor||commands.Length<1||commands.Length>(song?2:1)||
            !ValidSingleInvocationPp([commands[0]],used,curse&&used.Target!=used.Actor?2:1,invocation:battle.Context)||
            attack.Sequence+1!=used.Sequence||action.Length<2||action[0]!=commands[0]||commands[0].Sequence!=used.Sequence+1||
            action.Any(e=>e.Turn!=used.Turn)||action.Zip(action.Skip(1)).Any(p=>p.Second.Sequence!=p.First.Sequence+1))
            return Reject("unmatched-source-ghost-song-invocation");
        int following=battle.EventIndex+1+action.Length;
        if(following<battle.Events.Length&&battle.Events[following] is not
            (SourceAttackStringEvent or SourceMoveUsedEvent or SourceOrderEvent or SourceTurnReadyEvent or SourcePpCommandEvent)&&
            battle.Events[following].Turn!=used.Turn)return Reject("unmatched-source-ghost-song-turn-boundary");
        SourceBattleEvent result;int tail;byte? turn=null;
        if(definition.Effect=="EFFECT_DESTINY_BOND")
        {
            // CANCELLER_FLAGS clears an earlier use, even on an unsuccessful
            // action attempt. Only this fresh setdestinybond write owns the root.
            if(action[1] is not SourceTowerVolatileEvent {Effect:"destiny-bond",Applied:true,Reason:"move"} bond||
                bond.Side!=used.Actor||!CureMoveEndTail(action,2,used,battle.Slots,invocation:battle.Context))return Reject("unmatched-source-destiny-bond-result");
            result=bond;
        }
        else if(song)
        {
            var affected=new HashSet<BattlerSlot>();int last=-1;tail=1;
            while(tail<action.Length&&action[tail] is SourceMtMoonVolatileEvent {Volatile:"perish-song",Before:-1,After:3,Reason:"move"} marker)
            {
                if(marker.Side.Value<=last||marker.Side.Value>=battle.Slots||!affected.Add(marker.Side))return Reject("unmatched-source-perish-song-order");
                last=marker.Side.Value;tail++;
            }
            if(affected.Count==0)return Reject("source-no-animation-result");
            result=action[tail-1];
            if(commands.Length==2)
            {
                var extra=commands[1];var first=commands[0];
                // PressurePPLoseOnUsingPerishSong scans the first matching move
                // position, rather than necessarily using the selected position.
                if(tail>=action.Length||action[tail]!=extra||extra.Actor!=used.Actor||extra.Target!=used.Actor||extra.PpOwner!=used.Actor||
                    extra.ChosenMove!=(battle.Context?.ChosenMove??used.Move)||extra.CurrentMove!=used.Move||extra.PpSlot<0||extra.PpSlot>(battle.Context?.IsCalled==true?3:used.Slot)||
                    extra.Before is <0 or >255||extra.CommandCost<1||extra.CommandCost>=battle.Slots||
                    extra.After!=Math.Max(0,extra.Before-extra.CommandCost)||extra.RngCursor!=first.RngCursor||
                    extra.PpSlot==(battle.Context?.PpSlot??used.Slot)&&extra.Before!=first.After)return Reject("unmatched-source-perish-song-pressure");
                tail++;
            }
            last=-1;
            while(tail<action.Length&&action[tail] is SourceAbilityActivatedEvent {Ability:"ABILITY_SOUNDPROOF"} blocked)
            {
                if(blocked.Side.Value<=last||blocked.Side.Value>=battle.Slots||affected.Contains(blocked.Side))return Reject("unmatched-source-perish-song-soundproof");
                last=blocked.Side.Value;tail++;
            }
            if(!CureMoveEndTail(action,tail,used,battle.Slots,(rows,index,sync,owner)=>GhostSongPartySync(rows,index,sync,owner,commands,null,battle.Context)))
                return Reject("unmatched-source-perish-song-tail");
        }
        else if(curse&&used.Target==used.Actor)
        {
            // Curse's own animation precedes these three commands. There is no
            // General_StatsChange call, including partially capped stat groups.
            WildBattleStat[] stats=[WildBattleStat.Speed,WildBattleStat.Attack,WildBattleStat.Defense];
            if(action.Length<4)return Reject("unmatched-source-curse-stats");
            bool changed=false;
            for(int i=0;i<3;i++)
            {
                int delta=i==0?-1:1;
                if(action[i+1] is SourceStatChangedEvent s&&s.Actor==used.Actor&&s.Target==used.Actor&&s.Stat==stats[i]&&
                    s.Before is >=0 and <=12&&s.After==Math.Clamp(s.Before+delta,0,12)&&s.Before!=s.After)changed=true;
                else if(i==0&&action[i+1] is SourceStatAtMinimumEvent minimum&&minimum.Actor==used.Actor&&minimum.Target==used.Actor&&minimum.Stat==stats[i]){ }
                else if(i>0&&action[i+1] is SourceStatAtMaximumEvent maximum&&maximum.Actor==used.Actor&&maximum.Target==used.Actor&&maximum.Stat==stats[i]){ }
                else return Reject("unmatched-source-curse-stat-order");
            }
            if(!changed||!CureMoveEndTail(action,4,used,battle.Slots,invocation:battle.Context))return Reject("unmatched-source-curse-stat-tail");
            result=action[1];turn=1;
        }
        else if(curse)
        {
            if(action.Length<3||action[1] is not SourceTowerVolatileEvent {Effect:"curse",Applied:true,Reason:"move"} marked||marked.Side!=used.Target||
                action[2] is not SourceTowerHpEvent {Effect:"curse-cost"} cost||cost.Side!=used.Actor||cost.HpBefore<1||cost.Damage<1||
                cost.HpAfter!=Math.Max(0,cost.HpBefore-cost.Damage))return Reject("unmatched-source-ghost-curse-result");
            if(!SelfCostTail(action,3,used,battle.Slots,commands,
                new(cost.Side,cost.HpBefore,cost.HpAfter,cost.Damage),maximum=>cost.Damage==Math.Max(1,maximum/2),battle.Context))
                return Reject("unmatched-source-curse-cost-tail");
            result=marked;turn=0;
        }
        else return Reject("not-source-ghost-song-animation");
        return new(new(battle.Id,used.Sequence,attack.Sequence,commands[0].Sequence,result.Sequence,
            definition.SourceMoveId,used.Actor,used.Target,false){MoveTurn=turn},"committed-source-animation");
    }

    private static bool GhostSongPartySync(SourceBattleEvent[] rows,int index,SourceCalledPartyChangedEvent sync,
        SourceMoveUsedEvent used,SourcePpCommandEvent[] commands,SourceTowerHpEvent? cost,CampaignAnimationInvocation? invocation)=>
        CommittedCostPartySync(rows,index,sync,used,commands,cost is null?null:new(cost.Side,cost.HpBefore,cost.HpAfter,cost.Damage),
            cost is null?null:maximum=>cost.Damage==Math.Max(1,maximum/2),invocation);
}
