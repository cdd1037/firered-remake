using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Randomness;

namespace Game.Domain.World;

/// <summary>Eligibility before trainer/frame/coordinate/warp scripts preempt later stages. No RNG is drawn here.</summary>
public sealed record FourIslandFieldCallbacks(bool TookStep, bool AdvanceFriendship, bool AdvancePoison,
    bool AdvanceRepel, bool CheckStandardWildEncounter)
{
    public static readonly FourIslandFieldCallbacks None = new(false, false, false, false, false);
}
public sealed record FourIslandFieldIntent(CommandId CommandId, long ExpectedRevision, CommandId? CompletedPresentationId = null);
public enum FourIslandMovementKind { IceSlip, WaterfallAscent, WaterfallDescent }
public enum FourIslandMovementPhase { ShowPokemon, Moving }
public sealed record FourIslandMovementState(WorldSnapshot AwaitingWorld, CommandId PendingPresentationId,
    FourIslandMovementKind Kind, FourIslandMovementPhase Phase, Direction MovementDirection, int CompletedTiles,
    long? MoveOwnerInstanceId = null)
{
    public bool ControlsLocked => true;
    public bool PreventStep => Kind == FourIslandMovementKind.WaterfallAscent;
    public bool CanSave => false;
}
public sealed record FourIslandMovementCue(string SourceAction, Direction Direction, ImmutableArray<int> PixelsPerUpdate,
    long? MoveOwnerInstanceId = null)
{
    public int MovementUpdates => PixelsPerUpdate.Length;
}
public enum FourIslandFieldEventKind { ShowPokemon, Moved, Blocked, Stopped, Cancelled, Fell }
public sealed record FourIslandFieldEvent(CommandId CausedByCommandId, FourIslandFieldEventKind Kind,
    TilePosition Before, TilePosition After, string Reason, FourIslandMovementCue? Presentation = null);
public sealed record FourIslandFieldTransition(TransitionStatus Status, WorldSnapshot State, FourIslandMovementState? ActiveMovement,
    ImmutableArray<FourIslandFieldEvent> Events, FourIslandFieldCallbacks Callbacks, Diagnostic? Diagnostic)
{
    public bool ControlsLocked => ActiveMovement is not null;
    public bool CanSave => ActiveMovement is null;
}

/// <summary>Shared bounded source predicates. The IFieldMap must retain raw source behaviors and source collision/elevation.</summary>
public static class FourIslandFieldSource
{
    public const string SourceCommit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";
    public static readonly MapId FirstFloor = new("map.four-island-icefall-cave-1f");
    public static readonly MapId Basement = new("map.four-island-icefall-cave-b1f");
    public static readonly MapId Entrance = new("map.four-island-icefall-cave-entrance");
    public const int Ice = 0x23, ThinIce = 0x26, CrackedIce = 0x27, Waterfall = 0x13;
    public static bool IsForcedTile(int behavior) => behavior is Ice or Waterfall;
    public static FourIslandFieldCallbacks CompletionCallbacks(int behavior, bool moved, bool forcedFlag, bool fieldControlsLocked = false) =>
        !moved || fieldControlsLocked || IsForcedTile(behavior) ? FourIslandFieldCallbacks.None : new(true, true, !forcedFlag, !forcedFlag, true);

    internal static Diagnostic? Validate(WorldSnapshot world, FourIslandFieldIntent intent, IFieldMap map)
    {
        if (world.Player is null || !StableId.IsValid(world.RulesVersion) || !StableId.IsValid(world.Player.MapId.Value) ||
            !Enum.IsDefined(world.Player.Facing) || world.Revision is < 0 or long.MaxValue || world.StepCount is < 0 or long.MaxValue ||
            world.Player.Elevation is < 0 or > 15 || world.Rng.AlgorithmId != Gen3Rng.AlgorithmId)
            return new("invalid-four-island-world", "A valid field world is required.");
        if (!StableId.IsValid(intent.CommandId.Value)) return new("invalid-four-island-command", "A stable command ID is required.");
        if (intent.ExpectedRevision != world.Revision) return new("stale-revision", "Expected revision does not match.");
        if (map.Id != world.Player.MapId || map.Width <= 0 || map.Height <= 0 || map.Width > ConnectedFieldRules.MaximumMapDimension || map.Height > ConnectedFieldRules.MaximumMapDimension)
            return new("invalid-four-island-map", "Map identity or dimensions do not match.");
        var here = map.GetTile(world.Player.X, world.Player.Y);
        if (here is null || here.Blocked || here.Elevation is < 0 or > 15 ||
            (here.Elevation != 15 && here.Elevation != world.Player.Elevation) || map.SourceBehaviorAt(world.Player.X, world.Player.Y) is not (>= 0 and <= 255))
            return new("invalid-four-island-current-cell", "Current source cell is absent, blocked or inconsistent.");
        return null;
    }
    internal static (int X, int Y) Delta(Direction d) => d switch
    { Direction.North => (0, -1), Direction.South => (0, 1), Direction.East => (1, 0), _ => (-1, 0) };
    internal static string? Collision(IFieldMap map, TilePosition p, int x, int y, Direction direction, Func<int, int, int, bool>? occupied)
    {
        var t = map.GetTile(x, y);
        if (t is null || t.Blocked) return "collision";
        if (ConnectedFieldRules.BlocksEdge(map.SourceBehaviorAt(p.X, p.Y) ?? 255, direction) ||
            ConnectedFieldRules.BlocksEdge(map.SourceBehaviorAt(x, y) ?? 255, ConnectedFieldRules.Opposite(direction))) return "directional-collision";
        if (p.Elevation != 0 && t.Elevation is not (0 or 15) && p.Elevation != t.Elevation) return "elevation";
        if (occupied?.Invoke(x, y, t.Elevation == 15 ? p.Elevation : t.Elevation) == true) return "object-collision";
        return null;
    }
}
