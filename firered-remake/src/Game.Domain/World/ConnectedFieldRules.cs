using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Randomness;

namespace Game.Domain.World;

/// <summary>Already classified avatar action, not a physical key press or an animation frame.</summary>
public enum ConnectedFieldAction { Idle, TurnInPlace, Walk }
public enum ConnectedFieldEventKind { Idle, Turned, Blocked, Walked, JumpedLedge }
public sealed record ConnectedFieldIntent(CommandId CommandId, long ExpectedRevision, ConnectedFieldAction Action, Direction Direction);
/// <summary>Raw source values. Occupied is a caller-resolved, elevation-compatible object collision.</summary>
public sealed record ConnectedFieldCell(int SourceBehavior, int Collision, int Elevation, bool Occupied = false);
public sealed record ConnectedFieldConnection(Direction Direction, int Offset, MapId DestinationMapId);
/// <summary>Local zero-based cells; connections preserve source order. This profile supports one per side.</summary>
public sealed record ConnectedFieldMap(MapId Id, int Width, int Height, ImmutableArray<ConnectedFieldCell> Cells,
    ImmutableArray<ConnectedFieldConnection> Connections)
{
    public ConnectedFieldCell? CellAt(int x, int y) => x < 0 || y < 0 || x >= Width || y >= Height ||
        Cells.IsDefault || (long)y * Width + x >= Cells.Length ? null : Cells[y * Width + x];
}
public sealed record ConnectedFieldEvent(CommandId CausedByCommandId, ConnectedFieldEventKind Kind,
    TilePosition Before, TilePosition After, int StepsAdded, string Reason);
public sealed record ConnectedFieldTransition(TransitionStatus Status, WorldSnapshot State,
    ImmutableArray<ConnectedFieldEvent> Events, bool MapChanged, bool RequiresEncounterCheck, Diagnostic? Diagnostic);

/// <summary>
/// Bounded, completed ordinary walking/turning/ledge actions. No input adapter, door dispatch, scripts,
/// encounter probability, RNG draws, object motion, camera, or presentation is executed here.
/// </summary>
public static class ConnectedFieldRules
{
    public const string RulesVersion = "fr-connected-field-v1";
    public const string SourceCommit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";
    // Upstream grid coordinates are s16 and include the seven-cell map border.
    public const int MaximumMapDimension = short.MaxValue - 7;

    public static ConnectedFieldTransition Complete(WorldSnapshot state, ConnectedFieldIntent intent,
        IReadOnlyDictionary<MapId, ConnectedFieldMap> maps,bool enableCaveGround=false,bool enableSand=false,bool biking=false,
        bool enableSpinTiles=false,RocketSpinState? activeSpin=null,bool enableCardinalLedges=false)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(maps);
        ConnectedFieldTransition Fail(string code, string message, TransitionStatus status = TransitionStatus.Fault) =>
            new(status, state, [], false, false, new(code, message));
        if (activeSpin is not null)
            return Fail("spin-controls-locked", "Ordinary player actions are unavailable until spin presentation completes.", TransitionStatus.Rejected);
        if (state.RulesVersion != RulesVersion)
            return Fail("unsupported-connected-field-profile", RulesVersion);
        if (state.Player is null || !StableId.IsValid(state.Player.MapId.Value) || !Enum.IsDefined(state.Player.Facing) ||
            state.Player.Elevation is < 0 or > 15 || state.Revision is < 0 or long.MaxValue ||
            state.StepCount is < 0 or long.MaxValue || state.Rng.AlgorithmId != Gen3Rng.AlgorithmId)
            return Fail("invalid-connected-field-state", "Position, counters, or RNG identity is invalid.");
        if (!StableId.IsValid(intent.CommandId.Value) || !Enum.IsDefined(intent.Action) || !Enum.IsDefined(intent.Direction))
            return Fail("invalid-connected-field-command", "A stable ID, action, and cardinal direction are required.", TransitionStatus.Rejected);
        if (intent.ExpectedRevision != state.Revision)
            return Fail("stale-revision", "Expected revision does not match.", TransitionStatus.Rejected);
        if (!maps.TryGetValue(state.Player.MapId, out var map) || map is null)
            return Fail("connected-map-missing", state.Player.MapId.Value);
        var validation = ValidateMap(map, state.Player.MapId);
        if (validation is not null) return Fail(validation.Code, validation.Message);
        var here = map.CellAt(state.Player.X, state.Player.Y);
        if (here is null || !ValidCell(here) || here.Collision != 0 || here.Occupied ||
            (here.Elevation != 15 && here.Elevation != state.Player.Elevation))
            return Fail("invalid-connected-current-cell", "Current cell is absent, blocked, malformed, or has inconsistent elevation.");
        if (!WalkableBehavior(here.SourceBehavior,enableCaveGround,enableSand,enableSpinTiles))
            return Fail(BehaviorFault(here.SourceBehavior), $"Current source behavior 0x{here.SourceBehavior:x2}.");

