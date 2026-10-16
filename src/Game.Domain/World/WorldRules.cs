using Game.Domain.Ids;
using Game.Domain.Randomness;

namespace Game.Domain.World;

public static class WorldRules
{
    public static WorldTransition Move(WorldSnapshot state, MoveIntent intent, IWorldMap map)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(intent); ArgumentNullException.ThrowIfNull(map);
        WorldTransition Fail(TransitionStatus status, string code, string message) => new(status, state, [], new(code, message));
        var invariant = ValidateState(state, map);
        if (invariant is not null) return Fail(TransitionStatus.Fault, invariant.Code, invariant.Message);
        if (!StableId.IsValid(intent.CommandId.Value) || !Enum.IsDefined(intent.Direction))
            return Fail(TransitionStatus.Rejected, "invalid-move-intent", "Command ID and cardinal direction are required.");
        if (intent.ExpectedRevision != state.Revision)
            return Fail(TransitionStatus.Rejected, "stale-revision", "Expected revision does not match.");
        var (dx, dy) = intent.Direction switch
        {
            Direction.North => (0, -1), Direction.East => (1, 0), Direction.South => (0, 1), Direction.West => (-1, 0),
            _ => throw new InvalidOperationException("Validated direction vanished.")
        };
        int x = state.Player.X + dx, y = state.Player.Y + dy;
        bool outside = x < 0 || y < 0 || x >= map.Width || y >= map.Height;
        TileCell? cell = outside ? null : map.GetTile(x, y);
        if (!outside && cell is null) return Fail(TransitionStatus.Fault, "missing-cell", "Map is missing an in-bounds cell.");
        // Blocked signposts have ordinary collision/facing; their interaction script is a separate command.
        // Other unsupported movement modes fault before facing, step count, or RNG changes.
        if (cell is not null && cell.Behavior is not (TileBehavior.Floor or TileBehavior.ArrowWarp or TileBehavior.StairWarp) && !(cell.Behavior is TileBehavior.Sign or TileBehavior.Warp && cell.Blocked))
            return Fail(TransitionStatus.Fault, "unsupported-tile-behavior", $"Tile behavior {cell.Behavior} is not enabled in fr-grid-v0.");
        string reason = outside ? "map-boundary" : cell!.Blocked ? "collision" :
            cell.Elevation != state.Player.Elevation ? "elevation" : "walk";
        bool moved = reason == "walk";
        var position = state.Player with { Facing = intent.Direction, X = moved ? x : state.Player.X, Y = moved ? y : state.Player.Y };
        var next = state with { Revision = state.Revision + 1, StepCount = state.StepCount + (moved ? 1 : 0), Player = position };
        // Blocked directional input is accepted: facing changes, but position/step count/RNG do not.
        return new(TransitionStatus.Accepted, next,
            [new(intent.CommandId, moved ? WorldEventKind.Moved : WorldEventKind.Blocked, state.Player, position, reason)], null);
    }
    /// <summary>Pure semantic validation for load/restore boundaries; does not move or consume RNG.</summary>
    public static Diagnostic? ValidateState(WorldSnapshot state, IWorldMap map)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(map);
        if (state.RulesVersion != RuleVersions.GridWorldV0)
            return new("unsupported-world-profile", "Only fr-grid-v0 movement is implemented.");
        if (state.Player is null || !StableId.IsValid(state.Player.MapId.Value) || state.Player.MapId != map.Id ||
            map.Width <= 0 || map.Height <= 0 || state.Player.X < 0 || state.Player.X >= map.Width ||
            state.Player.Y < 0 || state.Player.Y >= map.Height || !Enum.IsDefined(state.Player.Facing) ||
            state.Revision < 0 || state.Revision == long.MaxValue || state.StepCount < 0 || state.StepCount == long.MaxValue ||
            state.Rng.AlgorithmId != Gen3Rng.AlgorithmId)
            return new("invalid-world-state", "World position, map, or counters are invalid.");
        var current = map.GetTile(state.Player.X, state.Player.Y);
        if (current is null || current.Blocked || current.Elevation != state.Player.Elevation)
            return new("invalid-current-cell", "Current tile is missing, blocked, or has mismatched elevation.");
        if (current.Behavior is not (TileBehavior.Floor or TileBehavior.ArrowWarp or TileBehavior.StairWarp))
            return new("unsupported-current-tile-behavior", "Current tile behavior is outside fr-grid-v0.");
        return null;
    }

}
