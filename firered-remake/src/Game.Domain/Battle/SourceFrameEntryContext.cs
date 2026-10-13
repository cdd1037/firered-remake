using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Randomness;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

internal sealed class FrameEntryContext(SourceBattleFrame frame, PokemonCatalog? catalog = null) : SourceEntryAbilityContext
{
    internal SourceBattleFrame Frame { get; private set; } = frame;
    internal readonly ImmutableArray<SourceBattleEvent>.Builder Events = ImmutableArray.CreateBuilder<SourceBattleEvent>();
    internal readonly ImmutableArray<RandomTrace>.Builder Trace = ImmutableArray.CreateBuilder<RandomTrace>();
    internal override int Turn => Frame.Turn;
    internal override int BattlerCount => Frame.BattlerCount;
    internal override SourceFieldRegisters Field { get => Frame.Field; set => Frame = Frame with { Field = value }; }
    internal override bool SuppressIntimidateTrace => false;
    internal override bool SinglesLivingTargets => Frame.Layout == SourceBattleLayout.Singles;
    internal override bool IsAbsent(BattlerSlot slot) => Frame.IsSourceAbsent(slot);
    internal override WildBattleCombatant Get(BattlerSlot slot) => Frame.Get(slot).Body ?? throw new SourceRecipeUnsupported("source_entry_absent_body");
    internal override void Set(BattlerSlot slot, WildBattleCombatant body)
    {
        // Entry callbacks mutate only battle registers, never permanent party identity.
        var cell = Frame.Get(slot); Frame = Frame with { Slots = Frame.Slots.SetItem(slot.Value, cell with { Body = body }) };
    }
    internal override void Add(Func<long, SourceBattleEvent> create)
    {
        var published = SourceBattleEventHooks.Publish(Frame, (n, _) => create(n)); Frame = published.State; Events.Add(published.Event);
    }
    internal override ushort Draw(string purpose, int bound)
    {
        var draw = Gen3Rng.Next(Frame.Rng, purpose, bound); Frame = Frame with { Rng = draw.State }; Trace.Add(draw.Trace); return draw.Value;
    }
    internal override void RecordAbility(BattlerSlot slot, string ability) { }
    internal override bool SourceCalledMist(BattlerSlot slot) => Frame.Sides[(int)slot.Side].MistTurnsRemaining > 0;
    internal override bool SourceCalledSecondaryBlocked(BattlerSlot slot) => Get(slot).CalledVolatile?.SubstituteHp > 0;
    internal override void SourceCalledProtectionFailure(BattlerSlot actor, BattlerSlot target, MoveId move, string reason) =>
        Add(n => new SourceMoveFailedEvent(n, Turn, actor, target, move, reason));
    internal override void ArmIntimidate(BattlerSlot slot)
    {
        var entry = Frame.EntryAbilities ?? throw new SourceRecipeUnsupported("source_entry_lifecycle_required");
        Frame = Frame with { EntryAbilities = entry with { PendingIntimidateMask = (byte)(entry.PendingIntimidateMask | slot.Mask) } };
    }
    internal void Spikes(PokemonCatalog catalog, BattlerSlot slot)
    {
        if (Frame.Sides[(int)slot.Side].Spikes == 0) return;
        // Reuse the residual owner's permanent synchronization and actual-occupant faint command.
        var hazard = new FrameEndTurnContext(Frame);
        SourceSpikesRules.Enter(catalog, slot, Frame.Sides[(int)slot.Side].Spikes, Turn,
            hazard.Get, hazard.Set, hazard.Add, hazard.Faint,
            () => hazard.Add(n => new SourceMtMoonVolatileEvent(n, Turn, slot, "spikes", 0, 1, "entry")));
        hazard.CheckTeamsLost();
        Frame = hazard.Frame; Events.AddRange(hazard.Events);
    }
    internal void ReplacementAbilities(BattlerSlot slot)
    {
        var mon = Get(slot);
        if (mon.Pokemon.CurrentHp == 0) return;
        if (WildBattleEffective.Ability(mon) == "ABILITY_TRUANT") Set(slot, mon with { TruantLoafing = true });
        SwitchIn(slot);
        SwitchInItems(slot);
    }
    internal void SwitchInItems(BattlerSlot slot)
    {
        var items = SourceFrameBerryItems.ApplyCanonical(Frame, SourceBerryPhase.SwitchIn, slot, catalog);
        Frame = items.State; Events.AddRange(items.Events); Trace.AddRange(items.Trace);
    }
    internal void Settle(bool forecast = true, bool items = true)
    {
        // Original ABILITYEFFECT_INTIMIDATE1 precedes TRACE, each scans numeric battler IDs.
        for (int i = 0; i < BattlerCount; i++)
        {
            var slot = new BattlerSlot(i); var entry = Frame.EntryAbilities;
            if (entry is null || (entry.PendingIntimidateMask & slot.Mask) == 0) continue;
            Frame = Frame with { EntryAbilities = entry with { PendingIntimidateMask = (byte)(entry.PendingIntimidateMask & ~slot.Mask) } };
            Intimidate(slot);
        }
        ResolveTrace();
        // HandleFaintedMonActions case 6 runs normal(moveTurn=TRUE) for battler 0 only.
        if (items)
        {
            var berries = SourceFrameBerryItems.ApplyCanonical(Frame, SourceBerryPhase.NormalMoveTurn, new(0), catalog);
            Frame = berries.State; Events.AddRange(berries.Events); Trace.AddRange(berries.Trace);
        }
        if (forecast) ForecastAll();
    }
}
