using System.Collections.Immutable;
using Game.Domain.Randomness;

namespace Game.Domain.Encounters;

/// <summary>TryGenerateWildMon(..., flags: 0), after the shared source roamer gate.
/// Sweet Scent never runs a walking-rate, terrain-change, cooldown, ability or Repel check.</summary>
public static class SweetScentEncounterRules
{
    public static EarlyLandEncounterResult Land(EarlyLandEncounterState state, EarlyLandEncounterTable table,
        byte behavior, RngState rng, OrdinaryLandEncounterDefinition? ordinary = null)
    {
        if (ordinary is null) EarlyLandEncounterRules.ValidateState(state, table);
        else ordinary.ValidateState(state, table);
        if (state.Pending is not null) throw new InvalidOperationException("sweet_scent_candidate_pending");
        var slotDraw = Gen3Rng.Next(rng, "encounter.land-slot", 100);
        var slot = table.Slots[EarlyLandEncounterRules.ChooseSlot(slotDraw.Trace.Result)];
        var levelDraw = Gen3Rng.Next(slotDraw.State, "encounter.level", slot.MaximumLevel - slot.MinimumLevel + 1);
        var candidate = new EarlyLandEncounterCandidate(table.Map, slot.Index, slot.Species, (byte)(slot.MinimumLevel + levelDraw.Trace.Result));
        return new(state with { Pending = new(candidate, behavior) }, levelDraw.State,
            EncounterCheckOutcome.CandidateReady, candidate, null, [new("global", slotDraw.Trace), new("global", levelDraw.Trace)]);
    }

    public static WaterEncounterResult Water(WaterEncounterState state, WaterEncounterTable table, byte behavior, RngState rng)
    {
        WaterEncounterRules.ValidateState(state, table);
        if (state.Pending is not null) throw new InvalidOperationException("sweet_scent_candidate_pending");
        var slotDraw = Gen3Rng.Next(rng, "encounter.water-slot", 100);
        var slot = table.Slots[WaterEncounterRules.ChooseSlot(slotDraw.Trace.Result)];
        int lo = Math.Min(slot.MinimumLevel, slot.MaximumLevel), hi = Math.Max(slot.MinimumLevel, slot.MaximumLevel);
        var levelDraw = Gen3Rng.Next(slotDraw.State, "encounter.level", hi - lo + 1);
        var candidate = new WaterEncounterCandidate(table.Map, slot.Index, slot.Species, (byte)(lo + levelDraw.Trace.Result));
        return new(state with { Pending = new(candidate, behavior) }, levelDraw.State,
            EncounterCheckOutcome.CandidateReady, candidate, null, [new("global", slotDraw.Trace), new("global", levelDraw.Trace)]);
    }
}
