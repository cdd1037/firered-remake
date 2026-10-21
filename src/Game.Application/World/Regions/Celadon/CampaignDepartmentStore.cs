using System.Text.Json.Serialization;
using Game.Domain;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.World;

namespace Game.Application;

/// <summary>The store's dynamic return is independent of the retained Rocket elevator return.</summary>
public sealed record CampaignDepartmentElevator(MapId DestinationMapId,int DestinationWarpId,int X,int Y,int Elevation,int Floor)
{
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingDefault)] public bool FloorWindowVisible{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public int? MenuDefault{get;init;}
}

public sealed partial class CampaignSession
{
    private const string DepartmentElevatorMap="map.celadon-city-department-store-elevator";
    private const string DepartmentElevatorSource="data/maps/CeladonCity_DepartmentStore_Elevator/scripts.inc";

    private int DepartmentValue(CampaignState state,string token)=>
        token.StartsWith("VAR_",StringComparison.Ordinal)?state.Script.Variables.GetValueOrDefault(token):
        int.TryParse(token,out int n)?n:_content.TryConstant(token,out n)?n:
        throw new InvalidDataException("department_elevator_operand_missing: "+token);

    private bool TryApplyDepartmentStoreEffect(CampaignState state,StoryEffect effect,out CampaignState next)
    {
        next=state;
        bool ownSource=effect.Source.Path==DepartmentElevatorSource;
        // The shared helper also serves Rocket Hideout. Claim it only in this exact map.
        bool sharedSource=state.World.Player.MapId.Value==DepartmentElevatorMap&&
            effect.Source.Path=="data/event_scripts.s"&&effect.Source.Symbol=="EventScript_GetElevatorFloor";
        if(!(ownSource||sharedSource)||effect.Opcode is not("special" or "specialvar" or "multichoicedefault" or "setdynamicwarp"))return false;
        if(state.World.Player.MapId.Value!=DepartmentElevatorMap||state.DepartmentElevator is not {} elevator||
            !state.Script.Locked||!ValidDepartmentDestination(elevator))
            throw new InvalidDataException("department_elevator_context");
        var a=effect.Args;int Value(string token)=>DepartmentValue(state,token);
        if(effect.Opcode=="setdynamicwarp")
        {
            if(!ownSource||a.Length!=4)throw new InvalidDataException("department_elevator_destination_operands");
            var floor=_content.DepartmentStoreElevatorFloors.SingleOrDefault(f=>
                _content.MapFromSourceSymbol(a[0])==f.DestinationMapId&&Value(a[1])==f.DestinationWarpId&&
                Value(a[2])==f.X&&Value(a[3])==f.Y)??throw new InvalidDataException("department_elevator_destination");
            if(floor.SourceFloorValue!=Value("VAR_0x8006"))throw new InvalidDataException("department_elevator_destination_floor");
            // Source sets the dynamic destination even for a same-floor selection, before its comparison.
            next=state with{DepartmentElevator=elevator with{DestinationMapId=floor.DestinationMapId,
                DestinationWarpId=floor.DestinationWarpId,X=floor.X,Y=floor.Y,Elevation=floor.Elevation,Floor=floor.SourceFloorValue}};
            return true;
        }
        if(effect.Opcode=="multichoicedefault")
        {
            if(!ownSource||a.Length!=5||a[0]!="0"||a[1]!="0"||a[2]!="MULTICHOICE_DEPT_STORE_ELEVATOR"||
                Value(a[3]) is <0 or >4||a[4]!="FALSE"||
                _content.DepartmentStoreElevatorFloors.Single(f=>f.SourceFloorValue==elevator.Floor).MenuIndex!=Value(a[3]))
                throw new InvalidDataException("department_elevator_menu");
            next=state with{DepartmentElevator=elevator with{MenuDefault=Value(a[3])}};return true;
        }
        if(a.Length!=(effect.Opcode=="specialvar"?2:1))throw new InvalidDataException("department_elevator_special_operands");
        string special=effect.Opcode=="specialvar"?a[1]:a[0];
        if(sharedSource&&(effect.Opcode!="special"||special!="GetElevatorFloor"))throw new InvalidDataException("department_elevator_shared_effect");
        switch(special)
        {
            case "GetElevatorFloor" when sharedSource:
                next=state with{Script=state.Script with{Variables=state.Script.Variables.SetItem("VAR_ELEVATOR_FLOOR",elevator.Floor)}};return true;
            case "DrawElevatorCurrentFloorWindow" when effect.Opcode=="special":
                if(Value("VAR_0x8005")!=elevator.Floor)throw new InvalidDataException("department_elevator_window_floor");
                next=state with{DepartmentElevator=elevator with{FloorWindowVisible=true}};return true;
            case "CloseElevatorCurrentFloorWindow" when effect.Opcode=="special":
                next=state with{DepartmentElevator=elevator with{FloorWindowVisible=false,MenuDefault=null}};return true;
            case "InitElevatorFloorSelectMenuPos" when effect.Opcode=="specialvar":
                if(a[0]!="VAR_RESULT"||Value("VAR_0x8004")!=3)throw new InvalidDataException("department_elevator_result_lvalue");
                int choice=_content.DepartmentStoreElevatorFloors.Single(f=>f.SourceFloorValue==elevator.Floor).MenuIndex;
                next=state with{Script=state.Script with{Variables=state.Script.Variables.SetItem("VAR_RESULT",choice)}};return true;
            case "AnimateElevator" when effect.Opcode=="special":
                int from=Value("VAR_0x8005"),to=Value("VAR_0x8006");
                if(from==to||to!=elevator.Floor||from!=Value("VAR_ELEVATOR_FLOOR")||
                    !_content.DepartmentStoreElevatorFloors.Any(f=>f.SourceFloorValue==from))
                    throw new InvalidDataException("department_elevator_animation_floor");
                next=state with{PresentationTicks=_content.DepartmentStoreElevatorShakeFrames(from,to)};return true;
            default:throw new InvalidDataException("department_elevator_special: "+special);
        }
    }

