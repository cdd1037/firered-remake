using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.World;

namespace Game.Application;

public sealed partial class CampaignSession
{
    private static bool SixIslandMountainMap(MapId id)=>id.Value is "map.six-island-water-path" or SixIslandRuinMap;
    private IReadOnlyDictionary<MapId,ConnectedFieldMap> SixIslandWalkingMaps(IReadOnlyDictionary<MapId,ConnectedFieldMap> maps)
    {
        return new CampaignMapView<ConnectedFieldMap>(maps,(id,map)=>SixIslandMountainMap(id)?map with{Cells=map.Cells.Select(c=>
            c.Collision==0&&c.SourceBehavior==0x0c?c with{SourceBehavior=0}:c).ToImmutableArray()}:map);
    }
    private IReadOnlyDictionary<MapId,IFieldMap> SixIslandMovementMaps(IReadOnlyDictionary<MapId,IFieldMap> maps,CampaignState state)
    {
        return new CampaignMapView<IFieldMap>(maps,(id,map)=>SixIslandMountainMap(id)?new SixIslandMovementMap(map,
            id.Value==SixIslandRuinMap?SixIslandDoorEdits(state.Script.Flags.GetValueOrDefault(RuinValleyCutRules.PermanentFlag)):
                id==state.World.Player.MapId?state.MapEdits:null,_content):map);
    }
    private sealed class SixIslandMovementMap(IFieldMap source,ImmutableSortedDictionary<string,FieldMetatileEdit>? edits,ICampaignCatalog content):IFieldMap
    {
        public MapId Id=>source.Id;public int Width=>source.Width;public int Height=>source.Height;public ImmutableArray<FieldWarp> FieldWarps=>source.FieldWarps;
        private int? Raw(int x,int y)=>edits?.TryGetValue(x+":"+y,out var edit)==true?content.MetatileDefinition(Id,edit.SourceMetatileId)!.Behavior:source.SourceBehaviorAt(x,y);
        public int? SourceEncounterTypeAt(int x,int y)=>edits?.TryGetValue(x+":"+y,out var edit)==true?content.MetatileDefinition(Id,edit.SourceMetatileId)!.EncounterType:source.SourceEncounterTypeAt(x,y);
        public int? SourceBehaviorAt(int x,int y)=>Raw(x,y)==0x0c&&GetTile(x,y)?.Blocked==false?0:Raw(x,y);
        public TileCell? GetTile(int x,int y)
        {
            var tile=source.GetTile(x,y);
            if(edits?.TryGetValue(x+":"+y,out var edit)==true)tile=new(edit.Collision!=0,edit.Elevation,tile?.Behavior??TileBehavior.Unsupported);
            return tile is {Blocked:false}&&Raw(x,y)==0x0c?tile with{Behavior=TileBehavior.Floor}:tile;
        }
    }
}
