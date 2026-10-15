using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.World;

namespace Game.Domain.Encounters;

public sealed record RockSmashEncounterTable(MapId Map, int RawEncounterRate, ImmutableArray<WaterEncounterSlot> Slots);
public sealed record RockSmashEncounterCandidate(MapId Map, int Slot, SpeciesId Species, byte Level);
public enum RockSmashEncounterOutcome { NoTable, RateMiss, RepelSuppressed, CandidateReady }

/// <summary>Pass the original cached held-item bit from the encounter process, not a newly sampled lead item.
/// RockSmashWildEncounter bypasses the standard cooldown function that updates that cache.</summary>
public sealed record RockSmashEncounterContext(EncounterRateRngState RateRng, ushort EncounterRateBuff = 0,
    bool RidingBicycle = false, bool WhiteFluteActive = false, bool BlackFluteActive = false,
    bool CachedLeadHasCleanseTag = false);
public sealed record RockSmashEncounterResult(RockSmashEncounterContext Context, RngState GlobalRng,
    RockSmashEncounterOutcome Outcome, RockSmashEncounterCandidate? Candidate, int? RateThreshold,
    ImmutableArray<EncounterRandomTrace> RandomTrace);

/// <summary>The RockSmashWildEncounter special through slot/level/Repel only. A source-owned ordinary wild
/// factory must generate the candidate before Application can publish actor removal and start a battle.</summary>
public static class RockSmashEncounterRules
{
    // DoWildEncounterRateTest(..., TRUE): bike, buff, flute, cached Cleanse Tag, cap; abilities are ignored.
    public static int RateThreshold(int rawRate, RockSmashEncounterContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (rawRate is < 0 or > 255) throw new ArgumentOutOfRangeException(nameof(rawRate));
        int rate = rawRate * 16;
        if (context.RidingBicycle) rate = rate * 80 / 100;
        rate += context.EncounterRateBuff * 16 / 200;
        if (context.WhiteFluteActive) rate += rate / 2; // White wins if both source flags are set.
        else if (context.BlackFluteActive) rate /= 2;
        if (context.CachedLeadHasCleanseTag) rate = rate * 2 / 3;
        return Math.Min(1600, rate);
    }

    public static RockSmashEncounterResult Check(FieldRockSmashCatalog catalog, MapId map,
        RockSmashEncounterContext context, ImmutableArray<PartyPokemon> party, ushort repelSteps, RngState globalRng)
    {
        ArgumentNullException.ThrowIfNull(catalog); ArgumentNullException.ThrowIfNull(context);
        if (!catalog.HasRockMap(map)) throw new ArgumentException("rock_smash_map_not_in_source", nameof(map));
        if (context.RateRng.AlgorithmId != EncounterRateRng.AlgorithmId || globalRng.AlgorithmId != Gen3Rng.AlgorithmId)
            throw new ArgumentException("rock_smash_rng_algorithm");
        if (party.IsDefault || party.Length > 6 || party.Any(mon => mon is null || mon.Pokemon is null ||
            mon.Pokemon.Level is < 1 or > 100 || mon.Pokemon.CurrentHp < 0)) throw new ArgumentException("rock_smash_party");
        var table = catalog.EncountersFor(map);
        // The Four Island source rock has no header/table: no RNG, no cooldown and no rate modifier changes.
        if (table is null) return new(context, globalRng, RockSmashEncounterOutcome.NoTable, null, null, []);
        var trace = ImmutableArray.CreateBuilder<EncounterRandomTrace>();
        int threshold = RateThreshold(table.RawEncounterRate, context);
        var rate = EncounterRateRng.Next(context.RateRng);
        context = context with { RateRng = rate.State };
        trace.Add(new("wild-rate", rate.Trace));
        RockSmashEncounterResult Finish(RockSmashEncounterOutcome outcome, RockSmashEncounterCandidate? candidate = null) =>
            new(context, globalRng, outcome, candidate, threshold, trace.ToImmutable());
        if (rate.Trace.Result >= threshold) return Finish(RockSmashEncounterOutcome.RateMiss);
        int Draw(string purpose, int bound)
        {
            var draw = Gen3Rng.Next(globalRng, purpose, bound); globalRng = draw.State;
            trace.Add(new("global", draw.Trace)); return draw.Trace.Result;
        }
        var slot = table.Slots[WaterEncounterRules.ChooseSlot(Draw("encounter.rock-smash-slot", 100))];
        int low = Math.Min(slot.MinimumLevel, slot.MaximumLevel), high = Math.Max(slot.MinimumLevel, slot.MaximumLevel);
        byte level = (byte)(low + Draw("encounter.level", high - low + 1));
        // The first living, non-Egg member is the Repel comparison, not necessarily the move user or slot 0.
        var repelLead = party.FirstOrDefault(member => !member.IsEgg && member.Pokemon.CurrentHp > 0);
        if (repelSteps != 0 && (repelLead is null || level < repelLead.Pokemon.Level))
            return Finish(RockSmashEncounterOutcome.RepelSuppressed);
        // No standard-walking cooldown/global pre-roll, ability modifier, roamer check or rate-buff mutation.
        // Preserve these inputs even on a miss or Repel rejection. Common battle-return resets are separate.
        return Finish(RockSmashEncounterOutcome.CandidateReady, new(map, slot.Index, slot.Species, level));
    }
}
