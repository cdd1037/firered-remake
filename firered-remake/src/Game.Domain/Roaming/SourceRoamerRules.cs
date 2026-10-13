using System.Collections.Immutable;
using Game.Domain.Randomness;
using Game.Domain.Rules;

namespace Game.Domain.Roaming;

/// <summary>Pure source roamer lifecycle with the approved complete-IV and Roar fixes.
/// No catalog admission, campaign flags, serialization IO, battle AI or ordinary encounter RNG.</summary>
public static class SourceRoamerRules
{
    public const string SourceCommit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";
    public const string RulesVersion = "firered-roamer-lifecycle-fullivs-roar-v1";
    public const string PersistencePolicy = "explicit-runtime-preserve-on-continue-v1";
    public const int Level = 50;
    public const int MapGroup = 3;

    /// <summary>Celio's one-time creation. Even an inactive old individual cannot be rerolled.</summary>
    public static RoamerResult Initialize(RoamerState state, int starterVariable, RngState rng)
    {
        Validate(state); ValidateRng(rng);
        if (state.Individual is not null) throw new InvalidOperationException("Roamer was already initialized.");
        string species = starterVariable switch
        {
            0 => "pret:SPECIES_ENTEI", 1 => "pret:SPECIES_RAIKOU", 2 => "pret:SPECIES_SUICUNE",
            _ => throw new ArgumentOutOfRangeException(nameof(starterVariable))
        };
        if (rng.Cursor > ulong.MaxValue - 5) throw new OverflowException("RNG cursor cannot accommodate initialization.");
        var draws = new Draws(rng);
        uint pid = (uint)draws.Next("pokemon.personality.low16") |
            ((uint)draws.Next("pokemon.personality.high16") << 16);
        uint first = (uint)draws.Next("pokemon.ivs.hp-attack-defense");
        uint second = (uint)draws.Next("pokemon.ivs.speed-specialAttack-specialDefense");
        uint packedIvs = (first & 0x7fffu) | ((second & 0x7fffu) << 15);
        var individual = new RoamerIndividual(species, Level, pid, packedIvs,
            MaximumHp(species, packedIvs), 0, true);
        int route = SourceRoamerLocations.Rows[draws.Next("roamer.initial-route", 25)][0];
        var runtime = RoamerRuntime.Empty with { Location = new(MapGroup, route) };
        return draws.Result(new(individual, runtime));
    }

    /// <summary>Chosen deterministic save semantics: validate and return exact input state/RNG.
    /// This intentionally does not emulate source cold-Continue RAM reset/route reroll.</summary>
    public static RoamerResult RestoreSaved(RoamerState state, RngState rng)
    {
        Validate(state); ValidateRng(rng);
        return new(state, rng, []);
    }

    public static RoamerResult Transition(RoamerState state, RoamerTransition transition,
        RoamerMap destinationPlayerMap, RngState rng)
    {
        Validate(state); ValidateRng(rng); ValidateMap(destinationPlayerMap);
        if (!Enum.IsDefined(transition)) throw new ArgumentOutOfRangeException(nameof(transition));
        // Source overworld installs the destination and shifts player history before moving the beast.
        var history = state.Runtime.PlayerHistory;
        var shifted = state with { Runtime = state.Runtime with
        {
            PlayerHistory = [destinationPlayerMap, history[0], history[1]]
        } };
        var draws = new Draws(rng);
        if (transition == RoamerTransition.Warp)
            return draws.Result(MoveToOtherSet(shifted, draws));
        // Source consumes this gate even before Celio and after the beast becomes inactive.
        if (draws.Next("roamer.change-set-gate", 16) == 0)
            return draws.Result(MoveToOtherSet(shifted, draws));
        if (shifted.Individual?.Active != true) return draws.Result(shifted);
        var row = SourceRoamerLocations.Rows.First(x => x[0] == shifted.Runtime.Location.Number);
        int next;
        do
        {
            next = row[draws.Next("roamer.neighbor", 6) + 1];
        }
        while (next == 255 || shifted.Runtime.PlayerHistory[2] == new RoamerMap(MapGroup, next));
        return draws.Result(shifted with { Runtime = shifted.Runtime with { Location = new(MapGroup, next) } });
    }