    private CampaignState RecordDepartmentElevatorEntrance(CampaignState previous,CampaignState moved)
    {
        if(moved.World.Player.MapId.Value!=DepartmentElevatorMap||
            previous.World.Player.MapId==moved.World.Player.MapId)return moved;
        var p=previous.World.Player;var(dx,dy)=Delta(p.Facing);
        var entrance=_content.DepartmentStoreElevatorEntrances.SingleOrDefault(e=>
            e.SourceMapId==p.MapId&&e.ReturnX==p.X+dx&&e.ReturnY==p.Y+dy)
            ??throw new InvalidDataException("department_elevator_entry_warp");
        var map=_content.Maps[p.MapId];
        var warp=map.FieldWarps.SingleOrDefault(w=>w.Index==entrance.SourceWarpId&&
            w.X==entrance.ReturnX&&w.Y==entrance.ReturnY&&w.DestinationMapId.Value==DepartmentElevatorMap&&
            w.DestinationWarpId==entrance.ElevatorWarpId)
            ??throw new InvalidDataException("department_elevator_entry_source");
        var tile=map.GetTile(warp.X,warp.Y)??throw new InvalidDataException("department_elevator_entry_cell");
        if(tile.Elevation!=entrance.ReturnElevation||warp.Elevation!=0&&warp.Elevation!=tile.Elevation)
            throw new InvalidDataException("department_elevator_entry_elevation");
        var floor=_content.DepartmentStoreElevatorFloors.Single(f=>f.DestinationMapId==p.MapId);
        return moved with{DepartmentElevator=new(p.MapId,entrance.SourceWarpId,warp.X,warp.Y,tile.Elevation,floor.SourceFloorValue)};
    }

