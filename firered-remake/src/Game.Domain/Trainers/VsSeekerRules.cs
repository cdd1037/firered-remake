using System.Collections.Immutable;
using Game.Domain.Randomness;

namespace Game.Domain.Trainers;

public sealed record VsSeekerRematch(int Index, string SourceMap, ImmutableArray<string> Trainers,
    ImmutableArray<int> TrainerSourceIds);
public sealed record VsSeekerActorBinding(string SourceMap, int TemplateIndex, int LocalId, string? SourceLocalId,
    string TrainerSymbol, string ScriptLabel, string BattleMacro, string TrainerType, string GraphicsSymbol,
    int GraphicsSourceId, int X, int Y, string MovementSymbol, string Flag);
public sealed record VsSeekerAcquisition(string SourceMap, int TemplateIndex, int LocalId, int X, int Y,
    string ScriptLabel, string SourcePath, string OwnershipFlag, string ItemSymbol, bool FlagSetBeforeGiveItem, int InitialChargeSteps);
public sealed record VsSeekerRosterRequirement(string TrainerSymbol, int SourceId, string PartyMacro, string PartySymbol, bool DoubleBattle);

/// <summary>Source-derived data, with stable source symbols. SourceMap on a rematch is unused original metadata;
/// ActorBindings identifies the actual map (several Route21 rows belong to Route21_South).</summary>
public sealed record VsSeekerCatalog(ImmutableArray<VsSeekerRematch> Rematches, ImmutableArray<VsSeekerActorBinding> ActorBindings,
    VsSeekerAcquisition Acquisition, ImmutableDictionary<int, int> RunningMovementByGraphics,
    int DefaultRunningMovement, ImmutableArray<VsSeekerRosterRequirement> RosterRequirements,
    ImmutableArray<string> ProgressFlags, ImmutableArray<string> ExcludedMaps)
{
    public VsSeekerRematch? FindBase(string trainer) => Rematches.FirstOrDefault(r => r.Trainers[0] == trainer);
    public VsSeekerRematch? FindAny(string trainer) => trainer is VsSeekerRules.None or VsSeekerRules.Skip
        ? null : Rematches.FirstOrDefault(r => r.Trainers.Contains(trainer));
    public int RunningMovement(int graphicSourceId) => RunningMovementByGraphics.GetValueOrDefault(graphicSourceId, DefaultRunningMovement);
}

/// <summary>Charge is the low source byte; RematchSteps is the high byte, not a second battery.
/// Sparse ranks are source array entries indexed by map-local ID and hold rank 1..5, not trainer IDs.</summary>
public sealed record VsSeekerState(byte ChargeSteps, byte RematchSteps, bool Charging, ImmutableSortedDictionary<int, byte> RematchRanks)
{
    public static VsSeekerState Empty { get; } = new(0, 0, false, ImmutableSortedDictionary<int, byte>.Empty);
    public ushort PackedCounter => (ushort)(ChargeSteps | (RematchSteps << 8));
}

/// <summary>Only NORMAL/BURIED templates, in original template order. Coordinates are current map tiles without
/// the source +7 border. ObjectIsSane means active, sprite owner matches slot, and map event count >= local ID.
/// HasTemplate tracks GetBaseTemplateForObjectEvent for source movement overrides. Missing lookups use slot16,
/// ObjectIsSane=false, HasTemplate=false; the source's resulting out-of-bounds RAM access is not emulated.</summary>
public sealed record VsSeekerTrainer(int TemplateIndex, int LocalId, string TrainerSymbol, int ObjectSlot,
    int X, int Y, bool ObjectIsSane, int GraphicsSourceId, int TemplateMovement = 0, int FacingDirection = 1, bool HasTemplate = true);