    public static RoamerEncounterResult TryEncounter(RoamerState state,
        RoamerEncounterContext context, RngState rng)
    {
        Validate(state); ValidateRng(rng); ValidateContext(context);
        var draws = new Draws(rng);
        RoamerEncounterResult Result(RoamerEncounterDecision decision, RoamerEncounterCandidate? candidate = null) =>
            new(decision, candidate, draws.Rng, draws.Trace.ToImmutable());
        if (context.Method is RoamerEncounterMethod.Fishing or RoamerEncounterMethod.RockSmash)
            return Result(RoamerEncounterDecision.NotApplicable);
        bool sweetScent = context.Method is RoamerEncounterMethod.SweetScentLand or RoamerEncounterMethod.SweetScentWater;
        if (!sweetScent && (!context.HasEncounterTable || !context.OrdinaryGatesPassed))
            return Result(RoamerEncounterDecision.NoEncounter);
        var fallback = context.HasEncounterTable ? RoamerEncounterDecision.UseOrdinaryEncounter : RoamerEncounterDecision.NoEncounter;
        if (state.Individual?.Active != true || context.PlayerMap != state.Runtime.Location)
            return Result(fallback);
        if (draws.Next("roamer.encounter-gate", 4) != 0) return Result(fallback);
        if (!sweetScent && context.RepelActive)
        {
            var lead = context.Party.FirstOrDefault(x => x.CurrentHp > 0 && !x.IsEgg);
            if (lead is null || lead.Level > Level)
                return Result(RoamerEncounterDecision.NoEncounter); // Successful roamer gate, no ordinary fallback.
        }
        return Result(RoamerEncounterDecision.RoamerEncounter, RestoreEncounter(state.Individual));
    }

    /// <summary>Restores all six original IVs, not the original buggy single-byte truncation.
    /// No RNG parameter is needed or consumed; battle reconstruction still owns moves/PP/OT.</summary>
    public static RoamerEncounterCandidate RestoreEncounter(RoamerIndividual individual)
    {
        ValidateIndividual(individual);
        if (!individual.Active) throw new InvalidOperationException("Inactive roamer cannot appear.");
        return new(individual, UnpackIvs(individual.PackedIvs), MaximumHp(individual.Species, individual.PackedIvs));
    }

    public static RoamerResult Settle(RoamerState state, RoamerBattleResult battle, RngState rng)
    {
        Validate(state); ValidateRng(rng); ArgumentNullException.ThrowIfNull(battle);
        if (state.Individual is null || !state.Individual.Active)
            throw new InvalidOperationException("An active roamer encounter is required.");
        if (battle.EncounteredIndividual != state.Individual)
            throw new ArgumentException("Settlement individual does not match the encountered saved identity.", nameof(battle));
        if (!Enum.IsDefined(battle.Outcome)) throw new ArgumentOutOfRangeException(nameof(battle.Outcome));
        if (battle.CurrentHp < 0 || battle.CurrentHp > MaximumHp(state.Individual.Species, state.Individual.PackedIvs))
            throw new ArgumentOutOfRangeException(nameof(battle.CurrentHp));
        byte status = unchecked((byte)battle.Status);
        ValidateStatus(status);
        bool terminal = battle.Outcome is RoamerBattleOutcome.Won or RoamerBattleOutcome.Drew or RoamerBattleOutcome.Caught;
        if (battle.CurrentHp == 0 && !terminal)
            throw new ArgumentException("A still-active roamer cannot retain zero HP.", nameof(battle));
        var draws = new Draws(rng);
        // Preserve the source order even for a capture/KO: update HP/status, move while active, THEN deactivate.
        var updated = state with { Individual = state.Individual with { CurrentHp = battle.CurrentHp, Status = status } };
        updated = MoveToOtherSet(updated, draws);
        if (terminal) updated = updated with { Individual = updated.Individual! with { Active = false } };
        return draws.Result(updated);
    }

    public static ImmutableArray<int> UnpackIvs(uint packedIvs)
    {
        if (packedIvs > 0x3fffffffu) throw new ArgumentOutOfRangeException(nameof(packedIvs));
        return [(int)(packedIvs & 31), (int)((packedIvs >> 5) & 31), (int)((packedIvs >> 10) & 31),
            (int)((packedIvs >> 15) & 31), (int)((packedIvs >> 20) & 31), (int)((packedIvs >> 25) & 31)];
    }

    public static int SourceSpeciesId(string species) => species switch
    {
        "pret:SPECIES_RAIKOU" => 243, "pret:SPECIES_ENTEI" => 244, "pret:SPECIES_SUICUNE" => 245,
        _ => throw new ArgumentException("Unsupported source roamer species.", nameof(species))
    };

