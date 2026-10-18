using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.World;

namespace Game.Application;

public sealed partial class CampaignSession
{
    private static bool PostgameGroundMap(MapId map) => map.Value is SevenIslandCanyonMap or
        "map.seven-island-sevault-canyon-entrance" or "map.seven-island-tanoby-ruins" or "map.seven-island-trainer-tower" or "map.birth-island-exterior" or "map.two-island-cape-brink";
    private static bool PostgameGroundBehavior(MapId map, int? behavior) => (map.Value, behavior) is
        (SevenIslandCanyonMap, 0x0c) or ("map.seven-island-sevault-canyon-entrance", 0x0c) or
        ("map.seven-island-tanoby-ruins", 0x0c or 0x17) or ("map.seven-island-trainer-tower", 0x17) or
        ("map.birth-island-exterior", 0x0c) or ("map.two-island-cape-brink", 0x0c);
    private IReadOnlyDictionary<MapId, ConnectedFieldMap> PostgameWalkingMaps(IReadOnlyDictionary<MapId, ConnectedFieldMap> maps)
    {
        // Original cells remain authoritative for Surf, encounters, terrain and callbacks.
        return new CampaignMapView<ConnectedFieldMap>(maps,(id,map)=>PostgameGroundMap(id)||id.Value==TanobyKeyMap
            ? map with { Cells = map.Cells.Select(c => c.Collision != 0 ? c :
                PostgameGroundBehavior(id,c.SourceBehavior) ? c with { SourceBehavior = 0x00 } :
                id.Value == TanobyKeyMap && c.SourceBehavior == 0x20 ? c with { SourceBehavior = 0x08 } : c).ToImmutableArray() } : map);
    }
    private IReadOnlyDictionary<MapId, IFieldMap> PostgameMovementMaps(IReadOnlyDictionary<MapId, IFieldMap> maps)
    {
        return new CampaignMapView<IFieldMap>(maps,(id,map)=>PostgameGroundMap(id)?new PostgameMovementMap(map):map);
    }
    private sealed class PostgameMovementMap(IFieldMap source) : IFieldMap
    {
        public MapId Id => source.Id;
        public int Width => source.Width;
        public int Height => source.Height;
        public ImmutableArray<FieldWarp> FieldWarps => source.FieldWarps;
        public int? SourceEncounterTypeAt(int x, int y) => source.SourceEncounterTypeAt(x, y);
        public int? SourceBehaviorAt(int x, int y) => PostgameGroundBehavior(Id, source.SourceBehaviorAt(x, y)) &&
            source.GetTile(x, y)?.Blocked == false ? 0x00 : source.SourceBehaviorAt(x, y);
        public TileCell? GetTile(int x, int y)
        {
            var tile = source.GetTile(x, y);
            return tile is { Blocked: false } && PostgameGroundBehavior(Id, source.SourceBehaviorAt(x, y))
                ? tile with { Behavior = TileBehavior.Floor } : tile;
        }
    }
}
