using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json.Serialization;

namespace Game.Domain.Trainers;

/// <summary>The semantic, unencrypted fields of one source SaveBlock1 TrainerTower record.</summary>
public sealed record TrainerTowerCourseRecord(
    int TimerFrames,
    int BestTimeFrames,
    int FloorsCleared,
    int SetId,
    bool ReceivedPrize,
    bool CheckedFinalTime,
    bool SpokeToOwner,
    bool HasLost,
    bool UnknownStatus,
    bool Validated);

/// <summary>Transient source script progress (VAR_TEMP_1); never a stable field-save boundary.</summary>
public sealed record TrainerTowerFloorChain(int FloorNumber, TrainerTowerBattleFormat Format, int TrainerIndex);

/// <summary>
/// The four source records, selected challenge, and counter-running intent. The map-scene gate
/// belongs to Application; resetting that gate on exterior entry does not disable this counter.
/// </summary>
public sealed record TrainerTowerProgressState(
    TrainerTowerChallengeKind SelectedCourse,
    ImmutableArray<TrainerTowerCourseRecord> Records,
    bool TimerRunning,
    TrainerTowerFloorChain? FloorChain)
{
    [JsonIgnore]
    public TrainerTowerCourseRecord CurrentRecord => Records[(int)SelectedCourse];
}

public enum TrainerTowerChallengeStatus { Lost = 0, Unknown = 1, Normal = 2 }
public enum TrainerTowerPrizeResult { Received = 0, BagFull = 1, AlreadyReceived = 2 }
public enum TrainerTowerTimeResult { NewRecord = 0, NotImproved = 1, AlreadyChecked = 2 }

public sealed record TrainerTowerOwnerCheck(TrainerTowerProgressState State, int Result);
public sealed record TrainerTowerPrizeCheck(TrainerTowerProgressState State, TrainerTowerPrizeResult Result, string Item);
public sealed record TrainerTowerTimeCheck(TrainerTowerProgressState State, TrainerTowerTimeResult Result);
public sealed record TrainerTowerStatusCheck(TrainerTowerProgressState State, TrainerTowerChallengeStatus Status);

public readonly record struct TrainerTowerTime(int Minutes, int Seconds, int Centiseconds)
{
    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"{Minutes:00}:{Seconds:00}.{Centiseconds:00}");
}

/// <summary>
/// Local set 1 rules from pret/pokefirered 037335f4c725d7c9aecdac87066f2002b4bd7e14,
/// src/trainer_tower.c and data/scripts/trainer_tower.inc. No clock, battle engine, inventory,
/// map, or persistence side effects are performed here.
/// </summary>
public static class TrainerTowerProgressRules
{
    public const int LocalSetId = 1;
    public const int CourseCount = 4;
    public const int FloorCount = 8;
    public const int FramesPerSecond = 60;
    public const int MaximumTimeFrames = 215999;

    public static TrainerTowerProgressState Create() => new(
        TrainerTowerChallengeKind.Single,
        Enumerable.Range(0, CourseCount).Select(_ => new TrainerTowerCourseRecord(
            0, MaximumTimeFrames, 0, LocalSetId, false, false, false, false, false, false)).ToImmutableArray(),
        false,
        null);

    /// <summary>
    /// Call only after the entry script's party heal and start-clock dialogue. As in the source,
    /// an out-of-range menu value selects Single; a new run retains best time, prize and loss flags.
    /// </summary>
    public static TrainerTowerProgressState Start(TrainerTowerProgressState state, TrainerTowerChallengeKind course)
    {
        Validate(state);
        if ((uint)course >= CourseCount) course = TrainerTowerChallengeKind.Single;
        state = state with { SelectedCourse = course, TimerRunning = true, FloorChain = null };
        return ReplaceCurrent(state, state.CurrentRecord with
        {
            TimerFrames = 0,
            FloorsCleared = 0,
            SpokeToOwner = false,
            CheckedFinalTime = false,
            Validated = true
        });
    }