        ConnectedFieldTransition Accepted(ConnectedFieldEventKind kind, TilePosition position, int steps, string reason)
        {
            var next = state with { Revision = state.Revision + 1, StepCount = state.StepCount + steps, Player = position };
            return new(TransitionStatus.Accepted, next, [new(intent.CommandId, kind, state.Player, position, steps, reason)],
                position.MapId != state.Player.MapId, (kind is ConnectedFieldEventKind.Walked or ConnectedFieldEventKind.Turned or ConnectedFieldEventKind.JumpedLedge) &&
                (!enableSpinTiles || !RocketSpinRules.IsArrow(maps[position.MapId].CellAt(position.X,position.Y)!.SourceBehavior)), null);
        }
        ConnectedFieldTransition Blocked(string reason) => Accepted(ConnectedFieldEventKind.Blocked,
            state.Player with { Facing = intent.Direction }, 0, reason);
        if (intent.Action == ConnectedFieldAction.Idle)
            return Accepted(ConnectedFieldEventKind.Idle, state.Player, 0, "idle");
        if (intent.Action == ConnectedFieldAction.TurnInPlace)
        {
            if (intent.Direction == state.Player.Facing)
                return Fail("invalid-turn-action", "A completed turn must change facing.", TransitionStatus.Rejected);
            return Accepted(ConnectedFieldEventKind.Turned, state.Player with { Facing = intent.Direction }, 0, "turn-completed");
        }

        var (dx, dy) = intent.Direction switch
        {
            Direction.North => (0, -1), Direction.East => (1, 0), Direction.South => (0, 1), _ => (-1, 0)
        };
        int x = state.Player.X + dx, y = state.Player.Y + dy;
        var destination = map;
        bool crosses = x < 0 || y < 0 || x >= map.Width || y >= map.Height;
        if (crosses)
        {
            var connection = map.Connections.FirstOrDefault(c => c.Direction == intent.Direction);
            if (connection is null)
                return Fail("unloaded-field-exit", "No supported connection is defined for this edge.");
            if (!maps.TryGetValue(connection.DestinationMapId, out destination) || destination is null)
                return Fail("connection-destination-missing", connection.DestinationMapId.Value);
            validation = ValidateMap(destination, connection.DestinationMapId);
            if (validation is not null) return Fail(validation.Code, validation.Message);
            // SetPositionFromConnection, followed by CameraMove's final x/y increment. Never clamp an aperture.
            long tx = intent.Direction switch
            {
                Direction.North or Direction.South => (long)x - connection.Offset,
                Direction.West => destination.Width - 1, _ => 0
            };
            long ty = intent.Direction switch
            {
                Direction.East or Direction.West => (long)y - connection.Offset,
                Direction.North => destination.Height - 1, _ => 0
            };
            if (tx < 0 || ty < 0 || tx >= destination.Width || ty >= destination.Height)
                return Fail("connection-aperture-outside", "Translated coordinate is outside the destination map.");
            x = (int)tx; y = (int)ty;
        }
        var front = destination.CellAt(x, y);
        if (front is null || !ValidCell(front)) return Fail("invalid-connected-target-cell", "Target cell is absent or malformed.");
        // Fixed FireRed GetBikeCollisionAt turns running-disallowed tiles into
        // an ordinary collision before movement; this is not a walkable-tile admission.
        if (biking && front.SourceBehavior == 0x0a) return Blocked("bicycle-terrain-collision");
        if (front.SourceBehavior == 0x69 && intent.Direction == Direction.North && state.Player.Facing == Direction.North)
            return Fail("field-warp-handler-required", "Dispatch the north-facing front door before ordinary walking.");
        if (!WalkableBehavior(front.SourceBehavior,enableCaveGround,enableSand,enableSpinTiles) && front.SourceBehavior is not (0x3b or 0x69 or 0x80 or 0x84 or 0x87 or 0x88 or 0x8f) &&
            !(enableCardinalLedges && front.SourceBehavior is >=0x38 and <=0x3a))
            return Fail(BehaviorFault(front.SourceBehavior), $"Target source behavior 0x{front.SourceBehavior:x2}.");