    public static int MaximumHp(string species, uint packedIvs)
    {
        _ = SourceSpeciesId(species);
        if (packedIvs > 0x3fffffffu) throw new ArgumentOutOfRangeException(nameof(packedIvs));
        int baseHp = species switch { "pret:SPECIES_RAIKOU" => 90, "pret:SPECIES_ENTEI" => 115, _ => 100 };
        return StatRules.CalculateHp(baseHp, (int)(packedIvs & 31), 0, Level);
    }

    /// <summary>Validates the isolated lifecycle schema. Campaign admission/quest flags remain the caller's job.</summary>
    public static void Validate(RoamerState state)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(state.Runtime);
        if (state.Runtime.PlayerHistory.IsDefault || state.Runtime.PlayerHistory.Length != 3)
            throw new ArgumentException("Exactly three player history maps are required.", nameof(state));
        foreach (var map in state.Runtime.PlayerHistory) ValidateMap(map);
        if (state.Individual is null)
        {
            if (state.Runtime.Location != default) throw new ArgumentException("Uninitialized roamer has no route.", nameof(state));
            return;
        }
        ValidateIndividual(state.Individual);
        if (state.Runtime.Location.Group != MapGroup || !SourceRoamerLocations.Rows.Any(x => x[0] == state.Runtime.Location.Number))
            throw new ArgumentException("Roamer must occupy an authenticated source route.", nameof(state));
    }

    public static void ValidateIndividual(RoamerIndividual individual)
    {
        ArgumentNullException.ThrowIfNull(individual);
        if (individual.Level != Level) throw new ArgumentException("Celio's roamer must remain level 50.", nameof(individual));
        int maxHp = MaximumHp(individual.Species, individual.PackedIvs);
        if (individual.CurrentHp < 0 || individual.CurrentHp > maxHp || (individual.Active && individual.CurrentHp == 0))
            throw new ArgumentException("Invalid saved roamer HP.", nameof(individual));
        ValidateStatus(individual.Status);
    }

    private static RoamerState MoveToOtherSet(RoamerState state, Draws draws)
    {
        if (state.Individual?.Active != true) return state;
        int next;
        do { next = SourceRoamerLocations.Rows[draws.Next("roamer.other-location-set", 25)][0]; }
        while (next == state.Runtime.Location.Number);
        return state with { Runtime = state.Runtime with { Location = new(MapGroup, next) } };
    }

    private static void ValidateContext(RoamerEncounterContext context)
    {
        ArgumentNullException.ThrowIfNull(context); ValidateMap(context.PlayerMap);
        if (!Enum.IsDefined(context.Method)) throw new ArgumentOutOfRangeException(nameof(context.Method));
        if (context.Party.IsDefault || context.Party.Length > 6) throw new ArgumentException("Invalid party snapshot.", nameof(context));
        foreach (var member in context.Party)
            if (member is null || member.Level is < 1 or > 100 || member.CurrentHp is < 0 or > ushort.MaxValue)
                throw new ArgumentException("Invalid party member.", nameof(context));
    }

    private static void ValidateMap(RoamerMap map)
    {
        if (map.Group is < 0 or > 255 || map.Number is < 0 or > 255) throw new ArgumentOutOfRangeException(nameof(map));
    }
    private static void ValidateRng(RngState rng)
    {
        if (rng.AlgorithmId != Gen3Rng.AlgorithmId) throw new NotSupportedException("Unsupported RNG algorithm.");
    }
    private static void ValidateStatus(byte status)
    {
        // Source persistent primary status: healthy, sleep count1..7, poison, burn, freeze, paralysis or toxic.
        if (status > 7 && status is not (8 or 16 or 32 or 64 or 128))
            throw new ArgumentException("Conflicting source primary status bits.", nameof(status));
    }

    private sealed class Draws(RngState rng)
    {
        public RngState Rng { get; private set; } = rng;
        public ImmutableArray<RandomTrace>.Builder Trace { get; } = ImmutableArray.CreateBuilder<RandomTrace>();
        public int Next(string purpose, int bound = 65536)
        {
            var draw = Gen3Rng.Next(Rng, purpose, bound);
            Rng = draw.State; Trace.Add(draw.Trace); return draw.Trace.Result;
        }
        public RoamerResult Result(RoamerState state) => new(state, Rng, Trace.ToImmutable());
    }
}
