using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Randomness;
namespace Game.Domain.World;

/// <summary>Pinned source map rows; warp-table rows on ordinary ground are arrival markers, not fall triggers.</summary>
public static class DottedHoleSource
{
    public const string Commit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";
    public const int FallBehavior = 0x66;
    public static readonly MapId RuinValley = new("map.six-island-ruin-valley");
    public static readonly MapId FirstFloor = new("map.six-island-dotted-hole-1f");
    public static readonly MapId SapphireRoom = new("map.six-island-dotted-hole-sapphire-room");
    public sealed record MapContract(int Width, int Height, ImmutableArray<FieldWarp> Warps, ImmutableArray<int> WarpBehaviors);
    public static ImmutableDictionary<MapId, MapContract> Maps { get; } = new Dictionary<MapId, MapContract>
    {
        [new("map.six-island-dotted-hole-1f")] = new(16, 11, [
            new(0,6,9,3,new("map.six-island-ruin-valley"),0),
            new(1,6,5,3,new("map.six-island-dotted-hole-b1f"),0),
            new(2,13,5,3,new("map.six-island-dotted-hole-sapphire-room"),0),
            new(3,6,7,3,new("map.six-island-dotted-hole-b1f"),2),
        ], [0x65,0x66,0x61,0x8]),
        [new("map.six-island-dotted-hole-b1f")] = new(13, 11, [
            new(0,6,7,3,new("map.six-island-dotted-hole-1f"),1),
            new(1,6,1,3,new("map.six-island-dotted-hole-b2f"),0),
            new(2,11,5,3,new("map.six-island-dotted-hole-1f"),3),
            new(3,6,9,3,new("map.six-island-dotted-hole-1f"),3),
            new(4,1,5,3,new("map.six-island-dotted-hole-1f"),3),
        ], [0x8,0x66,0x66,0x66,0x66]),
        [new("map.six-island-dotted-hole-b2f")] = new(13, 11, [
            new(0,6,7,3,new("map.six-island-dotted-hole-b1f"),1),
            new(1,6,1,3,new("map.six-island-dotted-hole-1f"),3),
            new(2,11,5,3,new("map.six-island-dotted-hole-1f"),3),
            new(3,6,9,3,new("map.six-island-dotted-hole-1f"),3),
            new(4,1,5,3,new("map.six-island-dotted-hole-b3f"),0),
        ], [0x8,0x66,0x66,0x66,0x66]),
        [new("map.six-island-dotted-hole-b3f")] = new(13, 11, [
            new(0,6,7,3,new("map.six-island-dotted-hole-b2f"),4),
            new(1,6,1,3,new("map.six-island-dotted-hole-1f"),3),
            new(2,11,5,3,new("map.six-island-dotted-hole-b4f"),0),
            new(3,6,9,3,new("map.six-island-dotted-hole-1f"),3),
            new(4,1,5,3,new("map.six-island-dotted-hole-1f"),3),
        ], [0x8,0x66,0x66,0x66,0x66]),
        [new("map.six-island-dotted-hole-b4f")] = new(13, 11, [
            new(0,6,7,3,new("map.six-island-dotted-hole-b3f"),2),
            new(1,6,1,3,new("map.six-island-dotted-hole-1f"),3),
            new(2,11,5,3,new("map.six-island-dotted-hole-1f"),3),
            new(3,6,9,3,new("map.six-island-dotted-hole-sapphire-room"),1),
            new(4,1,5,3,new("map.six-island-dotted-hole-1f"),3),
        ], [0x8,0x66,0x66,0x66,0x66]),
        [new("map.six-island-dotted-hole-sapphire-room")] = new(16, 15, [
            new(0,11,7,3,new("map.six-island-dotted-hole-1f"),2),
            new(1,5,9,3,new("map.six-island-dotted-hole-b4f"),3),
        ], [0x61,0x8]),
    }.ToImmutableDictionary();

    public static bool ValidateMap(IFieldMap map) => Maps.TryGetValue(map.Id, out var expected) &&
        map.Width == expected.Width && map.Height == expected.Height && !map.FieldWarps.IsDefault &&
        map.FieldWarps.SequenceEqual(expected.Warps) && expected.Warps.All(w =>
            map.GetTile(w.X,w.Y) is { Blocked:false, Elevation:3 } &&
            map.SourceBehaviorAt(w.X,w.Y) == expected.WarpBehaviors[w.Index]);

    internal static Diagnostic? ValidateWorld(WorldSnapshot world, FourIslandFieldIntent intent, IFieldMap map)
    {
        if (world.Player is null || !StableId.IsValid(world.RulesVersion) || !StableId.IsValid(world.Player.MapId.Value) ||
            !Enum.IsDefined(world.Player.Facing) || world.Revision is <0 or >=long.MaxValue-4 ||
            world.StepCount is <0 or >=long.MaxValue-1 || world.Rng.AlgorithmId != Gen3Rng.AlgorithmId)
            return new("invalid-dotted-hole-world", "A valid bounded field world is required.");
        if (!StableId.IsValid(intent.CommandId.Value) || intent.ExpectedRevision != world.Revision)
            return new("stale-or-invalid-dotted-hole-command", "Command ID or revision does not match.");
        if (map.Id != world.Player.MapId || map.GetTile(world.Player.X,world.Player.Y) is not { Blocked:false } cell ||
            cell.Elevation is <0 or >15 || world.Player.Elevation is <0 or >15 ||
            (cell.Elevation != 15 && cell.Elevation != world.Player.Elevation))
            return new("invalid-dotted-hole-position", "Current source cell is inconsistent.");
        return null;
    }
}