/// <summary>All object slots, including inactive/stale slots, in slot order. Map reset draws before checking validity.</summary>
public sealed record VsSeekerObject(int ObjectSlot, int LocalId, int Movement, bool Active, bool SpriteOwnerMatches);
public enum VsSeekerUseGuard { CanUse, NotOwned, InvalidMap, NotCharged, NoOneInRange }
public enum VsSeekerResponse { NoResponse, UnfoughtTrainers, FoundRematches }
public enum VsSeekerTrainerResponse { Unfought, NoRematch, Rematch }
public sealed record VsSeekerReaction(int LocalId, VsSeekerTrainerResponse Response);
public sealed record VsSeekerMovementChange(int LocalId, int ObjectSlot, int? LiveMovement, int? TemplateMovement, bool ResetSpriteOffsets = false);
public sealed record VsSeekerStepResult(VsSeekerState State, bool RematchesExpired);
public sealed record VsSeekerUseResult(VsSeekerState State, RngState Rng, VsSeekerUseGuard Guard,
    VsSeekerResponse? Response, ImmutableArray<VsSeekerReaction> Reactions,
    ImmutableArray<VsSeekerMovementChange> Movements, ImmutableArray<RandomTrace> RandomTrace);
public sealed record VsSeekerResetResult(VsSeekerState State, RngState Rng,
    ImmutableArray<VsSeekerMovementChange> Movements, ImmutableArray<RandomTrace> RandomTrace);

/// <summary>Original vs_seeker.c rules. The caller owns field/battle command transactions, save serialization,
/// active object gathering, source scripts, presentation and the shared RNG; no private RNG is created here.</summary>
public static class VsSeekerRules
{
    public const string None = "TRAINER_NONE";
    public const string Skip = "SKIP";
    public const int ChargeLimit = 100;
    public const int FaceUp = 7, FaceDown = 8, FaceLeft = 9, FaceRight = 10;
    public const int RaiseHandAndStop = 77, RaiseHandAndJump = 78, RaiseHandAndSwim = 79;

    public static void Validate(VsSeekerState state)
    {
        if (state.RematchRanks is null ||
            state.RematchRanks.Any(r => r.Key is < 1 or >= 100 || r.Value is < 1 or > 5))
            throw new ArgumentException("vs_seeker_state_invalid", nameof(state));
    }

    /// <summary>Optional save admission for normally reachable counters. Pure transitions preserve the source's
    /// byte behavior even for an already over-cap input, instead of silently normalizing it.</summary>
    public static bool HasReachableCounters(VsSeekerState state) => state.ChargeSteps <= 100 && state.RematchSteps <= 100;

    /// <summary>Invoke only when the original step-count hook is reached: successful non-forced step, no earlier
    /// coordinate/warp script, no Union Room/playback. Expiry takes priority over poison/egg/repel handling.</summary>
    public static VsSeekerStepResult AdvanceStep(VsSeekerState state, bool hasItem)
    {
        Validate(state);
        byte charge = (byte)(hasItem && state.ChargeSteps < 100 ? state.ChargeSteps + 1 : state.ChargeSteps);
        byte steps = (byte)(state.Charging && state.RematchSteps < 100 ? state.RematchSteps + 1 : state.RematchSteps);
        bool expired = state.Charging && steps == 100;
        return new(state with { ChargeSteps = charge, RematchSteps = expired ? (byte)0 : steps,
            Charging = state.Charging && !expired, RematchRanks = expired ? ImmutableSortedDictionary<int, byte>.Empty : state.RematchRanks }, expired);
    }

    public static bool IsVisible(VsSeekerTrainer trainer, int playerX, int playerY) => trainer.ObjectIsSane &&
        trainer.X >= (long)playerX - 7 && trainer.X <= (long)playerX + 7 &&
        trainer.Y >= (long)playerY - 5 && trainer.Y <= (long)playerY + 5;

    public static bool IsMapAllowed(VsSeekerCatalog catalog, string mapSymbol, string mapType) =>
        (mapType is "MAP_TYPE_ROUTE" or "MAP_TYPE_TOWN" or "MAP_TYPE_CITY") && !catalog.ExcludedMaps.Contains(mapSymbol);

    /// <summary>GetNextAvailableRematchTrainer returns a rank, preserves SKIP holes and repeats the highest party.</summary>
    public static byte NextRank(VsSeekerCatalog catalog, string baseTrainer, IReadOnlySet<string> fought)
    {
        var row = catalog.FindBase(baseTrainer);
        if (row is null) return 0;
        for (byte rank = 1; rank < 6; rank++)
        {
            if (row.Trainers[rank] == None) return (byte)(rank - 1);
            if (row.Trainers[rank] == Skip || fought.Contains(row.Trainers[rank])) continue;
            return rank;
        }
        return 5;
    }