    private Decision<CampaignState>? TryDepartmentDynamicExit(CampaignState state,CampaignCommand command)
    {
        if(command.Kind!="move"||state.World.Player.MapId.Value!=DepartmentElevatorMap)return null;
        var p=state.World.Player;var source=_content.Maps[p.MapId];
        if(command.Direction!=Direction.South||p.Facing!=Direction.South||source.SourceBehaviorAt(p.X,p.Y)!=0x65)return null;
        var exit=_content.DepartmentStoreDynamicExits.SingleOrDefault(e=>e.X==p.X&&e.Y==p.Y&&
            e.SourceWarpId==127&&(e.Elevation==0||e.Elevation==p.Elevation));
        if(exit is null||!source.FieldWarps.Any(w=>w.Index==exit.WarpIndex&&w.X==p.X&&w.Y==p.Y&&
            w.DestinationMapId.Value=="map.dynamic"&&w.DestinationWarpId==127)||
            state.DepartmentElevator is not {} target||!ValidDepartmentDestination(target)||
            target.FloorWindowVisible||target.MenuDefault is not null)
            return new(CommandStatus.Fault,state,"department_dynamic_destination_missing");
        var map=_content.Maps[target.DestinationMapId];int x=target.X,y=target.Y,anchorElevation=target.Elevation;
        if(target.DestinationWarpId!=255)
        {
            var warp=map.FieldWarps.Single(w=>w.Index==target.DestinationWarpId);x=warp.X;y=warp.Y;anchorElevation=warp.Elevation;
        }
        if(map.SourceBehaviorAt(x,y)!=0x69)throw new InvalidDataException("department_dynamic_arrival_door");
        var anchor=new TilePosition(map.Id,x,y,anchorElevation,Direction.South);
        y++;
        var tile=map.GetTile(x,y)??throw new InvalidDataException("department_dynamic_arrival_cell");
        if(tile.Blocked)throw new InvalidDataException("department_dynamic_arrival_blocked");
        var moved=CommitDynamicFieldWarp(state,new(map.Id,x,y,tile.Elevation,Direction.South),anchor,0x69)
            with{Exploration=ResetEncounterModifiers(state.Exploration!)};
        return new(CommandStatus.Accepted,Pump(EnterMap(moved,Direction.South)));
    }

    private bool ValidDepartmentDestination(CampaignDepartmentElevator elevator)
    {
        var floor=_content.DepartmentStoreElevatorFloors.SingleOrDefault(f=>
            f.DestinationMapId==elevator.DestinationMapId&&f.SourceFloorValue==elevator.Floor);
        if(floor is null||!_content.Maps.TryGetValue(elevator.DestinationMapId,out var map)||
            map.GetTile(elevator.X,elevator.Y) is not {} tile||tile.Elevation!=elevator.Elevation)return false;
        if(elevator.DestinationWarpId==255)
            return floor.DestinationWarpId==255&&elevator.X==floor.X&&elevator.Y==floor.Y&&elevator.Elevation==floor.Elevation;
        var entrance=_content.DepartmentStoreElevatorEntrances.SingleOrDefault(e=>e.SourceMapId==elevator.DestinationMapId&&
            e.SourceWarpId==elevator.DestinationWarpId&&e.ReturnX==elevator.X&&e.ReturnY==elevator.Y&&e.ReturnElevation==elevator.Elevation);
        return entrance is not null&&map.FieldWarps.Any(w=>w.Index==entrance.SourceWarpId&&w.X==elevator.X&&w.Y==elevator.Y&&
            w.DestinationMapId.Value==DepartmentElevatorMap&&w.DestinationWarpId==entrance.ElevatorWarpId);
    }

    private string? ValidateDepartmentElevatorProgress(CampaignState state)
    {
        bool inside=state.World.Player.MapId.Value==DepartmentElevatorMap;
        if(state.DepartmentElevator is not {} elevator)return inside?"save_department_elevator_return_missing":null;
        if(elevator.FloorWindowVisible||elevator.MenuDefault is not null)return "save_department_elevator_menu_active";
        if(!ValidDepartmentDestination(elevator))return "save_department_elevator_destination";
        if(inside&&state.Script.Flags.GetValueOrDefault("FLAG_TEMP_2")&&
            state.Script.Variables.GetValueOrDefault("VAR_ELEVATOR_FLOOR")!=elevator.Floor)
            return "save_department_elevator_floor_mismatch";
        return null;
    }
}
