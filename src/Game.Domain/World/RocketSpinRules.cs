using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Randomness;

namespace Game.Domain.World;

public sealed record RocketSpinIntent(CommandId CommandId, long ExpectedRevision, CommandId? CompletedPresentationId = null);
public sealed record RocketSpinVisit(int X, int Y, int Elevation, Direction Direction) : IComparable<RocketSpinVisit>
{
    // A record's hash code includes a process-specific type identity. Explicit value
    // ordering keeps transient command-state JSON stable across independent processes.
    public int CompareTo(RocketSpinVisit? other)
    {
        if (other is null) return 1;
        int c = X.CompareTo(other.X); if (c != 0) return c;
        c = Y.CompareTo(other.Y); if (c != 0) return c;
        c = Elevation.CompareTo(other.Elevation); return c != 0 ? c : Direction.CompareTo(other.Direction);
    }
}

/// <summary>Transient control/presentation state. Never serialize as a player-save payload.</summary>
public sealed record RocketSpinState(WorldSnapshot AwaitingWorld, CommandId PendingPresentationId,
    int LastArrowBehavior, int ForcedStepsCompleted, ImmutableSortedSet<RocketSpinVisit> Visited)
{
    public bool ControlsLocked => true;
    public bool CanSave => false;
}

public enum RocketSpinEventKind { Moved, Blocked, Stopped }
public sealed record RocketSpinAnimationFrame(int SourceFrame, int Duration, bool HorizontalFlip = false);
/// <summary>Source data, not a timing authority. Movement is eight 2-pixel source ticks; AnimCmd durations remain literal.</summary>
public sealed record RocketSpinPresentationCue(int SoundEffectId, int? MovementActionId, Direction Direction,
    int MovementTicks, int PixelsPerTick, ImmutableArray<RocketSpinAnimationFrame> AnimationFrames, int AnimationLoopCount);
public sealed record RocketSpinEvent(CommandId CausedByCommandId, RocketSpinEventKind Kind,
    TilePosition Before, TilePosition After, int StepsAdded, string Reason, RocketSpinPresentationCue? Presentation);
/// <summary>
/// Source completion eligibility. Trainer/on-frame/coordinate/warp scripts can preempt later callbacks.
/// TookStep counts GAME_STAT_STEPS; friendship advances only if its step-script stage is reached.
/// </summary>
public sealed record RocketSpinCallbacks(bool TookStep, bool AdvanceFriendship, bool AdvancePoison,
    bool AdvanceRepel, bool CheckStandardWildEncounter)
{
    public static readonly RocketSpinCallbacks None = new(false, false, false, false, false);
}
public sealed record RocketSpinTransition(TransitionStatus Status, WorldSnapshot State, RocketSpinState? ActiveSpin,
    ImmutableArray<RocketSpinEvent> Events, RocketSpinCallbacks Callbacks, Diagnostic? Diagnostic)
{
    public bool ControlsLocked => ActiveSpin is not null;
    public bool CanSave => ActiveSpin is null;
}
/// <summary>Other active actors only; the caller excludes the player. Previous cells remain occupied while actors move.</summary>
public sealed record RocketSpinActor(MapId MapId, int X, int Y, int PreviousX, int PreviousY, int Elevation, bool Active = true);

/// <summary>
/// Bounded Rocket Hideout arrow/ordinary-floor/stop lifecycle, with one movement per correlated completion.
/// No RNG, scripts, counters outside WorldSnapshot, map connections, surfing, ledges, or object pushing.
/// </summary>
public static class RocketSpinRules
{
    public const string SourceCommit = ConnectedFieldRules.SourceCommit;
    public const int RightArrow = 0x54, LeftArrow = 0x55, UpArrow = 0x56, DownArrow = 0x57, StopTile = 0x58;
    public const int SpinSoundEffectId = 153; // SE_M_RAZOR_WIND2, include/constants/songs.h:158.
    public const int MaximumForcedSteps = 4096;

    public static bool IsArrow(int behavior) => behavior is >= RightArrow and <= DownArrow;
    public static bool IsSpinTile(int behavior) => behavior is >= RightArrow and <= StopTile;
    public static bool CanSave(RocketSpinState? activeSpin) => activeSpin is null;
    public static bool ControlsLocked(RocketSpinState? activeSpin) => activeSpin is not null;