    /// <summary>
    /// Supply elapsed 60 Hz frames for all on-line gameplay, including menus/dialogue/battles.
    /// Actual save-write suspension discards only that interval and preserves running intent.
    /// No offline duration is accepted or inferred.
    /// </summary>
    public static TrainerTowerProgressState AdvanceElapsedFrames(TrainerTowerProgressState state, long elapsedFrames, bool saveWriteInProgress = false)
    {
        Validate(state);
        ArgumentOutOfRangeException.ThrowIfNegative(elapsedFrames);
        if (saveWriteInProgress || !state.TimerRunning || elapsedFrames == 0) return state;
        int frames = state.CurrentRecord.TimerFrames;
        int added = (int)Math.Min(elapsedFrames, MaximumTimeFrames - frames);
        // Saturate without adding first: even long.MaxValue cannot overflow.
        return ReplaceCurrent(state, state.CurrentRecord with { TimerFrames = frames + added });
    }

    /// <summary>
    /// Source field-resume callback: the scene/loss flags do not gate resumption. At the cap
    /// there is no increment to schedule; owner conversation permanently stops this run.
    /// </summary>
    public static TrainerTowerProgressState ResumeTimer(TrainerTowerProgressState state)
    {
        Validate(state);
        if (state.CurrentRecord.SpokeToOwner) return state;
        return state.CurrentRecord.TimerFrames < MaximumTimeFrames ? state with { TimerRunning = true } : state;
    }

    /// <summary>Source GET_TIME disables the live counter at the display limit.</summary>
    public static TrainerTowerProgressState GetCurrentTime(TrainerTowerProgressState state)
    {
        Validate(state);
        return state.CurrentRecord.TimerFrames == MaximumTimeFrames ? state with { TimerRunning = false } : state;
    }

    public static TrainerTowerTime FormatTime(int frames)
    {
        if ((uint)frames > MaximumTimeFrames) throw new ArgumentOutOfRangeException(nameof(frames));
        return new(frames / 3600, frames / 60 % 60, frames % 60 * 168 / 100);
    }

    /// <summary>
    /// Source GET_FLOOR_CLEARED predicate, including its behavior for any noncurrent floor.
    /// This determines defeated-floor triggers; it is not authorization to skip a floor.
    /// sourceFloorIndex is the catalog template's floorIdx, not its position in the course.
    /// </summary>
    public static bool GetFloorAlreadyCleared(TrainerTowerProgressState state, int floorNumber, int sourceFloorIndex = FloorCount)
    {
        Validate(state);
        if (floorNumber < 1 || floorNumber > FloorCount + 1) throw new ArgumentOutOfRangeException(nameof(floorNumber));
        if (sourceFloorIndex < 1 || sourceFloorIndex > FloorCount) throw new ArgumentOutOfRangeException(nameof(sourceFloorIndex));
        return floorNumber - 1 != state.CurrentRecord.FloorsCleared || floorNumber > sourceFloorIndex;
    }

    /// <summary>
    /// Begin the source floor script, using the authenticated course's floor format.
    /// Knockout opponents remain one uninterrupted script chain until all three have won.
    /// </summary>
    public static TrainerTowerProgressState BeginFloor(TrainerTowerProgressState state, int floorNumber, TrainerTowerBattleFormat format)
    {
        Validate(state);
        if (state.FloorChain is not null) throw new InvalidOperationException("A Trainer Tower floor script is already active.");
        if (!state.CurrentRecord.Validated || state.CurrentRecord.SpokeToOwner)
            throw new InvalidOperationException("Choose a Trainer Tower course before starting its floor script.");
        if (floorNumber < 1 || floorNumber > FloorCount || floorNumber != state.CurrentRecord.FloorsCleared + 1)
            throw new ArgumentOutOfRangeException(nameof(floorNumber), "Only the next uncleared floor can start.");
        ValidateFormat(state.SelectedCourse, format);
        return state with { FloorChain = new(floorNumber, format, 0) };
    }

    public static TrainerTowerProgressState BeginFloor(TrainerTowerProgressState state, TrainerTowerCatalog catalog, int floorNumber)
    {
        Validate(state, catalog);
        if (floorNumber < 1 || floorNumber > FloorCount) throw new ArgumentOutOfRangeException(nameof(floorNumber));
        return BeginFloor(state, floorNumber, catalog.Course(state.SelectedCourse).Floors[floorNumber - 1].Format);
    }

