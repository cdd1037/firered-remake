using System.Collections.Immutable;
using Game.Domain;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.World;
namespace Game.Application;
public sealed partial class CampaignSession
{
    private const string TrainerTowerElevator="map.trainer-tower-elevator";
    private bool TryApplyTrainerTowerElevatorEffect(CampaignState state,StoryEffect effect,out CampaignState next)
    {
        next=state;
        bool own=effect.Source.Path=="data/maps/TrainerTower_Elevator/scripts.inc";
        bool shared=effect.Source.Path=="data/event_scripts.s"&&effect.Source.Symbol=="EventScript_GetElevatorFloor";
        if(!_content.TrainerTowerEnabled||state.World.Player.MapId.Value!=TrainerTowerElevator||!own&&!shared)return false;
        var a=effect.Args;int Value(string s)=>DepartmentValue(state,s);
        var elevator=state.TrainerTowerElevator??throw new InvalidDataException("trainer_tower_elevator_entry_missing");
        if(effect.Opcode=="setdynamicwarp")
        {
            if(!a.SequenceEqual(new[]{"MAP_TRAINER_TOWER_LOBBY","17","8"}))throw new InvalidDataException("trainer_tower_dynamic_warp_source");
            var tile=_content.Maps[new(TrainerTowerLobby)].GetTile(17,8)!;
            next=state with{TrainerTowerElevator=elevator with{DestinationMapId=new(TrainerTowerLobby),DestinationWarpId=255,X=17,Y=8,Elevation=tile.Elevation,Floor=3}};return true;
        }
        if(effect.Opcode=="multichoicedefault")
        {if(a.Length!=5||a[2]!="MULTICHOICE_ROOFTOP_B1F"||a[4]!="FALSE")throw new InvalidDataException("trainer_tower_elevator_menu");next=state with{TrainerTowerMenu=new(a[2],["ROOFTOP","B1F","EXIT"]),TrainerTowerElevator=elevator with{MenuDefault=Value(a[3])}};return true;}
        if(effect.Opcode is not("special" or "specialvar"))return false;
        var special=a[effect.Opcode=="specialvar"?1:0];
        CampaignState Var(string key,int value)=>state with{Script=state.Script with{Variables=state.Script.Variables.SetItem(key,value)}};
        switch(special)
        {
            case "GetElevatorFloor":next=Var("VAR_ELEVATOR_FLOOR",elevator.Floor);return true;
            case "InitElevatorFloorSelectMenuPos":next=Var(a[0],elevator.Floor==3?1:0);return true;
            case "DrawElevatorCurrentFloorWindow":next=state with{TrainerTowerElevator=elevator with{FloorWindowVisible=true}};return true;
            case "CloseElevatorCurrentFloorWindow":next=state with{TrainerTowerElevator=elevator with{FloorWindowVisible=false,MenuDefault=null},TrainerTowerMenu=null};return true;
            case "AnimateElevator":next=state with{PresentationTicks=171};return true;
            default:return false;
        }
    }
    private CampaignState RecordTrainerTowerElevatorEntrance(CampaignState before,CampaignState moved)
    {
        if(!_content.TrainerTowerEnabled||moved.World.Player.MapId.Value!=TrainerTowerElevator||before.World.Player.MapId==moved.World.Player.MapId)return moved;
        if(before.World.Player.MapId.Value!="map.trainer-tower-roof")throw new InvalidDataException("trainer_tower_elevator_source");
        var map=_content.Maps[before.World.Player.MapId];var warp=map.FieldWarps.Single(w=>w.DestinationMapId.Value==TrainerTowerElevator);
        var tile=map.GetTile(warp.X,warp.Y)!;
        return moved with{TrainerTowerElevator=new(before.World.Player.MapId,warp.Index,warp.X,warp.Y,tile.Elevation,15)};
    }
    private Decision<CampaignState>? TryTrainerTowerDynamicExit(CampaignState state,CampaignCommand command)
    {
        if(!_content.TrainerTowerEnabled||command.Kind!="move"||state.World.Player.MapId.Value!=TrainerTowerElevator)return null;
        var p=state.World.Player;var map=_content.Maps[p.MapId];
        if(command.Direction!=Direction.South||p.Facing!=Direction.South||map.SourceBehaviorAt(p.X,p.Y)!=0x65)return null;
        if(state.TrainerTowerElevator is not {} target||target.FloorWindowVisible||target.MenuDefault is not null)return new(CommandStatus.Rejected,state,"trainer_tower_elevator_destination");
        var destination=_content.Maps[target.DestinationMapId];int x=target.X,y=target.Y;
        if(destination.SourceBehaviorAt(x,y)==0x69)y++;
        var tile=destination.GetTile(x,y)!;
        var next=state with{World=state.World with{Revision=checked(state.World.Revision+1),Player=new(target.DestinationMapId,x,y,tile.Elevation,Direction.South)}};
        return new(CommandStatus.Accepted,Pump(EnterMap(next,Direction.South)));
    }
}