    /// <summary>
    /// Existing source friendship callbacks can advance the RNG at the 128-step boundary.
    /// Rebind that externally computed stream only; callbacks cannot move the player or consume a revision here.
    /// This helper neither draws RNG nor proves the caller executed the correct friendship rules.
    /// </summary>
    public static RocketSpinState ReconcileCallbackRng(RocketSpinState activeSpin, WorldSnapshot world)
    {
        ArgumentNullException.ThrowIfNull(activeSpin);
        ArgumentNullException.ThrowIfNull(world);
        if (activeSpin.AwaitingWorld is null || world.Rng.AlgorithmId != Gen3Rng.AlgorithmId ||
            world.Rng.Cursor < activeSpin.AwaitingWorld.Rng.Cursor ||
            (world with { Rng = activeSpin.AwaitingWorld.Rng }) != activeSpin.AwaitingWorld)
            throw new ArgumentException("Only the existing callback's forward RNG state may differ from the awaiting world.", nameof(world));
        return activeSpin with { AwaitingWorld = world };
    }

    /// <summary>Use after completing the ordinary presentation that landed on an arrow, not from raw directional input.</summary>
    public static RocketSpinTransition Start(WorldSnapshot world, RocketSpinIntent intent,
        IReadOnlyDictionary<MapId, ConnectedFieldMap> maps, RocketSpinState? activeSpin = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(maps);
        if (activeSpin is not null) return Fail(world, activeSpin, "spin-already-active", "Complete the outstanding presentation.", TransitionStatus.Rejected);
        if (intent.CompletedPresentationId is not null)
            return Fail(world, null, "unexpected-spin-completion", "Starting spin has no forced presentation to acknowledge.", TransitionStatus.Rejected);
        var problem = Validate(world, intent, maps);
        if (problem is not null) return Fail(world, null, problem.Value.Code, problem.Value.Message, problem.Value.Status);
        var behavior = maps[world.Player.MapId].CellAt(world.Player.X, world.Player.Y)!.SourceBehavior;
        if (!IsArrow(behavior)) return Fail(world, null, "spin-arrow-required", "Only source arrow tiles start this lifecycle.", TransitionStatus.Rejected);
        return MoveOne(world, intent.CommandId, maps[world.Player.MapId], behavior, 0, ImmutableSortedSet<RocketSpinVisit>.Empty, null);
    }

