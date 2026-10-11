using System.Collections.Immutable;
using Game.Domain.Encounters;
using Game.Domain.Ids;

namespace Game.Domain.World;

/// <summary>
/// Explicit Safari ground admission for source mountain tops (0x0c, elevation4).
/// Original collision/elevation, source maps and water boundaries remain authoritative.
/// This adapter is used only for movement; raw maps remain the source for encounters and presentation.
/// </summary>
public static class SafariFieldMovementRules
{
    public const string SourceCommit = EarlyLandEncounterRules.SourceCommit;
    public const int MountainTopBehavior = 0x0c;
    public const int RockStairsBehavior = 0x2a;
    public static bool IsSafariArea(MapId id) => id.Value is "map.safari-zone-center" or "map.safari-zone-east" or "map.safari-zone-north" or "map.safari-zone-west";

    public static ConnectedFieldMap ForConnectedMovement(ConnectedFieldMap map, string rulesVersion)
    {
        CheckProfile(rulesVersion); ArgumentNullException.ThrowIfNull(map);
        if (!IsSafariArea(map.Id)) return map;
        CheckDimensions(map.Id, map.Width, map.Height);
        if (map.Cells.IsDefault || map.Cells.Length != map.Width * map.Height) throw new ArgumentException("Safari ground grid differs.");
        return map with { Cells = map.Cells.Select(cell =>
        {
            CheckMountain(map.Id, cell.SourceBehavior, cell.Elevation, cell.Collision == 0);
            return cell.SourceBehavior == MountainTopBehavior ? cell with { SourceBehavior = 0 } : cell;
        }).ToImmutableArray() };
    }

    public static IFieldMap ForFieldMovement(IFieldMap map, string rulesVersion)
    {
        CheckProfile(rulesVersion); ArgumentNullException.ThrowIfNull(map);
        if (!IsSafariArea(map.Id)) return map;
        CheckDimensions(map.Id, map.Width, map.Height);
        return new MountainGroundMap(map);
    }

    private static void CheckProfile(string rulesVersion)
    {
        if (rulesVersion != EarlyLandEncounterRules.SafariRulesVersion)
            throw new NotSupportedException("Safari terrain requires the Safari profile.");
    }
    private static void CheckDimensions(MapId map, int width, int height)
    {
        var expected = map.Value switch
        {
            "map.safari-zone-center" => (51,36), "map.safari-zone-east" => (54,35),
            "map.safari-zone-north" => (57,40), "map.safari-zone-west" => (48,36),
            _ => throw new NotSupportedException("Unknown Safari area.")
        };
        if ((width,height) != expected) throw new ArgumentException("Safari source map dimensions differ.");
    }
    private static void CheckMountain(MapId map, int? behavior, int elevation, bool free)
    {
        if (behavior == MountainTopBehavior && (map.Value == "map.safari-zone-center" || elevation != 4 || !free))
            throw new ArgumentException("Safari mountain top source terrain differs.");
    }
    private sealed class MountainGroundMap(IFieldMap source) : IFieldMap
    {
        public MapId Id => source.Id;
        public int Width => source.Width;
        public int Height => source.Height;
        public ImmutableArray<FieldWarp> FieldWarps => source.FieldWarps;
        public int? SourceEncounterTypeAt(int x,int y) => source.SourceEncounterTypeAt(x,y);
        public int? SourceBehaviorAt(int x,int y)
        {
            var behavior = source.SourceBehaviorAt(x,y); var cell = source.GetTile(x,y);
            if (cell is not null) CheckMountain(Id, behavior, cell.Elevation, !cell.Blocked);
            return behavior == MountainTopBehavior ? 0 : behavior;
        }
        public TileCell? GetTile(int x,int y)
        {
            var cell = source.GetTile(x,y);
            if (cell is null) return null;
            var behavior = source.SourceBehaviorAt(x,y); CheckMountain(Id, behavior, cell.Elevation, !cell.Blocked);
            return behavior == MountainTopBehavior ? cell with { Behavior = TileBehavior.Floor } : cell;
        }
    }
}
