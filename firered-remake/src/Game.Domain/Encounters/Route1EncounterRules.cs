using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Randomness;

namespace Game.Domain.Encounters;

/// <summary>
/// Source-backed candidate selection only. No Pokemon instance, battle, map movement, IO or presentation.
/// A candidate suspends before GenerateWildMon; complete it only after the downstream transaction succeeds.
/// </summary>
public static class Route1EncounterRules
{
    public const string SourceCommit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";
    public const string RulesVersion = "firered-route1-walking-candidates-v1";
    public const int RawEncounterRate = 21;
    public const int BaseCooldown = 6;
    public const byte TallGrassBehavior = 2;
    public static MapId Map { get; } = new("map.route-1");
    public static ImmutableArray<Route1EncounterSlot> Slots { get; } = [
        new(0, new("pret:SPECIES_PIDGEY"), 3, 3, 20),
        new(1, new("pret:SPECIES_RATTATA"), 3, 3, 20),
        new(2, new("pret:SPECIES_PIDGEY"), 3, 3, 10),
        new(3, new("pret:SPECIES_RATTATA"), 3, 3, 10),
        new(4, new("pret:SPECIES_PIDGEY"), 2, 2, 10),
        new(5, new("pret:SPECIES_RATTATA"), 2, 2, 10),
        new(6, new("pret:SPECIES_PIDGEY"), 3, 3, 5),
        new(7, new("pret:SPECIES_RATTATA"), 3, 3, 5),
        new(8, new("pret:SPECIES_PIDGEY"), 4, 4, 4),
        new(9, new("pret:SPECIES_RATTATA"), 4, 4, 4),
        new(10, new("pret:SPECIES_PIDGEY"), 5, 5, 1),
        new(11, new("pret:SPECIES_RATTATA"), 4, 4, 1)
    ];

    /// <summary>ResetMenuAndMonGlobals: one global Random() seeds the independent rate stream as u16.</summary>
    public static Route1EncounterInitialization SeedFromGlobal(RngState globalRng, ushort previousMetatileBehavior = 0)
    {
        if (previousMetatileBehavior > byte.MaxValue) throw new ArgumentOutOfRangeException(nameof(previousMetatileBehavior));
        var draw = Gen3Rng.Next(globalRng, "encounter.seed-rate-rng");
        return new(new(new(draw.Value), previousMetatileBehavior), draw.State, [new("global", draw.Trace)]);
    }

    public static Route1EncounterResult Check(Route1EncounterState state, Route1EncounterCheck check,
        Route1EncounterProfile profile, RngState globalRng)
    {
        ValidateState(state);
        ArgumentNullException.ThrowIfNull(check);
        ValidateProfile(profile);
        if (state.Pending is not null) throw new InvalidOperationException("A wild candidate is pending; complete its generation/start transaction before another field check.");
        if (globalRng.AlgorithmId != Gen3Rng.AlgorithmId) throw new NotSupportedException("Unknown global RNG algorithm.");
        if (check.Map != Map) throw new NotSupportedException("Only the fixed FireRed Route 1 land profile is supported.");
        if (!Enum.IsDefined(check.Trigger) || !Enum.IsDefined(check.EncounterType)) throw new NotSupportedException("Unknown field check or encounter terrain.");
        if (check.EncounterType == EncounterTileType.Water ||
            (check.EncounterType == EncounterTileType.Land && check.MetatileBehavior != TallGrassBehavior))
            throw new NotSupportedException("Only Route 1 tall-grass land encounters and non-encounter tiles are supported.");
        if (check.EncounterType == EncounterTileType.None && check.MetatileBehavior == TallGrassBehavior)
            throw new ArgumentException("Route 1 tall grass must retain its source land encounter attribute.");

        // Keep the original public profile/state/serialization contract while sharing source equations.
        var result = EarlyLandEncounterRules.CheckCore(new(state.RateRng, state.PreviousMetatileBehavior,
            state.EncounterRateBuff, state.StepsSinceLastEncounter), EarlyLandEncounterRules.Route1Table,
            new(check.Map, check.Trigger, check.MetatileBehavior, check.EncounterType), profile.WildEncountersDisabled, globalRng);
        Route1EncounterCandidate? candidate = result.Candidate is {} c ? new(c.Map, c.Slot, c.Species, c.Level) : null;
        var next = state with { RateRng = result.State.RateRng, PreviousMetatileBehavior = result.State.PreviousMetatileBehavior,
            EncounterRateBuff = result.State.EncounterRateBuff, StepsSinceLastEncounter = result.State.StepsSinceLastEncounter,
            Pending = candidate is null ? null : new(candidate, check.MetatileBehavior) };
        return new(next, result.GlobalRng, result.Outcome, candidate, result.RateThreshold, result.RandomTrace);
    }