    /// <summary>Progress is checked once for the proposed rank, then falls back to the previous non-SKIP party.
    /// This deliberately does not recursively check earlier progression flags.</summary>
    public static byte GateRank(VsSeekerCatalog catalog, VsSeekerRematch row, byte rank, IReadOnlySet<string> flags)
    {
        if (rank > 5) throw new ArgumentOutOfRangeException(nameof(rank));
        if (rank == 0 || flags.Contains(catalog.ProgressFlags[rank])) return rank;
        while (--rank != 0) if (row.Trainers[rank] != Skip) return rank;
        return 0;
    }

    public static string RematchTrainer(VsSeekerCatalog catalog, string baseTrainer, IReadOnlySet<string> fought, IReadOnlySet<string> flags)
    {
        byte rank = NextRank(catalog, baseTrainer, fought);
        return rank == 0 ? None : catalog.FindBase(baseTrainer)!.Trainers[GateRank(catalog, catalog.FindBase(baseTrainer)!, rank, flags)];
    }

    public static bool ShouldTryRematch(VsSeekerCatalog catalog, VsSeekerState state, string baseTrainer, int lastTalked, IReadOnlySet<string> fought) =>
        catalog.FindBase(baseTrainer) is not null && (state.RematchRanks.ContainsKey(lastTalked) || fought.Contains(baseTrainer));
    public static bool IsReady(VsSeekerCatalog catalog, VsSeekerState state, string trainer, int lastTalked) =>
        catalog.FindAny(trainer) is not null && state.RematchRanks.ContainsKey(lastTalked);

    public static VsSeekerUseGuard CanUse(VsSeekerCatalog catalog, VsSeekerState state, bool hasItem, string mapSymbol,
        string mapType, IReadOnlyList<VsSeekerTrainer> trainers, int playerX, int playerY, IReadOnlySet<string> fought)
    {
        Validate(state);
        if (!hasItem) return VsSeekerUseGuard.NotOwned;
        if (!IsMapAllowed(catalog, mapSymbol, mapType)) return VsSeekerUseGuard.InvalidMap;
        if (state.ChargeSteps != 100) return VsSeekerUseGuard.NotCharged;
        return trainers.Any(t => IsVisible(t, playerX, playerY) && (!fought.Contains(t.TrainerSymbol) || NextRank(catalog, t.TrainerSymbol, fought) != 0))
            ? VsSeekerUseGuard.CanUse : VsSeekerUseGuard.NoOneInRange;
    }

    public static VsSeekerUseResult Use(VsSeekerCatalog catalog, VsSeekerState state, bool hasItem, string mapSymbol,
        string mapType, IReadOnlyList<VsSeekerTrainer> trainers, int playerX, int playerY, IReadOnlySet<string> fought, RngState rng)
    {
        ValidateTrainers(trainers);
        var guard = CanUse(catalog, state, hasItem, mapSymbol, mapType, trainers, playerX, playerY, fought);
        if (guard != VsSeekerUseGuard.CanUse) return new(state, rng, guard, null, [], [], []);
        return ResolveResponse(catalog, state, trainers, playerX, playerY, fought, rng);
    }

