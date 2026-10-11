using System.Collections.Immutable;
using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

/// <summary>One frame storage adapter for move-end immunity, including ActionSwitch and the
/// resumed Baton script. Calling an entry callback alone never invokes this phase.</summary>
internal static class SourceFrameImmunityRules
{
    internal static SourceFourActionResult Apply(SourceBattleFrame frame)
    {
        var next = frame; var events = ImmutableArray.CreateBuilder<SourceBattleEvent>();
        void Add(Func<long,int,SourceBattleEvent> create)
        {
            var result = SourceBattleEventHooks.Publish(next,create); next=result.State; events.Add(result.Event);
        }
        void SetBody(BattlerSlot slot,WildBattleCombatant body)
        {
            var cell=next.Get(slot);var key=cell.Member!.Value;var party=next.Party(key.Side);
            var member=party.Members[key.Index] with{Pokemon=WildBattleTransformationRules.PersistentPokemon(body),
                Status=WildBattleTransformationRules.PersistentStatus(body),HeldItem=body.HeldItem,KnockedOffItem=body.KnockedOffItem};
            next=next with{Slots=next.Slots.SetItem(slot.Value,cell with{Body=body}),
                Parties=next.Parties.SetItem((int)key.Side,party with{Members=party.Members.SetItem(key.Index,member)})};
        }
        // Source scans numeric bodies without an HP or absence predicate.
        foreach(var slot in next.Slots.Take(next.BattlerCount).Where(c=>c.Body is not null).Select(c=>c.Slot))
        {
            var cell=next.Get(slot);var body=cell.Body!;string ability=WildBattleEffective.Ability(body);
            var cure=SourceImmunityRules.Resolve(ability,body.Status,body.ConfusionTurnsRemaining,cell.Registers.InfatuationSource is not null);
            if(cure.Cure==SourceImmunityCure.None)continue;
            Add((n,t)=>new SourceAbilityActivatedEvent(n,t,slot,ability));
            if(cure.Cure==SourceImmunityCure.PrimaryStatus)
            {
                if (body.Nightmare) Add((n,t)=>new SourceTowerVolatileEvent(n,t,slot,"nightmare",false,cure.Reason));
                SetBody(slot,body with{Status=WildBattleStatus.None,Nightmare=false,SourcePartyStatusOverride=null});
                Add((n,t)=>new SourceStatusChangedEvent(n,t,slot,body.Status,WildBattleStatus.None,cure.Reason));
            }
            else if(cure.Cure==SourceImmunityCure.Confusion)
            {
                SetBody(slot,body with{ConfusionTurnsRemaining=0});
                Add((n,t)=>new SourceConfusionChangedEvent(n,t,slot,body.ConfusionTurnsRemaining,0,cure.Reason));
            }
            else
            {
                next=next with{Slots=next.Slots.SetItem(slot.Value,cell with{Registers=cell.Registers with{InfatuationSource=null}})};
                Add((n,t)=>new SourceInfatuationChangedEvent(n,t,slot,cell.Registers.InfatuationSource,null,cure.Reason));
            }
        }
        return new(next,events.ToImmutable(),[],null);
    }
}