    /// <summary>
    /// Call once for the exact pending cue. A stop arrival stays locked until this acknowledgment.
    /// A stale/duplicate/wrong acknowledgment preserves all state and never moves a second tile.
    /// </summary>
    public static RocketSpinTransition CompletePresentation(WorldSnapshot world, RocketSpinState activeSpin,
        RocketSpinIntent intent, IReadOnlyDictionary<MapId, ConnectedFieldMap> maps)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(activeSpin);
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(maps);
        if (!StableId.IsValid(activeSpin.PendingPresentationId.Value) || !IsArrow(activeSpin.LastArrowBehavior) ||
            activeSpin.ForcedStepsCompleted is <= 0 or > MaximumForcedSteps || activeSpin.Visited is null ||
            activeSpin.Visited.Count != activeSpin.ForcedStepsCompleted || activeSpin.Visited.Any(v =>
                v is null || !Enum.IsDefined(v.Direction) || v.Elevation is < 0 or > 15 || v.X < 0 || v.Y < 0))
            return Fail(world, activeSpin, "invalid-spin-state", "The transient source movement state is malformed.");
        if (world != activeSpin.AwaitingWorld)
            return Fail(world, activeSpin, "spin-world-changed", "The world changed while the forced movement presentation was pending.", TransitionStatus.Rejected);
        if (intent.CompletedPresentationId != activeSpin.PendingPresentationId || intent.CommandId == activeSpin.PendingPresentationId)
            return Fail(world, activeSpin, "spin-presentation-mismatch", "A new command must acknowledge the exact pending presentation.", TransitionStatus.Rejected);
        var problem = Validate(world, intent, maps);
        if (problem is not null) return Fail(world, activeSpin, problem.Value.Code, problem.Value.Message, problem.Value.Status);
        var map = maps[world.Player.MapId];
        var behavior = map.CellAt(world.Player.X, world.Player.Y)!.SourceBehavior;
        if (behavior == StopTile)
        {
            var next = world with { Revision = world.Revision + 1 };
            return new(TransitionStatus.Accepted, next, null,
                [new(intent.CommandId, RocketSpinEventKind.Stopped, world.Player, world.Player, 0, "stop-tile-completed", null)],
                RocketSpinCallbacks.None, null);
        }
        // TryUpdatePlayerSpinDirection changes lastSpinTile only on another arrow.
        return MoveOne(world, intent.CommandId, map, IsArrow(behavior) ? behavior : activeSpin.LastArrowBehavior,
            activeSpin.ForcedStepsCompleted, activeSpin.Visited, activeSpin);
    }

    /// <summary>FieldGetPlayerInput and TryStartStepCountScript, before player_step clears the forced flag.</summary>
    public static RocketSpinCallbacks CompletionCallbacks(int sourceBehavior, bool forcedFlag, bool moved)
    {
        if (!moved || IsArrow(sourceBehavior)) return RocketSpinCallbacks.None;
        return new(true, true, !forcedFlag, !forcedFlag, true);
    }

    public static bool ActorOccupies(RocketSpinActor actor, TilePosition player, int targetX, int targetY)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(player);
        if (actor.Elevation is < 0 or > 15 || player.Elevation is < 0 or > 15)
            throw new ArgumentOutOfRangeException(nameof(actor), "Source object elevations are four-bit values.");
        return actor.Active && actor.MapId == player.MapId &&
            ((actor.X == targetX && actor.Y == targetY) || (actor.PreviousX == targetX && actor.PreviousY == targetY)) &&
            (actor.Elevation == 0 || player.Elevation == 0 || actor.Elevation == player.Elevation);
    }

    public static RocketSpinPresentationCue Presentation(Direction direction, bool moved = true)
    {
        if (!Enum.IsDefined(direction)) throw new ArgumentOutOfRangeException(nameof(direction));
        if (!moved) return new(SpinSoundEffectId, null, direction, 0, 0, [], 0);
        // object_event_anims.h:556–590, including the source's counterintuitive East/West first frames.
        ImmutableArray<RocketSpinAnimationFrame> frames = direction switch
        {
            Direction.South => [new(0,2), new(2,2,true), new(1,2), new(2,2)],
            Direction.North => [new(1,2), new(2,2), new(0,2), new(2,2,true)],
            Direction.West => [new(2,2,true), new(1,2), new(2,2), new(0,2)],
            _ => [new(2,2), new(0,2), new(2,2,true), new(1,2)]
        };
        int action = direction switch { Direction.South => 0x94, Direction.North => 0x95, Direction.West => 0x96, _ => 0x97 };
        return new(SpinSoundEffectId, action, direction, 8, 2, frames, 1);
    }

    private static RocketSpinTransition MoveOne(WorldSnapshot world, CommandId commandId, ConnectedFieldMap map,
        int arrow, int completed, ImmutableSortedSet<RocketSpinVisit> visited, RocketSpinState? previous)
    {
        Direction direction = arrow switch { RightArrow => Direction.East, LeftArrow => Direction.West, UpArrow => Direction.North, _ => Direction.South };
        var p = world.Player;
        var visit = new RocketSpinVisit(p.X, p.Y, p.Elevation, direction);
        if (visited.Contains(visit)) return Fail(world, previous, "spin-route-loop", "The same position, elevation, and forced direction recur.");
        if (completed >= MaximumForcedSteps) return Fail(world, previous, "spin-step-limit", "The bounded source movement budget is exhausted.");
        var (dx, dy) = direction switch { Direction.North => (0,-1), Direction.East => (1,0), Direction.South => (0,1), _ => (-1,0) };
        int x = p.X + dx, y = p.Y + dy;
        var here = map.CellAt(p.X, p.Y)!;
        var front = map.CellAt(x, y);
        RocketSpinTransition Blocked(string reason) => new(TransitionStatus.Accepted,
            world with { Revision = world.Revision + 1 }, null,
            [new(commandId, RocketSpinEventKind.Blocked, p, p, 0, reason, Presentation(direction, false))], RocketSpinCallbacks.None, null);
        // Rocket Hideout has no map connections. Unknown connected topology is explicitly unsupported.
        if (x < 0 || y < 0 || x >= map.Width || y >= map.Height)
        {
            if (map.Connections.Any(c => c.Direction == direction)) return Fail(world, previous, "unsupported-spin-connection", "Spinning across a map connection is outside this profile.");
            return Blocked("map-boundary-collision");
        }
        if (front is null || !ConnectedFieldRules.ValidCell(front)) return Fail(world, previous, "invalid-spin-target-cell", "Target source cell is absent or malformed.");
        // Source ledge logic overrides ordinary collisions; never accidentally treat it as a wall or one-cell floor.
        if (front.SourceBehavior is >= 0x38 and <= 0x3b)
            return Fail(world, previous, "unsupported-spin-ledge", "The Hideout spin profile does not admit ledge jumps.");
        if (front.Collision != 0) return Blocked("collision");
        if (ConnectedFieldRules.BlocksEdge(here.SourceBehavior, direction) ||
            ConnectedFieldRules.BlocksEdge(front.SourceBehavior, ConnectedFieldRules.Opposite(direction))) return Blocked("directional-collision");
        if (p.Elevation != 0 && front.Elevation is not (0 or 15) && front.Elevation != p.Elevation) return Blocked("elevation");
        if (front.Occupied) return Blocked("object-collision");
        if (!SupportedGround(front.SourceBehavior)) return Fail(world, previous, "unsupported-spin-target-behavior", $"Source behavior 0x{front.SourceBehavior:x2} needs its own movement handler.");
        var callbacks = CompletionCallbacks(front.SourceBehavior, true, true);
        int steps = callbacks.TookStep ? 1 : 0;
        var position = new TilePosition(map.Id, x, y, front.Elevation == 15 ? p.Elevation : front.Elevation, direction);
        var next = world with { Revision = world.Revision + 1, StepCount = world.StepCount + steps, Player = position };
        var spin = new RocketSpinState(next, commandId, arrow, completed + 1, visited.Add(visit));
        return new(TransitionStatus.Accepted, next, spin,
            [new(commandId, RocketSpinEventKind.Moved, p, position, steps, "forced-spin-tile-completed", Presentation(direction))], callbacks, null);
    }

    private static bool SupportedGround(int behavior) => behavior == 0 || behavior is >= 0x30 and <= 0x37 || IsSpinTile(behavior);

    private static (string Code, string Message, TransitionStatus Status)? Validate(WorldSnapshot world,
        RocketSpinIntent intent, IReadOnlyDictionary<MapId, ConnectedFieldMap> maps)
    {
        if (world.RulesVersion != ConnectedFieldRules.RulesVersion || world.Player is null ||
            !StableId.IsValid(world.Player.MapId.Value) || !Enum.IsDefined(world.Player.Facing) ||
            world.Player.Elevation is < 0 or > 15 || world.Revision is < 0 or long.MaxValue ||
            world.StepCount is < 0 or long.MaxValue || world.Rng.AlgorithmId != Gen3Rng.AlgorithmId)
            return ("invalid-spin-world", "A valid connected-field world snapshot is required.", TransitionStatus.Fault);
        if (!StableId.IsValid(intent.CommandId.Value)) return ("invalid-spin-command", "A stable command ID is required.", TransitionStatus.Rejected);
        if (intent.ExpectedRevision != world.Revision) return ("stale-revision", "Expected revision does not match.", TransitionStatus.Rejected);
        if (!maps.TryGetValue(world.Player.MapId, out var map) || map is null)
            return ("spin-map-missing", world.Player.MapId.Value, TransitionStatus.Fault);
        var diagnostic = ConnectedFieldRules.ValidateMap(map, world.Player.MapId);
        if (diagnostic is not null) return (diagnostic.Code, diagnostic.Message, TransitionStatus.Fault);
        var cell = map.CellAt(world.Player.X, world.Player.Y);
        if (cell is null || !ConnectedFieldRules.ValidCell(cell) || cell.Collision != 0 || cell.Occupied ||
            (cell.Elevation != 15 && cell.Elevation != world.Player.Elevation) || !SupportedGround(cell.SourceBehavior))
            return ("invalid-spin-current-cell", "Current source cell must be loaded, free, supported, and elevation-consistent.", TransitionStatus.Fault);
        return null;
    }

    private static RocketSpinTransition Fail(WorldSnapshot world, RocketSpinState? spin, string code, string message,
        TransitionStatus status = TransitionStatus.Fault) => new(status, world, spin, [], RocketSpinCallbacks.None, new(code, message));
}
