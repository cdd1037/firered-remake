using System.Collections.Immutable;
using Game.Domain.WildBattle;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;

namespace Game.Domain.Battle;

/// <summary>Frame storage adapter for the shared berry rules. Switch-in admits only source switch-in items.
/// Move-end scans numeric positions; normal item callbacks name one actual position.</summary>
internal static class SourceFrameBerryItems
{
    internal static bool SupportsHeldItem(string item, PokemonCatalog? catalog = null) => item is "ITEM_CHOICE_BAND" or "ITEM_FOCUS_BAND" or "ITEM_KINGS_ROCK" or "ITEM_SHELL_BELL" ||
        SourceFourRewards.SupportsRewardHeldItem(item) || SourceHeldBerryRules.Supports(item) || SourceHeldItemCatalog.Supported(catalog, item);

    // Preserve the historical adapter's reflection/test contract without new item admission.
    internal static SourceFourActionResult Apply(SourceBattleFrame frame, SourceBerryPhase phase, BattlerSlot? only = null) =>
        ApplyCanonical(frame, phase, only);
    internal static SourceFourActionResult ApplyCanonical(SourceBattleFrame frame, SourceBerryPhase phase, BattlerSlot? only = null, PokemonCatalog? catalog = null)
    {
        var next = frame; var events = ImmutableArray.CreateBuilder<SourceBattleEvent>();
        var trace = ImmutableArray.CreateBuilder<RandomTrace>();
        ushort Draw(string purpose, int bound) { var d = Gen3Rng.Next(next.Rng, purpose, bound); next = next with { Rng = d.State }; trace.Add(d.Trace); return d.Value; }
        void Add(Func<long, int, SourceBattleEvent> create)
        {
            var result = SourceBattleEventHooks.Publish(next, create); next = result.State; events.Add(result.Event);
        }
        foreach (var slot in only is { } one ? [one] : Enumerable.Range(0, next.BattlerCount).Select(i => new BattlerSlot(i)))
        {
            var cell = next.Get(slot);
            if (cell.Body is not { } before) continue;
            if (phase == SourceBerryPhase.SwitchIn && SourceHeldItemCatalog.Find(catalog, before.HeldItem) is { } held)
                next = next with { Field = SourceBattleMoneyRules.ApplyEntry(next.Field, held.HoldEffect) };
            var effect = SourceHeldBerryRules.ResolveCanonical(catalog, before, phase, cell.Registers.InfatuationSource is not null,
                Draw, $"source-battle.slot-{slot.Value}.held-item");
            if (!effect.Activated) continue;
            var after = effect.Body;
            var key = cell.Member!.Value; var party = next.Party(key.Side);
            var member = party.Members[key.Index] with { Pokemon = WildBattleTransformationRules.PersistentPokemon(after),
                Status = WildBattleTransformationRules.PersistentStatus(after), HeldItem = after.HeldItem, KnockedOffItem = after.KnockedOffItem };
            if (effect.Consume) member = WildBattleSourcePartyEffects.WithPersistentHeldItem(member, "ITEM_NONE");
            next = next with { Slots = next.Slots.SetItem(slot.Value, cell with { Body = after,
                    Registers = effect.CureInfatuation ? cell.Registers with { InfatuationSource = null } : cell.Registers,
                    Position = effect.Consume ? cell.Position with { UsedHeldItem = before.HeldItem } : cell.Position }),
                Parties = next.Parties.SetItem((int)key.Side, party with { Members = party.Members.SetItem(key.Index, member) }) };
            SourceHeldItemEvents.Publish(before, effect, slot, cell.Registers.InfatuationSource, next.Turn,
                create => Add((n, _) => create(n)));
        }
        return new(next, events.ToImmutable(), trace.ToImmutable(), null);
    }
}
