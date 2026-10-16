using System.Collections.Immutable;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.WildBattle;

namespace Game.Domain.Items;

public static partial class SourceBattleItemEventRules
{
    internal static SourceFourActionResult Publish(SourceBattleFrame before, SourceBattleFrame after, OccupantToken actor,
        PartyMemberKey target, ItemId item, long instanceId)
    {
        var next = after; var events = ImmutableArray.CreateBuilder<SourceBattleEvent>();
        void Add(Func<long,int,SourceBattleEvent> create) { var p=SourceBattleEventHooks.Publish(next,create); next=p.State;events.Add(p.Event); }
        var old = before.Party(target.Side).Members[target.Index]; var current = after.Party(target.Side).Members[target.Index];
        var a=before.Get(actor.Slot).Body!; var b=after.Get(actor.Slot).Body!;
        var active=after.Slots.FirstOrDefault(c=>c.Member==target);
        var pp = old.Pokemon.Moves.Select((m,i)=>new SourceItemPpRecovery(m.Move,i,m.CurrentPp,current.Pokemon.Moves[i].CurrentPp)).Where(p=>p.After>p.Before).ToImmutableArray();
        Add((n,t)=>new SourceMedicineAppliedEvent(n,t,item,instanceId,target.Index,active is not null,
            old.Pokemon.CurrentHp,current.Pokemon.CurrentHp,old.Status,current.Status) {
            Actor=actor.Slot,TargetSide=target.Side,PpRecovery=pp.IsEmpty?null:pp,
            FriendshipBefore=old.Pokemon.Friendship==current.Pokemon.Friendship?null:old.Pokemon.Friendship,
            FriendshipAfter=old.Pokemon.Friendship==current.Pokemon.Friendship?null:current.Pokemon.Friendship });
        foreach(var stat in new[]{WildBattleStat.Attack,WildBattleStat.Defense,WildBattleStat.Speed,WildBattleStat.Accuracy,WildBattleStat.SpecialAttack})
        {
            int Value(WildBattleCombatant body)=>stat switch { WildBattleStat.Attack=>body.AttackStage,WildBattleStat.Defense=>body.DefenseStage,
                WildBattleStat.Speed=>body.SpeedStage,WildBattleStat.Accuracy=>body.AccuracyStage,_=>body.SpecialAttackStageDelta+6 };
            if(Value(a)!=Value(b)) Add((n,t)=>new SourceStatChangedEvent(n,t,actor.Slot,actor.Slot,stat,Value(a),Value(b)));
        }
        if(!a.FocusEnergy&&b.FocusEnergy) Add((n,t)=>new SourceFocusEnergyEvent(n,t,actor.Slot));
        int oldMist=before.Sides[(int)actor.Slot.Side].MistTurnsRemaining,newMist=after.Sides[(int)actor.Slot.Side].MistTurnsRemaining;
        if(oldMist!=newMist) Add((n,t)=>new SourceMtMoonVolatileEvent(n,t,actor.Slot,"mist",oldMist,newMist,"item"));
        if(active is not null)
        {
            var previous=before.Get(active.Slot);
            if(previous.Body!.ConfusionTurnsRemaining!=active.Body!.ConfusionTurnsRemaining)
                Add((n,t)=>new SourceConfusionChangedEvent(n,t,active.Slot,previous.Body.ConfusionTurnsRemaining,active.Body.ConfusionTurnsRemaining,"item"));
            if(previous.Registers.InfatuationSource!=active.Registers.InfatuationSource)
                Add((n,t)=>new SourceInfatuationChangedEvent(n,t,active.Slot,previous.Registers.InfatuationSource,active.Registers.InfatuationSource,"item"));
        }
        return new(next,events.ToImmutable(),[],null);
    }
}