    /// <summary>
    /// Settle one won battle and advance its script chain. The source CLEARED_FLOOR command
    /// subsequently calls AddFloorCleared, after the final opponent's out-of-way movement.
    /// </summary>
    public static TrainerTowerProgressState WinBattle(TrainerTowerProgressState state)
    {
        Validate(state);
        var chain = state.FloorChain ?? throw new InvalidOperationException("There is no active Trainer Tower floor script.");
        if (chain.Format == TrainerTowerBattleFormat.Knockout && chain.TrainerIndex < 2)
            return state with { FloorChain = chain with { TrainerIndex = chain.TrainerIndex + 1 } };
        return state with { FloorChain = null };
    }

    /// <summary>Native CLEARED_FLOOR. Call once at the source script's actual clearing point.</summary>
    public static TrainerTowerProgressState AddFloorCleared(TrainerTowerProgressState state)
    {
        Validate(state);
        if (state.FloorChain is not null) throw new InvalidOperationException("Finish all Trainer Tower floor battles before clearing its floor.");
        if (state.CurrentRecord.FloorsCleared >= FloorCount) throw new InvalidOperationException("All Trainer Tower floors are already cleared.");
        return ReplaceCurrent(state, state.CurrentRecord with { FloorsCleared = state.CurrentRecord.FloorsCleared + 1 });
    }

    /// <summary>
    /// Loss and draw both set source hasLost and end the script chain, without clearing floor,
    /// record, or counter. Application heals and warps to lobby (9,7), with no money penalty.
    /// </summary>
    public static TrainerTowerProgressState LoseBattle(TrainerTowerProgressState state)
    {
        Validate(state);
        if (state.FloorChain is null) throw new InvalidOperationException("There is no active Trainer Tower floor script.");
        return SetPlayerLost(state with { FloorChain = null });
    }

    /// <summary>Native SET_LOST; kept separate for the source script dispatcher.</summary>
    public static TrainerTowerProgressState SetPlayerLost(TrainerTowerProgressState state)
    {
        Validate(state);
        return ReplaceCurrent(state, state.CurrentRecord with { HasLost = true });
    }

    /// <summary>The lobby consumes loss before unkA_4; its following script resets the scene gate.</summary>
    public static TrainerTowerStatusCheck ConsumeChallengeStatus(TrainerTowerProgressState state)
    {
        Validate(state);
        var record = state.CurrentRecord;
        if (record.HasLost) return new(ReplaceCurrent(state, record with { HasLost = false }), TrainerTowerChallengeStatus.Lost);
        if (record.UnknownStatus) return new(ReplaceCurrent(state, record with { UnknownStatus = false }), TrainerTowerChallengeStatus.Unknown);
        return new(state, TrainerTowerChallengeStatus.Normal);
    }

    /// <summary>Native GET_OWNER_STATE disables the counter before any owner or prize text.</summary>
    public static TrainerTowerOwnerCheck GetOwnerState(TrainerTowerProgressState state)
    {
        Validate(state);
        var record = state.CurrentRecord;
        int result = (record.SpokeToOwner ? 1 : 0) + (record.ReceivedPrize && record.CheckedFinalTime ? 1 : 0);
        return new(ReplaceCurrent(state with { TimerRunning = false }, record with { SpokeToOwner = true }), result);
    }

    /// <summary>
    /// Application passes the first floor's catalog prize and the actual atomic bag-add result.
    /// Check ReceivedPrize before attempting that add. A full bag never consumes the prize.
    /// </summary>
    public static TrainerTowerPrizeCheck GiveChallengePrize(TrainerTowerProgressState state, string sourceFirstFloorPrizeItem, bool itemAdded)
    {
        Validate(state);
        if (string.IsNullOrWhiteSpace(sourceFirstFloorPrizeItem)) throw new ArgumentException("A source first-floor prize is required.", nameof(sourceFirstFloorPrizeItem));
        if (state.CurrentRecord.ReceivedPrize) return new(state, TrainerTowerPrizeResult.AlreadyReceived, sourceFirstFloorPrizeItem);
        if (!itemAdded) return new(state, TrainerTowerPrizeResult.BagFull, sourceFirstFloorPrizeItem);
        return new(ReplaceCurrent(state, state.CurrentRecord with { ReceivedPrize = true }), TrainerTowerPrizeResult.Received, sourceFirstFloorPrizeItem);
    }