        bool matchingLedge=(front.SourceBehavior,intent.Direction) switch
        {
            (0x3b,Direction.South)=>true,
            (0x38,Direction.East) or (0x39,Direction.West) or (0x3a,Direction.North)=>enableCardinalLedges,
            _=>false
        };
        if (matchingLedge)
        {
            // Source ShouldJumpLedge overrides the intermediate tile's ordinary collision/elevation result.
            // GetLedgeJumpDirection uses the matching cardinal metatile predicate. Preserve the old
            // south-only admission unless the caller selects the later route profile explicitly.
            if (crosses) return Fail("unsupported-ledge-connection", "A ledge cannot straddle maps in this bounded profile.");
            int landingX=x+dx,landingY=y+dy;
            var landing = map.CellAt(landingX, landingY);
            if (landing is null || !ValidCell(landing) || !WalkableBehavior(landing.SourceBehavior,enableCaveGround,enableSand) ||
                landing.Collision != 0 || landing.Occupied || landing.Elevation == 15)
                return Fail("unsupported-ledge-landing", "Expected a loaded, free ordinary landing cell.");
            return Accepted(ConnectedFieldEventKind.JumpedLedge,
                new(map.Id, landingX, landingY, landing.Elevation, intent.Direction), 1,
                intent.Direction.ToString().ToLowerInvariant()+"-ledge-completed");
        }
        if (front.Collision != 0) return Blocked("collision");
        if (BlocksEdge(here.SourceBehavior, intent.Direction) || BlocksEdge(front.SourceBehavior, Opposite(intent.Direction)))
            return Blocked("directional-collision");
        if (state.Player.Elevation != 0 && front.Elevation is not (0 or 15) && front.Elevation != state.Player.Elevation)
            return Blocked("elevation");
        if (front.Occupied) return Blocked("object-collision");
        // A zero-collision ledge/sign/door would need a separately audited action; do not quietly call it floor.
        if (!WalkableBehavior(front.SourceBehavior,enableCaveGround,enableSand,enableSpinTiles))
            return Fail("unsupported-passable-special-cell", $"Source behavior 0x{front.SourceBehavior:x2} is not an ordinary landing.");
        int elevation = front.Elevation == 15 ? state.Player.Elevation : front.Elevation;
        return Accepted(ConnectedFieldEventKind.Walked, new(destination.Id, x, y, elevation, intent.Direction), enableSpinTiles && RocketSpinRules.IsArrow(front.SourceBehavior) ? 0 : 1,
            crosses ? "connection-walk-completed" : "walk-completed");
    }

    internal static Diagnostic? ValidateMap(ConnectedFieldMap map, MapId expected)
    {
        if (map.Id != expected || !StableId.IsValid(map.Id.Value) || map.Width is <= 0 or > MaximumMapDimension ||
            map.Height is <= 0 or > MaximumMapDimension || map.Cells.IsDefault || (long)map.Width * map.Height != map.Cells.Length ||
            map.Connections.IsDefault)
            return new("invalid-connected-map", "Map identity, dimensions, cells, or connections are invalid.");
        var directions = new HashSet<Direction>();
        foreach (var c in map.Connections)
            if (c is null || !Enum.IsDefined(c.Direction) || !StableId.IsValid(c.DestinationMapId.Value) ||
                c.DestinationMapId == map.Id || !directions.Add(c.Direction))
                return new("unsupported-connection-definition", "Only one valid non-self connection per cardinal side is supported.");
        return null;
    }
    internal static bool ValidCell(ConnectedFieldCell cell) => cell.SourceBehavior is >= 0 and <= 255 &&
        cell.Collision is >= 0 and <= 3 && cell.Elevation is >= 0 and <= 15;
    private static bool WalkableBehavior(int behavior,bool enableCaveGround=false,bool enableSand=false,bool enableSpinTiles=false) => behavior is 0x00 or 0x02 or >= 0x30 and <= 0x37 || enableCaveGround&&behavior is 0x08 or 0x2a or 0x2b || enableSand&&behavior==0x21 || enableSpinTiles&&behavior is >=0x54 and <=0x58;
    private static string BehaviorFault(int behavior) => behavior switch
    {
        0x10 or 0x11 or 0x12 or 0x13 or 0x15 or 0x16 or 0x17 or 0x19 or 0x1a or 0x1b or >= 0x50 and <= 0x53 => "unsupported-water-movement",
        >= 0x60 and <= 0x71 => "field-warp-handler-required",
        _ => "unsupported-source-behavior"
    };
    internal static Direction Opposite(Direction direction) => direction switch
    {
        Direction.North => Direction.South, Direction.South => Direction.North,
        Direction.East => Direction.West, _ => Direction.East
    };
    internal static bool BlocksEdge(int behavior, Direction edge) => edge switch
    {
        Direction.East => behavior is 0x30 or 0x34 or 0x36,
        Direction.West => behavior is 0x31 or 0x35 or 0x37,
        Direction.North => behavior is 0x32 or 0x34 or 0x35,
        _ => behavior is 0x33 or 0x36 or 0x37
    };
}
