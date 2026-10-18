using System.Collections.Immutable;
using Game.Domain.Ids;
namespace Game.Domain.World;
public sealed record FieldWarp(int Index,int X,int Y,int Elevation,MapId DestinationMapId,int DestinationWarpId);
public interface IFieldMap:IWorldMap
{
    int? SourceBehaviorAt(int x,int y);
    int? SourceEncounterTypeAt(int x,int y)=>SourceBehaviorAt(x,y)==2?1:0;
    ImmutableArray<FieldWarp> FieldWarps {get;}
}
public sealed record FieldMovementTransition(TransitionStatus Status,WorldSnapshot State,ImmutableArray<WorldEvent> Events,bool MapChanged,Diagnostic? Diagnostic)
{ public FieldTransitionReceipt? Presentation { get; init; } }
/// <summary>Bounded door, arrow and directional stair rules. Entry scripts are applied by Application.</summary>
public static class FieldMovementRules
{
    public static FieldMovementTransition Move(WorldSnapshot state,MoveIntent intent,IReadOnlyDictionary<MapId,IFieldMap> maps,bool enableCaveDoors=false,bool enableLadders=false,bool enableSand=false,Func<IFieldMap,IFieldMap>? destinationLayout=null)
    {
        FieldMovementTransition Fail(string code,string message)=>new(TransitionStatus.Fault,state,[],false,new(code,message));
        if(!maps.TryGetValue(state.Player.MapId,out var map))return Fail("map_missing",state.Player.MapId.Value);
        var validation=WorldRules.ValidateState(state,enableLadders||enableSand?new LadderGroundMap(map,enableLadders,enableSand):map);
        if(validation is not null&&!(enableCaveDoors&&validation.Code=="unsupported-current-tile-behavior"&&map.GetTile(state.Player.X,state.Player.Y)?.Behavior==TileBehavior.Grass&&map.SourceBehaviorAt(state.Player.X,state.Player.Y)==0x02))return Fail(validation.Code,validation.Message);
        if(intent.ExpectedRevision!=state.Revision||!Enum.IsDefined(intent.Direction))return new(TransitionStatus.Rejected,state,[],false,new("invalid_field_command","Revision or direction is invalid."));
        static Direction? DirectionFor(int? behavior)=>behavior switch{0x62=>Direction.East,0x63=>Direction.West,0x64=>Direction.North,0x65=>Direction.South,0x6c or 0x6e=>Direction.East,0x6d or 0x6f=>Direction.West,_=>null};
        var here=map.SourceBehaviorAt(state.Player.X,state.Player.Y);
        var (dx,dy)=intent.Direction switch{Direction.North=>(0,-1),Direction.South=>(0,1),Direction.East=>(1,0),_=>(-1,0)};
        var frontX=state.Player.X+dx;var frontY=state.Player.Y+dy;
        FieldWarp? warp=null;bool caveStep=false;
        if(enableCaveDoors&&(map.SourceBehaviorAt(frontX,frontY)==0x60||enableLadders&&map.SourceBehaviorAt(frontX,frontY)==0x61))
        {
            var entrance=map.GetTile(frontX,frontY);
            // Source IsElevationMismatchAt treats actor elevation 0 and map
            // elevations 0/15 as compatible. Open League doors retain map 0.
            if(entrance is {Blocked:false}&&(entrance.Elevation is 0 or 15||state.Player.Elevation==0||entrance.Elevation==state.Player.Elevation))
            {
                // Non-animated doors warp after completing the actual step onto the tile.
                // PlayerGetElevation reads previousElevation, retained across source elevations 0/15.
                int warpElevation=entrance.Elevation is 0 or 15?state.Player.Elevation:entrance.Elevation;
                warp=map.FieldWarps.FirstOrDefault(w=>w.X==frontX&&w.Y==frontY&&(w.Elevation==0||w.Elevation==warpElevation));
                caveStep=warp is not null;
            }
        }
        if(warp is null&&state.Player.Facing==intent.Direction)
        {
            if(DirectionFor(here)==intent.Direction)warp=map.FieldWarps.FirstOrDefault(w=>w.X==state.Player.X&&w.Y==state.Player.Y&&(w.Elevation==0||w.Elevation==state.Player.Elevation));
            else if(intent.Direction==Direction.North&&map.SourceBehaviorAt(frontX,frontY)==0x69)
                warp=map.FieldWarps.FirstOrDefault(w=>w.X==frontX&&w.Y==frontY&&(w.Elevation==0||w.Elevation==state.Player.Elevation));
        }
        if(warp is not null)
        {
            if(!maps.TryGetValue(warp.DestinationMapId,out var destination))return Fail("warp_destination_missing",warp.DestinationMapId.Value);
            // Layout selection precedes source arrival-tile/facing resolution.
            // Application supplies a pure projection of the destination's entry script.
            if (destinationLayout is not null) destination = destinationLayout(destination);
            if (destination is null || destination.Id != warp.DestinationMapId) return Fail("warp_destination_layout_invalid", warp.DestinationMapId.Value);
            if(warp.DestinationWarpId<0||warp.DestinationWarpId>=destination.FieldWarps.Length)return Fail("warp_index_invalid",warp.DestinationWarpId.ToString());
            var arrival=destination.FieldWarps[warp.DestinationWarpId];int x=arrival.X,y=arrival.Y;var behavior=destination.SourceBehaviorAt(x,y);var facing=caveStep?intent.Direction:state.Player.Facing;
            // From source initial avatar transition and door/stair exit tasks. Sprite offsets are presentation-only.
            facing=behavior switch{0x62=>Direction.West,0x63=>Direction.East,0x64=>Direction.South,0x65=>Direction.North,0x6c or 0x6e=>Direction.West,0x6d or 0x6f=>Direction.East,0x69=>Direction.South,0x60 when enableCaveDoors=>Direction.South,0x00=>Direction.South,_=>facing};
            if(behavior==0x69||behavior==0x60&&enableCaveDoors)y++;
            else if(!(enableLadders&&behavior==0x61)&&behavior is not (0x00 or 0x62 or 0x63 or 0x64 or 0x65 or 0x6c or 0x6d or 0x6e or 0x6f))return Fail("warp_arrival_unsupported",behavior.ToString()??"missing");
            var tile=destination.GetTile(x,y);
            if(tile is null||tile.Blocked)return Fail("warp_arrival_blocked",$"{destination.Id}:{x},{y}");
            var position=new TilePosition(destination.Id,x,y,tile.Elevation,facing);
            var cause=caveStep?(map.SourceBehaviorAt(frontX,frontY)==0x60?FieldTransitionCause.NonAnimatedDoor:FieldTransitionCause.PlainWarp):here is >=0x6c and <=0x6f?FieldTransitionCause.StairWarp:warp.X==frontX&&warp.Y==frontY&&map.SourceBehaviorAt(frontX,frontY)==0x69&&intent.Direction==Direction.North?FieldTransitionCause.AnimatedDoor:FieldTransitionCause.PlainWarp;
            var arrivalKind=behavior switch{0x69=>FieldTransitionArrival.AnimatedDoor,0x60=>FieldTransitionArrival.NonAnimatedDoor,>=0x6c and <=0x6f=>FieldTransitionArrival.Stair,_=>FieldTransitionArrival.Plain};
            var receipt=new FieldTransitionReceipt(cause,caveStep?FieldTransitionDeparture.StepOntoWarp:FieldTransitionDeparture.Immediate,arrivalKind,state.Player,position,new(destination.Id,arrival.X,arrival.Y,arrival.Elevation,facing));
            var next=state with{Revision=state.Revision+1,Player=position,StepCount=checked(state.StepCount+(caveStep?1:0))};
            // Warp/door automatic arrival is an event transition, not a normal player step or RNG draw.
            return new(TransitionStatus.Accepted,next,[new(intent.CommandId,WorldEventKind.Moved,state.Player,position,caveStep?"cave-step-warp":"warp")],true,null){Presentation=receipt};
        }
        if(here is >=0x6c and <=0x6f && DirectionFor(here)==intent.Direction)
        {
            var next=state with{Revision=state.Revision+1,Player=state.Player with{Facing=intent.Direction}};
            return new(TransitionStatus.Accepted,next,[new(intent.CommandId,WorldEventKind.Blocked,state.Player,next.Player,"stair-turn")],false,null);
        }
        if(enableLadders&&(here is 0x08 or 0x2a or 0x2b or 0x61||map.SourceBehaviorAt(frontX,frontY) is 0x08 or 0x2a or 0x2b or 0x61)||
            enableSand&&(here is 0x17 or 0x21||map.SourceBehaviorAt(frontX,frontY) is 0x17 or 0x21))
        {
            var cells=ImmutableArray.CreateBuilder<ConnectedFieldCell>(map.Width*map.Height);
            for(int y=0;y<map.Height;y++)for(int x=0;x<map.Width;x++)
            {
                var tile=map.GetTile(x,y);int behavior=map.SourceBehaviorAt(x,y)??255;
                if(enableLadders&&behavior==0x61||behavior is >=0x62 and <=0x65 or >=0x6c and <=0x6f)behavior=0;
                cells.Add(new(behavior,tile?.Blocked==false?0:1,tile?.Elevation??0));
            }
            var ground=new ConnectedFieldMap(map.Id,map.Width,map.Height,cells.ToImmutable(),[]);
            var ordinary=ConnectedFieldRules.Complete(state with{RulesVersion=ConnectedFieldRules.RulesVersion},new(intent.CommandId,intent.ExpectedRevision,ConnectedFieldAction.Walk,intent.Direction),new Dictionary<MapId,ConnectedFieldMap>{{map.Id,ground}},enableLadders,enableSand);
            if(ordinary.Status!=TransitionStatus.Accepted)return new(ordinary.Status,state,[],false,ordinary.Diagnostic);
            var restored=ordinary.State with{RulesVersion=state.RulesVersion};bool moved=restored.Player.X!=state.Player.X||restored.Player.Y!=state.Player.Y;
            return new(TransitionStatus.Accepted,restored,[new(intent.CommandId,moved?WorldEventKind.Moved:WorldEventKind.Blocked,state.Player,restored.Player,ordinary.Events[0].Reason)],false,null);
        }
        var move=WorldRules.Move(state,intent,map);
        return new(move.Status,move.State,move.Events,false,move.Diagnostic);
    }
    // Ladder arrival occupies the source warp cell. Only the explicit cave profile
    // admits its ground/ladder terrain to the small grid transition helper.
    public static Diagnostic? ValidateLadderPosition(WorldSnapshot state,IFieldMap map)=>WorldRules.ValidateState(state,new LadderGroundMap(map));
    private sealed class LadderGroundMap(IFieldMap source,bool enableLadders=true,bool enableSand=false):IWorldMap
    {
        public MapId Id=>source.Id;public int Width=>source.Width;public int Height=>source.Height;
        public TileCell? GetTile(int x,int y)
        {
            var cell=source.GetTile(x,y);
            var behavior=source.SourceBehaviorAt(x,y);
            return cell is not null&&(cell.Blocked||behavior==0x00||enableLadders&&behavior is 0x08 or 0x2a or 0x2b or 0x32 or 0x61||enableSand&&behavior is 0x17 or 0x21)?cell with{Behavior=TileBehavior.Floor}:cell;
        }
    }

}
