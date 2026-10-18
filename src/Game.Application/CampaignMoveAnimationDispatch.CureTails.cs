using Game.Domain.Battle;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Application;

public static partial class CampaignMoveAnimationPresentation
{
    // These non-damaging self scripts finish with the common immunity phase,
    // complete held-item groups, then active-party synchronization. Unknown rows
    // cannot become a harmless tail merely because a valid PP command precedes them.
    private static bool CureMoveEndTail(SourceBattleEvent[] rows,int start,SourceMoveUsedEvent used,int slots)
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
                synchronized.Count(p=>p.Item1==sync.Side)>slots/2||!CureActiveSync(rows,i,sync,used))return false;
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

    private static bool CureActiveSync(SourceBattleEvent[] rows,int index,SourceCalledPartyChangedEvent sync,SourceMoveUsedEvent used)
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
            if(a is null||b is null||sync.Side!=used.Actor.Side||slot!=used.Slot||a.Move!=used.Move||
                a.CurrentPp!=used.PpBefore||b.CurrentPp!=used.PpAfter||a with{CurrentPp=b.CurrentPp}!=b)return false;
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