    /// <summary>Task_VsSeeker_2 completion: reset item charge and resolve the previously gathered trainer snapshot.
    /// Normal callers must have passed CanUse; exposed separately so presentation waits do not repeat guards.</summary>
    public static VsSeekerUseResult ResolveResponse(VsSeekerCatalog catalog, VsSeekerState state,
        IReadOnlyList<VsSeekerTrainer> trainers, int playerX, int playerY, IReadOnlySet<string> fought, RngState rng)
    {
        Validate(state); ValidateTrainers(trainers);
        var reactions = ImmutableArray.CreateBuilder<VsSeekerReaction>();
        var movements = ImmutableArray.CreateBuilder<VsSeekerMovementChange>();
        var trace = ImmutableArray.CreateBuilder<RandomTrace>();
        var respondents = new List<(string Trainer, int Movement)>();
        var priorVisible = new HashSet<string>(StringComparer.Ordinal);
        var ranks = state.RematchRanks;
        bool unfought = false;
        foreach (var trainer in trainers)
        {
            if (!IsVisible(trainer, playerX, playerY)) continue;
            if (!fought.Contains(trainer.TrainerSymbol))
            {
                unfought = true;
                reactions.Add(new(trainer.LocalId, VsSeekerTrainerResponse.Unfought));
                priorVisible.Add(trainer.TrainerSymbol);
                continue;
            }
            byte rank = NextRank(catalog, trainer.TrainerSymbol, fought);
            bool yes = false;
            if (rank != 0)
            {
                var draw = Gen3Rng.Next(rng, "vs-seeker-response:" + trainer.LocalId, 100);
                rng = draw.State; trace.Add(draw.Trace);
                int value = draw.Trace.Result;
                if (priorVisible.Contains(trainer.TrainerSymbol)) value = respondents.Any(r => r.Trainer == trainer.TrainerSymbol) ? 100 : 0;
                yes = value >= 30;
            }
            reactions.Add(new(trainer.LocalId, yes ? VsSeekerTrainerResponse.Rematch : VsSeekerTrainerResponse.NoRematch));
            if (yes)
            {
                ranks = ranks.SetItem(trainer.LocalId, rank);
                respondents.Add((trainer.TrainerSymbol, catalog.RunningMovement(trainer.GraphicsSourceId)));
            }
            priorVisible.Add(trainer.TrainerSymbol);
        }
        var response = respondents.Count > 0 ? VsSeekerResponse.FoundRematches : unfought ? VsSeekerResponse.UnfoughtTrainers : VsSeekerResponse.NoResponse;
        state = state with { ChargeSteps = 0, Charging = respondents.Count > 0 || state.Charging,
            RematchSteps = respondents.Count > 0 ? (byte)0 : state.RematchSteps, RematchRanks = ranks };
        // StartAllRespondantIdleMovements runs after the response animation, preserving source nested order.
        foreach (var respondent in respondents)
            foreach (var trainer in trainers)
                if (trainer.TrainerSymbol == respondent.Trainer)
                {
                    movements.Add(new(trainer.LocalId, trainer.ObjectSlot, trainer.ObjectIsSane ? respondent.Movement : null,
                        trainer.HasTemplate ? respondent.Movement : null));
                    state = state with { RematchRanks = state.RematchRanks.SetItem(trainer.LocalId, NextRank(catalog, trainer.TrainerSymbol, fought)) };
                }
        return new(state, rng, VsSeekerUseGuard.CanUse, response, reactions.ToImmutable(), movements.ToImmutable(), trace.ToImmutable());
    }

    /// <summary>After expiry's stop/freeze barrier, visit NORMAL/BURIED templates in template order. RNG is drawn
    /// for every raise-hand template, including inactive ones; low-byte item charge remains unchanged.</summary>
    public static VsSeekerResetResult ResetAfterChargeComplete(VsSeekerState state, IReadOnlyList<VsSeekerTrainer> trainers, RngState rng)
    {
        Validate(state); ValidateTrainers(trainers);
        var changes = ImmutableArray.CreateBuilder<VsSeekerMovementChange>(); var trace = ImmutableArray.CreateBuilder<RandomTrace>();
        foreach (var trainer in trainers)
        {
            if (!IsRaisedMovement(trainer.TemplateMovement)) continue;
            var draw = Gen3Rng.Next(rng, "vs-seeker-expiry-facing:" + trainer.LocalId, 4); rng = draw.State; trace.Add(draw.Trace);
            int movement = RandomFacing(draw.Trace.Result);
            changes.Add(new(trainer.LocalId, trainer.ObjectSlot, trainer.ObjectIsSane ? movement : null, movement));
        }
        return new(state, rng, changes.ToImmutable(), trace.ToImmutable());
    }

