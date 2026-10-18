using System.Collections.Immutable;
using Game.Domain;
using Game.Domain.Ids;
using Game.Domain.World;

namespace Game.Application;

public sealed partial class CampaignSession
{
    private IReadOnlyDictionary<MapId,IFieldMap> RocketMovementMaps(CampaignState state)
    {
        if(state.MapEdits is not {Count:>0}||!RocketMap(state.World.Player.MapId))return SeafoamRawMaps(state);
        var maps=SeafoamRawMaps(state);var id=state.World.Player.MapId;
        return ReplaceMap(maps,id,(IFieldMap)new RocketEditedMap(maps[id],state.MapEdits,_content));
    }
    private sealed class RocketEditedMap(IFieldMap original,ImmutableSortedDictionary<string,FieldMetatileEdit> edits,ICampaignCatalog content):IFieldMap
    {
        public MapId Id=>original.Id;public int Width=>original.Width;public int Height=>original.Height;
        public ImmutableArray<FieldWarp> FieldWarps=>original.FieldWarps;
        public int? SourceBehaviorAt(int x,int y)=>edits.TryGetValue(x+":"+y,out var e)?content.MetatileDefinition(Id,e.SourceMetatileId)!.Behavior:original.SourceBehaviorAt(x,y);
        public int? SourceEncounterTypeAt(int x,int y)=>edits.ContainsKey(x+":"+y)?0:original.SourceEncounterTypeAt(x,y);
        public TileCell? GetTile(int x,int y)
        {
            if(!edits.TryGetValue(x+":"+y,out var e))return original.GetTile(x,y);
            int b=SourceBehaviorAt(x,y)!.Value;
            return new(e.Collision!=0,e.Elevation,b is >=0x62 and <=0x65?TileBehavior.ArrowWarp:b is >=0x6c and <=0x6f?TileBehavior.StairWarp:b==0?TileBehavior.Floor:TileBehavior.Unsupported);
        }
    }
    private CampaignState RecordRocketElevatorEntrance(CampaignState previous,CampaignState moved)
    {
        if(moved.World.Player.MapId.Value!=RocketElevatorMap||previous.World.Player.MapId==moved.World.Player.MapId)return moved;
        var p=previous.World.Player;var(dx,dy)=Delta(p.Facing);
        var entrance=_content.RocketHideoutElevatorEntrances.SingleOrDefault(e=>e.SourceMapId==p.MapId&&e.ReturnX==p.X+dx&&e.ReturnY==p.Y+dy)??throw new InvalidDataException("rocket_elevator_entry_warp");
        var floor=_content.RocketHideoutElevatorFloors.Single(f=>f.DestinationMapId==p.MapId);
        return moved with{RocketElevator=new(p.MapId,entrance.SourceWarpId,entrance.ReturnX,entrance.ReturnY,entrance.ReturnElevation,floor.SourceFloorValue)};
    }
    private Decision<CampaignState>? TryRocketDynamicExit(CampaignState state,CampaignCommand command)
    {
        if(command.Kind!="move"||state.World.Player.MapId.Value!=RocketElevatorMap)return null;
        var p=state.World.Player;
        if(command.Direction!=Direction.South||p.Facing!=Direction.South||_content.Maps[p.MapId].SourceBehaviorAt(p.X,p.Y)!=0x65)return null;
        if(!_content.RocketHideoutDynamicExits.Any(e=>e.X==p.X&&e.Y==p.Y&&e.SourceWarpId==127)||state.RocketElevator is not {} target)return new(CommandStatus.Fault,state,"rocket_dynamic_destination_missing");
        var map=_content.Maps[target.DestinationMapId];int x=target.X,y=target.Y;
        if(target.DestinationWarpId!=255)
        {
            var w=map.FieldWarps.SingleOrDefault(w=>w.Index==target.DestinationWarpId)??throw new InvalidDataException("rocket_dynamic_source_warp");
            x=w.X;y=w.Y;
        }
        int behavior=map.SourceBehaviorAt(x,y)??throw new InvalidDataException("rocket_dynamic_arrival_missing");
        if(behavior==0x69)y++;
        var tile=map.GetTile(x,y)??throw new InvalidDataException("rocket_dynamic_arrival_cell");
        if(tile.Blocked)throw new InvalidDataException("rocket_dynamic_arrival_blocked");
        var moved=state with{World=state.World with{Revision=checked(state.World.Revision+1),Player=new(map.Id,x,y,tile.Elevation,Direction.South)},Exploration=ResetEncounterModifiers(state.Exploration!)};
        return new(CommandStatus.Accepted,Pump(EnterMap(moved,Direction.South)));
    }
}
