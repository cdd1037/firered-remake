using System.Collections.Immutable;
using Game.Domain.Ids;

namespace Game.Domain.World;

public sealed record SilphWarpPad(MapId SourceMapId,int WarpIndex,int X,int Y,int Elevation,
    MapId DestinationMapId,int DestinationWarpId);
public sealed record SilphDoorCell(int X,int Y,int SourceMetatileId,int Collision);
public sealed record SilphDoorDefinition(MapId MapId,string ScriptId,int DoorIndex,string Flag,
    string ClosedScriptId,string OpenScriptId,ImmutableArray<SilphDoorCell> ClosedCells,ImmutableArray<SilphDoorCell> OpenCells);
public sealed record CampaignSilphElevatorFloor(int MenuIndex,string Label,int SourceFloorValue,MapId DestinationMapId,
    int X,int Y,int Elevation,int DestinationWarpId);
public sealed record CampaignSilphElevatorEntrance(MapId SourceMapId,int SourceWarpId,int ElevatorWarpId,
    int ReturnX,int ReturnY,int ReturnElevation);
public sealed record CampaignSilphDynamicExit(int WarpIndex,int X,int Y,int Elevation,int SourceWarpId);
public sealed record SilphPadTransition(TransitionStatus Status,WorldSnapshot State,TilePosition? SteppedOntoPad,Diagnostic? Diagnostic);

/// <summary>Source 0x67 pads warp after one completed ordinary walking step. Arrival is not another step.</summary>
public static class SilphFieldRules
{
    public const int PadBehavior=0x67;
    public const string CardKeyFlag="FLAG_HIDE_SILPH_CO_5F_CARD_KEY";
    public const string ElevatorMap="map.silph-co-elevator";
    public static bool IsSilphMap(MapId id)=>id.Value.StartsWith("map.silph-co-",StringComparison.Ordinal);

    public static (int Scroll,int Row) ElevatorMenuPosition(int sourceFloor)
    {
        if(sourceFloor is <4 or >14)throw new ArgumentOutOfRangeException(nameof(sourceFloor));
        int index=14-sourceFloor,scroll=Math.Clamp(index-4,0,5);
        return(scroll,index-scroll);
    }

    // field_specials.c sElevatorAnimationDuration and Task_ElevatorShake (three frames per shake).
    public static int ElevatorShakeFrames(int from,int to)
    {
        if(from is <4 or >14||to is <4 or >14)throw new ArgumentOutOfRangeException(nameof(from));
        int distance=Math.Min(Math.Abs(from-to),8);
        return distance==0?0:3*new[]{8,16,24,32,38,46,53,56,57}[distance];
    }

    public static SilphPadTransition StepOntoPad(WorldSnapshot state,MoveIntent intent,
        IReadOnlyDictionary<MapId,IFieldMap> maps,IReadOnlyDictionary<MapId,ConnectedFieldMap> walkingMaps,
        ImmutableArray<SilphWarpPad> pads)
    {
        SilphPadTransition Fail(string code)=>new(TransitionStatus.Fault,state,null,new(code,code));
        if(intent.ExpectedRevision!=state.Revision||!Enum.IsDefined(intent.Direction))
            return new(TransitionStatus.Rejected,state,null,new("invalid_field_command","Revision or direction is invalid."));
        if(state.RulesVersion!=RuleVersions.GridWorldV0)return Fail("silph_pad_world_profile");
        var p=state.Player;
        if(!maps.TryGetValue(p.MapId,out var source)||!walkingMaps.TryGetValue(p.MapId,out var walking))return Fail("silph_pad_source_missing");
        var(dx,dy)=intent.Direction switch{Direction.North=>(0,-1),Direction.South=>(0,1),Direction.East=>(1,0),_=>(-1,0)};
        int x=p.X+dx,y=p.Y+dy;
        var pad=pads.SingleOrDefault(w=>w.SourceMapId==p.MapId&&w.X==x&&w.Y==y);
        if(pad is null||source.SourceBehaviorAt(x,y)!=PadBehavior||!source.FieldWarps.Any(w=>
            w.Index==pad.WarpIndex&&w.X==x&&w.Y==y&&w.Elevation==pad.Elevation&&w.DestinationMapId==pad.DestinationMapId&&w.DestinationWarpId==pad.DestinationWarpId))
            return Fail("silph_pad_source_warp");
        var cells=walking.Cells.Select(c=>c.SourceBehavior==PadBehavior?c with{SourceBehavior=0}:c).ToImmutableArray();
        var ordinary=ConnectedFieldRules.Complete(state with{RulesVersion=ConnectedFieldRules.RulesVersion},
            new(intent.CommandId,intent.ExpectedRevision,ConnectedFieldAction.Walk,intent.Direction),
            new Dictionary<MapId,ConnectedFieldMap>{{p.MapId,walking with{Cells=cells}}},true);
        if(ordinary.Status!=TransitionStatus.Accepted)return new(ordinary.Status,state,null,ordinary.Diagnostic);
        var stepped=ordinary.State with{RulesVersion=state.RulesVersion};
        if(stepped.Player.X==p.X&&stepped.Player.Y==p.Y)return new(TransitionStatus.Accepted,stepped,null,null);
        if(stepped.Player.X!=x||stepped.Player.Y!=y||pad.Elevation!=0&&pad.Elevation!=stepped.Player.Elevation)return Fail("silph_pad_step_mismatch");
        if(!maps.TryGetValue(pad.DestinationMapId,out var destination))return Fail("silph_pad_destination_missing");
        var arrival=destination.FieldWarps.SingleOrDefault(w=>w.Index==pad.DestinationWarpId);
        if(arrival is null||destination.SourceBehaviorAt(arrival.X,arrival.Y)!=PadBehavior||
            !pads.Any(w=>w.SourceMapId==destination.Id&&w.WarpIndex==arrival.Index&&w.X==arrival.X&&w.Y==arrival.Y))return Fail("silph_pad_destination_warp");
        var tile=destination.GetTile(arrival.X,arrival.Y);
        if(tile is null||tile.Blocked||arrival.Elevation!=0&&arrival.Elevation!=tile.Elevation)return Fail("silph_pad_destination_cell");
        var position=new TilePosition(destination.Id,arrival.X,arrival.Y,tile.Elevation,intent.Direction);
        return new(TransitionStatus.Accepted,stepped with{Player=position},stepped.Player,null);
    }
}