    public static VsSeekerResetResult ResetMap(VsSeekerState state, IReadOnlyList<VsSeekerObject> objects, RngState rng)
    {
        Validate(state);
        if (objects.Count > 16 || objects.Any(o => o.ObjectSlot is < 0 or >= 16) || objects.Select(o => o.ObjectSlot).Distinct().Count() != objects.Count ||
            !objects.Select(o => o.ObjectSlot).SequenceEqual(objects.Select(o => o.ObjectSlot).Order()))
            throw new ArgumentException("vs_seeker_object_order", nameof(objects));
        var changes = ImmutableArray.CreateBuilder<VsSeekerMovementChange>(); var trace = ImmutableArray.CreateBuilder<RandomTrace>();
        foreach (var obj in objects)
        {
            if (!IsRaisedMovement(obj.Movement)) continue;
            var draw = Gen3Rng.Next(rng, "vs-seeker-map-facing:" + obj.ObjectSlot, 4); rng = draw.State; trace.Add(draw.Trace);
            if (obj.Active && obj.SpriteOwnerMatches) changes.Add(new(obj.LocalId, obj.ObjectSlot, RandomFacing(draw.Trace.Result), null, true));
        }
        return new(state with { Charging = false, RematchSteps = 0, RematchRanks = ImmutableSortedDictionary<int, byte>.Empty },
            rng, changes.ToImmutable(), trace.ToImmutable());
    }

    /// <summary>Run on every trainer battle finish, before return/whiteout, not wild battle finish. The apparently
    /// unused Random call is retained once per matching template. All same-row local IDs clear, even on defeat.</summary>
    public static VsSeekerResetResult ResetAfterTrainerBattle(VsSeekerCatalog catalog, VsSeekerState state, string opponent,
        IReadOnlyList<VsSeekerTrainer> trainers, int selectedObjectSlot, RngState rng)
    {
        Validate(state); ValidateTrainers(trainers);
        var row = catalog.FindAny(opponent);
        var changes = ImmutableArray.CreateBuilder<VsSeekerMovementChange>(); var trace = ImmutableArray.CreateBuilder<RandomTrace>();
        if (row is not null)
            foreach (var trainer in trainers)
            {
                if (catalog.FindAny(trainer.TrainerSymbol)?.Index != row.Index) continue;
                var draw = Gen3Rng.Next(rng, "vs-seeker-battle-unused-facing:" + trainer.LocalId, 4); rng = draw.State; trace.Add(draw.Trace);
                int facing = trainer.FacingDirection switch { 0 or 1 => FaceDown, 2 => FaceUp, 3 => FaceLeft, 4 => FaceRight,
                    _ => throw new ArgumentException("vs_seeker_facing_direction") };
                changes.Add(new(trainer.LocalId, trainer.ObjectSlot, trainer.ObjectSlot == 16 ? null : trainer.ObjectSlot == selectedObjectSlot ? facing : FaceDown,
                    trainer.HasTemplate ? facing : null));
                state = state with { RematchRanks = state.RematchRanks.Remove(trainer.LocalId) };
            }
        return new(state, rng, changes.ToImmutable(), trace.ToImmutable());
    }

    /// <summary>Victory callback separately sets the actual selected opponent's fought flag in Application.</summary>
    public static VsSeekerState ClearLastTalked(VsSeekerState state, int lastTalked) => state with { RematchRanks = state.RematchRanks.Remove(lastTalked) };
    private static bool IsRaisedMovement(int movement) => movement is RaiseHandAndStop or RaiseHandAndJump or RaiseHandAndSwim;
    private static int RandomFacing(int value) => value switch { 0 => FaceUp, 1 => FaceDown, 2 => FaceLeft, 3 => FaceRight, _ => throw new ArgumentOutOfRangeException(nameof(value)) };
    private static void ValidateTrainers(IReadOnlyList<VsSeekerTrainer> trainers)
    {
        if (trainers.Count > 16 || trainers.Any(t => t.LocalId is < 1 or >= 100 || t.TemplateIndex is < 0 or >= 64 || t.ObjectSlot is < 0 or > 16 ||
                t.ObjectSlot == 16 && (t.ObjectIsSane || t.HasTemplate)) ||
            trainers.Select(t => t.LocalId).Distinct().Count() != trainers.Count ||
            trainers.Select(t => t.TemplateIndex).Distinct().Count() != trainers.Count ||
            !trainers.Select(t => t.TemplateIndex).SequenceEqual(trainers.Select(t => t.TemplateIndex).Order()))
            throw new ArgumentException("vs_seeker_trainer_order", nameof(trainers));
    }
}