    public static TrainerTowerPrizeCheck GiveChallengePrize(TrainerTowerProgressState state, TrainerTowerCatalog catalog, bool itemAdded)
    {
        Validate(state, catalog);
        var firstFloor = catalog.Course(state.SelectedCourse).Floors[0];
        return GiveChallengePrize(state, catalog.Template(firstFloor.TemplateSymbol).PrizeItem, itemAdded);
    }

    /// <summary>Native CHECK_FINAL_TIME: only strictly lower frames replace a record, including zero.</summary>
    public static TrainerTowerTimeCheck CheckFinalTime(TrainerTowerProgressState state)
    {
        Validate(state);
        var record = state.CurrentRecord;
        var result = record.CheckedFinalTime ? TrainerTowerTimeResult.AlreadyChecked
            : record.BestTimeFrames > record.TimerFrames ? TrainerTowerTimeResult.NewRecord : TrainerTowerTimeResult.NotImproved;
        return new(ReplaceCurrent(state, record with
        {
            CheckedFinalTime = true,
            BestTimeFrames = result == TrainerTowerTimeResult.NewRecord ? record.TimerFrames : record.BestTimeFrames
        }), result);
    }

    /// <summary>Source ResetTrainerTowerResults resets best times only, not prize or challenge fields.</summary>
    public static TrainerTowerProgressState ResetBestTimes(TrainerTowerProgressState state)
    {
        Validate(state);
        return state with { Records = state.Records.Select(record => record with { BestTimeFrames = MaximumTimeFrames }).ToImmutableArray() };
    }

    public static void Validate(TrainerTowerProgressState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if ((uint)state.SelectedCourse >= CourseCount || state.Records.IsDefault || state.Records.Length != CourseCount)
            throw new InvalidDataException("Trainer Tower requires one selected course and exactly four source records.");
        foreach (var record in state.Records)
        {
            if (record is null || record.SetId != LocalSetId || (uint)record.TimerFrames > MaximumTimeFrames ||
                (uint)record.BestTimeFrames > MaximumTimeFrames || (uint)record.FloorsCleared > FloorCount)
                throw new InvalidDataException("Trainer Tower record is outside the local set 1 source bounds.");
        }
        if (state.CurrentRecord.SpokeToOwner && state.TimerRunning)
            throw new InvalidDataException("The Trainer Tower counter cannot run after speaking to its owner.");
        if (state.FloorChain is { } chain)
        {
            ValidateFormat(state.SelectedCourse, chain.Format);
            int maximumTrainerIndex = chain.Format == TrainerTowerBattleFormat.Knockout ? 2 : 0;
            if (chain.FloorNumber < 1 || chain.FloorNumber > FloorCount || chain.FloorNumber != state.CurrentRecord.FloorsCleared + 1 ||
                chain.TrainerIndex < 0 || chain.TrainerIndex > maximumTrainerIndex)
                throw new InvalidDataException("Trainer Tower floor script does not match the selected record.");
        }
    }

    public static void ValidateStableSave(TrainerTowerProgressState state)
    {
        Validate(state);
        if (state.FloorChain is not null) throw new InvalidDataException("Trainer Tower cannot be saved during a floor battle script or Knockout chain.");
    }

    /// <summary>Also validates the active Mixed floor against the authenticated source course.</summary>
    public static void Validate(TrainerTowerProgressState state, TrainerTowerCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        Validate(state);
        if (state.FloorChain is { } chain && catalog.Course(state.SelectedCourse).Floors[chain.FloorNumber - 1].Format != chain.Format)
            throw new InvalidDataException("Trainer Tower chain format differs from the source course floor.");
    }

    public static void ValidateStableSave(TrainerTowerProgressState state, TrainerTowerCatalog catalog)
    {
        Validate(state, catalog);
        ValidateStableSave(state);
    }

    private static TrainerTowerProgressState ReplaceCurrent(TrainerTowerProgressState state, TrainerTowerCourseRecord record) =>
        state with { Records = state.Records.SetItem((int)state.SelectedCourse, record) };

    private static void ValidateFormat(TrainerTowerChallengeKind course, TrainerTowerBattleFormat format)
    {
        if ((uint)format > (uint)TrainerTowerBattleFormat.Knockout ||
            course != TrainerTowerChallengeKind.Mixed && (int)course != (int)format)
            throw new InvalidDataException("Trainer Tower floor format is inconsistent with the selected course.");
    }
}