    /// <summary>Source tail of successful TryStandardWildEncounter; this does not create a monster or start a battle.</summary>
    public static Route1EncounterState CompleteCandidate(Route1EncounterState state)
    {
        ValidateState(state);
        var pending = state.Pending ?? throw new InvalidOperationException("No encounter candidate is pending.");
        return state with { EncounterRateBuff = 0, StepsSinceLastEncounter = 0,
            PreviousMetatileBehavior = pending.MetatileBehavior, Pending = null };
    }

    /// <summary>ResetEncounterRateModifiers used by connected-map loads, warps and battle starts. Never reseeds.</summary>
    public static Route1EncounterState ResetRateModifiers(Route1EncounterState state)
    {
        ValidateState(state);
        if (state.Pending is not null) throw new InvalidOperationException("Cannot reset across an unfinished encounter transaction.");
        return state with { EncounterRateBuff = 0, StepsSinceLastEncounter = 0 };
    }

    public static int ChooseSlot(int roll)
    {
        if (roll is < 0 or >= 100) throw new ArgumentOutOfRangeException(nameof(roll));
        int end = 0;
        foreach (var slot in Slots)
        {
            end += slot.Weight;
            if (roll < end) return slot.Index;
        }
        throw new InvalidOperationException("Invalid source land slot table.");
    }

    public static void ValidateState(Route1EncounterState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.RulesVersion != RulesVersion || state.RateRng.AlgorithmId != EncounterRateRng.AlgorithmId)
            throw new NotSupportedException("Unsupported encounter process version or rate RNG.");
        if (state.PreviousMetatileBehavior > byte.MaxValue || state.StepsSinceLastEncounter > BaseCooldown)
            throw new ArgumentException("Encounter state is outside the bounded walking profile.");
        if (state.Pending is not null)
        {
            var candidate = state.Pending.Candidate;
            if (candidate is null || candidate.Map != Map || candidate.Slot < 0 || candidate.Slot >= Slots.Length ||
                state.Pending.MetatileBehavior != TallGrassBehavior)
                throw new ArgumentException("Invalid pending wild candidate.");
            var slot = Slots[candidate.Slot];
            if (candidate.Species != slot.Species || candidate.Level < slot.MinimumLevel || candidate.Level > slot.MaximumLevel)
                throw new ArgumentException("Pending wild candidate disagrees with the pinned Route 1 slot.");
        }
    }

    private static void ValidateProfile(Route1EncounterProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (profile.RulesVersion != RulesVersion || profile.Movement != EncounterMovementMode.Walking || profile.RepelSteps != 0 ||
            profile.LeadAbility is not ("ABILITY_NONE" or "ABILITY_OVERGROW" or "ABILITY_BLAZE" or "ABILITY_TORRENT") ||
            profile.LeadHeldItem != "ITEM_NONE" || profile.WhiteFluteActive || profile.BlackFluteActive || profile.RoamerActive || profile.QuestLogPlayback)
            throw new NotSupportedException("This profile supports ordinary walking, no Repel, no held item, an unmodified starter lead, no flute, inactive roamer and no Quest Log playback.");
    }
}
