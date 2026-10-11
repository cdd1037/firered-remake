using System.Collections.Immutable;
using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

/// <summary>Frame storage adapter for the shared berry rules. No switch-in berry phase exists.
/// Move-end scans numeric positions; normal item callbacks name one actual position.</summary>
internal static class SourceFrameBerryItems
{
    internal static bool SupportsHeldItem(string item) => item == "ITEM_FOCUS_BAND" ||
        SourceFourRewards.SupportsRewardHeldItem(item) || SourceHeldBerryRules.Supports(item);

    internal static SourceFourActionResult Apply(SourceBattleFrame frame, SourceBerryPhase phase, BattlerSlot? only = null)
    {
        var next = frame; var events = ImmutableArray.CreateBuilder<SourceBattleEvent>();
        void Add(Func<long, int, SourceBattleEvent> create)
        {
            var result = SourceBattleEventHooks.Publish(next, create); next = result.State; events.Add(result.Event);
        }
        foreach (var slot in only is { } one ? [one] : Enumerable.Range(0, next.BattlerCount).Select(i => new BattlerSlot(i)))
        {
            var cell = next.Get(slot);
            if (cell.Body is not { } before) continue;
            var effect = SourceHeldBerryRules.Resolve(before, phase);
            if (!effect.Consumed) continue;
            var after = before with { HeldItem = "ITEM_NONE", Pokemon = before.Pokemon with {
                CurrentHp = before.Pokemon.CurrentHp + effect.HpRestored } };
            if (effect.CureStatus) after = after with { Status = WildBattleStatus.None, SourcePartyStatusOverride = null,
                Nightmare = WildBattleStatus.IsSleeping(before.Status) ? false : before.Nightmare };
            if (effect.CureConfusion) after = after with { ConfusionTurnsRemaining = 0 };
            var key = cell.Member!.Value; var party = next.Party(key.Side);
            var member = party.Members[key.Index] with { Pokemon = WildBattleTransformationRules.PersistentPokemon(after),
                Status = WildBattleTransformationRules.PersistentStatus(after), HeldItem = after.HeldItem, KnockedOffItem = after.KnockedOffItem };
            member = WildBattleSourcePartyEffects.WithPersistentHeldItem(member, "ITEM_NONE");
            next = next with { Slots = next.Slots.SetItem(slot.Value, cell with { Body = after,
                    Position = cell.Position with { UsedHeldItem = before.HeldItem } }),
                Parties = next.Parties.SetItem((int)key.Side, party with { Members = party.Members.SetItem(key.Index, member) }) };
            if (effect.CureConfusion) Add((n,t) => new SourceConfusionChangedEvent(n,t,slot,before.ConfusionTurnsRemaining,0,"persim-berry"));
            if (effect.CureStatus) Add((n,t) => new SourceStatusChangedEvent(n,t,slot,before.Status,WildBattleStatus.None,"held-berry"));
            Add((n,t) => new SourceHeldItemConsumedEvent(n,t,slot,before.HeldItem,before.Pokemon.CurrentHp,after.Pokemon.CurrentHp,effect.HpRestored));
        }
        return new(next, events.ToImmutable(), [], null);
    }
}
