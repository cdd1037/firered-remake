using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Game.Domain;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.World;

namespace Game.Application;

/// <summary>Silph Co.'s dynamic return is independent of the retained Rocket elevator return.</summary>
public sealed record CampaignSilphElevator(MapId DestinationMapId,int DestinationWarpId,int X,int Y,int Elevation,int Floor)
{
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingDefault)] public bool FloorWindowVisible{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public int? MenuDefault{get;init;}
}

public sealed record CampaignSilphPad(string PresentationId,TilePosition From,TilePosition Pad,TilePosition To);

public sealed partial class CampaignSession
{
    private const string SilphElevatorMap="map.silph-co-elevator";
    private const string SilphElevatorSource="data/maps/SilphCo_Elevator/scripts.inc";
    private const string SilphDoorSource="data/scripts/silphco_doors.inc";

    private bool SilphMenuPending(CampaignState state)=>_content.SilphEnabled&&state.SilphElevator?.MenuDefault is not null;

    private Decision<CampaignState> DecideSilphElevatorChoice(CampaignState state,CampaignCommand command)
    {
        if(!SilphMenuPending(state)||command.Kind!="silph_elevator_choice"||command.Choice is not (>=0 and <=11 or 127)||
            state.World.Player.MapId.Value!=SilphElevatorMap||!state.Script.Locked||
            state.Script.Wait is not {Kind:StoryWaitKind.Presentation} wait||
            state.Script.Stack.LastOrDefault()?.ProgramId!="SilphCo_Elevator_EventScript_FloorSelect")
            return new(CommandStatus.Rejected,state,"silph_elevator_choice_required");
        var next=state with{SilphElevator=state.SilphElevator! with{MenuDefault=null},PresentationEffects=[],PresentationTicks=0,
            Script=state.Script with{Variables=state.Script.Variables.SetItem("VAR_RESULT",command.Choice.Value)}};
        return new(CommandStatus.Accepted,Pump(Advance(next,StoryRules.Continue(next.Script,wait.CorrelationId,_content))));
    }

    private Decision<CampaignState>? TrySilphPadStep(CampaignState state,CampaignCommand command)
    {
        if(command.Kind!="move"||!_content.SupportsTeleportPads(state.World.Player.MapId))return null;
        var p=state.World.Player;var(dx,dy)=Delta(command.Direction);
        if(SourceBehavior(state,p.X+dx,p.Y+dy)!=SilphFieldRules.PadBehavior)return null;
        var result=SilphFieldRules.StepOntoPad(state.World,new(new("silph-pad:"+state.World.Revision),state.World.Revision,command.Direction),
            SilphMovementMaps(RocketMovementMaps(state),state),OccupiedFieldMaps(state),_content.SilphWarpPads);
        if(result.Status!=TransitionStatus.Accepted)return new(result.Status==TransitionStatus.Rejected?CommandStatus.Rejected:CommandStatus.Fault,state,result.Diagnostic?.Code);
        if(result.SteppedOntoPad is not {} pad)return new(CommandStatus.Accepted,state with{World=result.State});
        // TryStartWarpEventScript preempts happiness, poison, Safari and Repel counters.
        // IncrementGameStat(STEPS) already happened; teleport arrival never replays it.
        var next=state with{World=result.State,Exploration=ResetEncounterModifiers(state.Exploration!)};
        next=Pump(EnterMap(next,command.Direction));
        return new(CommandStatus.Accepted,next with{SilphPad=new("silph-pad:"+result.State.Revision,p,pad,result.State.Player),PresentationTicks=99});
    }

    private Decision<CampaignState> DecideSilphPad(CampaignState state,CampaignCommand command)
    {
        if(state.SilphPad is not {} pad||command.Kind!="silph_pad_ack"||command.Text!=pad.PresentationId)
            return new(CommandStatus.Rejected,state,"silph_pad_presentation_pending");
        // The destination was committed atomically. Acknowledgement only releases presentation.
        return new(CommandStatus.Accepted,state with{SilphPad=null,PresentationTicks=0,PresentationEffects=[]});
    }

    private IReadOnlyDictionary<MapId,IFieldMap> SilphMovementMaps(IReadOnlyDictionary<MapId,IFieldMap> maps,CampaignState state)
    {
        if(!_content.SilphEnabled)return maps;
        return maps.ToDictionary(x=>x.Key,x=>_content.SupportsTeleportPads(x.Key)?(IFieldMap)new SilphMovementMap(x.Value,
            x.Key==state.World.Player.MapId?state.MapEdits:null,_content):x.Value);
    }

    private sealed class SilphMovementMap(IFieldMap original,ImmutableSortedDictionary<string,FieldMetatileEdit>? edits,ICampaignCatalog content):IFieldMap
    {
        public MapId Id=>original.Id;public int Width=>original.Width;public int Height=>original.Height;
        public ImmutableArray<FieldWarp> FieldWarps=>original.FieldWarps;
        public int? SourceBehaviorAt(int x,int y)=>edits?.TryGetValue(x+":"+y,out var e)==true?
            content.MetatileDefinition(Id,e.SourceMetatileId)!.Behavior:original.SourceBehaviorAt(x,y);
        public int? SourceEncounterTypeAt(int x,int y)=>original.SourceEncounterTypeAt(x,y);
        public TileCell? GetTile(int x,int y)
        {
            var cell=original.GetTile(x,y);
            if(edits?.TryGetValue(x+":"+y,out var e)==true)
            {
                int b=SourceBehaviorAt(x,y)!.Value;
                cell=new(e.Collision!=0,e.Elevation,b is 0 or SilphFieldRules.PadBehavior?TileBehavior.Floor:
                    b is >=0x62 and <=0x65?TileBehavior.ArrowWarp:b is >=0x6c and <=0x6f?TileBehavior.StairWarp:TileBehavior.Unsupported);
            }
            return cell is not null&&SourceBehaviorAt(x,y)==SilphFieldRules.PadBehavior?cell with{Behavior=TileBehavior.Floor}:cell;
        }
    }

    private bool TryApplySilphFieldEffect(CampaignState state,StoryEffect effect,out CampaignState next)
    {
        next=state;if(!_content.SilphEnabled)return false;
        if(TryApplySilphElevatorEffect(state,effect,out next))return true;
        if(effect.Source.Path!=SilphDoorSource)return false;
        if(!SilphFieldRules.IsSilphMap(state.World.Player.MapId))throw new InvalidDataException("silph_door_map_context");
        var a=effect.Args;
        if(effect.Opcode=="setmetatile")
        {
            var door=_content.SilphDoors.SingleOrDefault(d=>d.MapId==state.World.Player.MapId&&
                (d.ClosedScriptId==effect.Source.Symbol||d.OpenScriptId==effect.Source.Symbol))??throw new InvalidDataException("silph_door_patch_source");
            bool open=door.OpenScriptId==effect.Source.Symbol;
            if(open)RequireSilphDoorContext(state,door);
            else if(state.Script.Flags.GetValueOrDefault(door.Flag))throw new InvalidDataException("silph_door_closed_after_unlock");
            if(a.Length!=4)throw new InvalidDataException("silph_door_patch_operands");
            int x=SilphValue(state,a[0]),y=SilphValue(state,a[1]);
            var cell=(open?door.OpenCells:door.ClosedCells).SingleOrDefault(c=>c.X==x&&c.Y==y);
            if(cell is null||cell.SourceMetatileId!=SilphValue(state,a[2])||cell.Collision!=SilphValue(state,a[3]))
                throw new InvalidDataException("silph_door_patch_mismatch");
            var tile=_content.Maps[door.MapId].GetTile(x,y)??throw new InvalidDataException("silph_door_cell_missing");
            _=_content.MetatileDefinition(door.MapId,cell.SourceMetatileId)??throw new InvalidDataException("silph_door_metatile_missing");
            next=state with{MapEdits=(state.MapEdits??ImmutableSortedDictionary<string,FieldMetatileEdit>.Empty)
                .SetItem(x+":"+y,new(x,y,cell.SourceMetatileId,cell.Collision==0?0:3,tile.Elevation))};return true;
        }
        if(effect.Opcode=="special"&&a.Length==1&&a[0] is "DrawWholeMapView" or "SetHiddenItemFlag")
        {
            var door=_content.SilphDoors.SingleOrDefault(d=>d.MapId==state.World.Player.MapId&&d.DoorIndex==SilphValue(state,"VAR_TEMP_1"))
                ??throw new InvalidDataException("silph_door_special_context");
            if(effect.Source.Symbol!="EventScript_OpenDoor")throw new InvalidDataException("silph_door_special_source");
            RequireSilphDoorContext(state,door);
            foreach(var cell in door.OpenCells)
                if(state.MapEdits?.GetValueOrDefault(cell.X+":"+cell.Y) is not {} edit||edit.SourceMetatileId!=cell.SourceMetatileId||edit.Collision!=(cell.Collision==0?0:3))
                    throw new InvalidDataException("silph_door_open_incomplete");
            if(a[0]=="SetHiddenItemFlag")next=state with{Script=state.Script with{Flags=state.Script.Flags.SetItem(door.Flag,true)}};
            return true;
        }
        return false;
    }

    private void RequireSilphDoorContext(CampaignState state,SilphDoorDefinition door)
    {
        var p=state.World.Player;var(dx,dy)=Delta(p.Facing);
        if(!state.Script.Locked||!state.Script.Flags.GetValueOrDefault(SilphFieldRules.CardKeyFlag)||
            SilphValue(state,"VAR_TEMP_1")!=door.DoorIndex||SilphValue(state,"VAR_0x8004")!=SilphValue(state,door.Flag)||
            !_content.MapDefinition(p.MapId).BackgroundTriggers.Any(t=>t.ScriptId==door.ScriptId&&t.X==p.X+dx&&t.Y==p.Y+dy&&
                (t.Elevation==0||t.Elevation==p.Elevation)&&(t.PlayerFacing is null||t.PlayerFacing==p.Facing)))
            throw new InvalidDataException("silph_card_key_door_context");
    }

    private string? ValidateSilphFieldProgress(CampaignState state)
    {
        if(state.SilphPad is not null)return "save_silph_pad_active";
        if(ValidateSilphElevatorProgress(state) is {} elevatorError)return elevatorError;
        if(!_content.SilphEnabled)return state.Script.Flags.Any(f=>f.Value&&f.Key.StartsWith("FLAG_SILPH_",StringComparison.Ordinal))?"save_silph_profile_missing":null;
        bool key=state.Script.Flags.GetValueOrDefault(SilphFieldRules.CardKeyFlag);
        if(_content.SilphDoors.Any(d=>state.Script.Flags.GetValueOrDefault(d.Flag)&&!key))return "save_silph_door_without_card_key";
        if(!SilphFieldRules.IsSilphMap(state.World.Player.MapId))return null;
        var edits=state.MapEdits??ImmutableSortedDictionary<string,FieldMetatileEdit>.Empty;
        var expected=new Dictionary<string,FieldMetatileEdit>();
        foreach(var door in _content.SilphDoors.Where(d=>d.MapId==state.World.Player.MapId))
        {
            bool open=state.Script.Flags.GetValueOrDefault(door.Flag);
            foreach(var cell in open?door.OpenCells:door.ClosedCells)
            {
                var tile=_content.Maps[door.MapId].GetTile(cell.X,cell.Y)!;
                var value=new FieldMetatileEdit(cell.X,cell.Y,cell.SourceMetatileId,cell.Collision==0?0:3,tile.Elevation);
                string id=cell.X+":"+cell.Y;expected.Add(id,value);
                if(!open&&!edits.ContainsKey(id)||edits.TryGetValue(id,out var actual)&&actual!=value)return "save_silph_door_patch_mismatch";
            }
        }
        return edits.Any(e=>!expected.TryGetValue(e.Key,out var value)||value!=e.Value)?"save_silph_door_patch_unknown":null;
    }

    private int SilphValue(CampaignState state,string token)=>
        token.StartsWith("VAR_",StringComparison.Ordinal)?state.Script.Variables.GetValueOrDefault(token):
        int.TryParse(token,out int n)?n:_content.TryConstant(token,out n)?n:
        throw new InvalidDataException("silph_elevator_operand_missing: "+token);

    private bool TryApplySilphElevatorEffect(CampaignState state,StoryEffect effect,out CampaignState next)
    {
        next=state;
        if(!_content.SilphEnabled)return false;
        bool ownSource=effect.Source.Path==SilphElevatorSource;
        // The shared helper also serves Rocket Hideout. Claim it only in this exact map.
        bool sharedSource=state.World.Player.MapId.Value==SilphElevatorMap&&
            effect.Source.Path=="data/event_scripts.s"&&effect.Source.Symbol=="EventScript_GetElevatorFloor";
        if(!(ownSource||sharedSource)||effect.Opcode is not("special" or "specialvar" or "waitstate" or "setdynamicwarp"))return false;
        if(state.World.Player.MapId.Value!=SilphElevatorMap||state.SilphElevator is not {} elevator||
            !state.Script.Locked||!ValidSilphDestination(elevator))
            throw new InvalidDataException("silph_elevator_context");
        var a=effect.Args;int Value(string token)=>SilphValue(state,token);
        if(effect.Opcode=="setdynamicwarp")
        {
            if(!ownSource||a.Length!=4)throw new InvalidDataException("silph_elevator_destination_operands");
            var floor=_content.SilphElevatorFloors.SingleOrDefault(f=>
                _content.MapFromSourceSymbol(a[0])==f.DestinationMapId&&Value(a[1])==f.DestinationWarpId&&
                Value(a[2])==f.X&&Value(a[3])==f.Y)??throw new InvalidDataException("silph_elevator_destination");
            if(floor.SourceFloorValue!=Value("VAR_0x8006"))throw new InvalidDataException("silph_elevator_destination_floor");
            // Source sets the dynamic destination even for a same-floor selection, before its comparison.
            next=state with{SilphElevator=elevator with{DestinationMapId=floor.DestinationMapId,
                DestinationWarpId=floor.DestinationWarpId,X=floor.X,Y=floor.Y,Elevation=floor.Elevation,Floor=floor.SourceFloorValue}};
            return true;
        }
        if(effect.Opcode=="waitstate")
        {
            if(!ownSource||a.Length!=0||state.Script.Wait is not {Kind:StoryWaitKind.Presentation})
                throw new InvalidDataException("silph_elevator_wait_context");
            return true;
        }
        if(a.Length!=(effect.Opcode=="specialvar"?2:1))throw new InvalidDataException("silph_elevator_special_operands");
        string special=effect.Opcode=="specialvar"?a[1]:a[0];
        if(sharedSource&&(effect.Opcode!="special"||special!="GetElevatorFloor"))throw new InvalidDataException("silph_elevator_shared_effect");
        switch(special)
        {
            case "GetElevatorFloor" when sharedSource:
                next=state with{Script=state.Script with{Variables=state.Script.Variables.SetItem("VAR_ELEVATOR_FLOOR",elevator.Floor)}};return true;
            case "DrawElevatorCurrentFloorWindow" when effect.Opcode=="special":
                if(Value("VAR_0x8005")!=elevator.Floor)throw new InvalidDataException("silph_elevator_window_floor");
                next=state with{SilphElevator=elevator with{FloorWindowVisible=true}};return true;
            case "CloseElevatorCurrentFloorWindow" when effect.Opcode=="special":
                next=state with{SilphElevator=elevator with{FloorWindowVisible=false,MenuDefault=null}};return true;
            case "InitElevatorFloorSelectMenuPos" when effect.Opcode=="specialvar":
                if(a[0]!="VAR_RESULT"||Value("VAR_0x8004")!=Value("LISTMENU_SILPHCO_FLOORS"))throw new InvalidDataException("silph_elevator_result_lvalue");
                var menu=SilphFieldRules.ElevatorMenuPosition(elevator.Floor);
                next=state with{Script=state.Script with{Variables=state.Script.Variables.SetItem("VAR_RESULT",menu.Row)}};return true;
            case "ListMenu" when effect.Opcode=="special":
                if(Value("VAR_0x8004")!=Value("LISTMENU_SILPHCO_FLOORS")||!elevator.FloorWindowVisible||
                    Value("VAR_RESULT")!=SilphFieldRules.ElevatorMenuPosition(elevator.Floor).Row)
                    throw new InvalidDataException("silph_elevator_list_menu");
                next=state with{SilphElevator=elevator with{MenuDefault=14-elevator.Floor}};return true;
            case "AnimateElevator" when effect.Opcode=="special":
                int from=Value("VAR_0x8005"),to=Value("VAR_0x8006");
                if(from==to||to!=elevator.Floor||from!=Value("VAR_ELEVATOR_FLOOR")||
                    !_content.SilphElevatorFloors.Any(f=>f.SourceFloorValue==from))
                    throw new InvalidDataException("silph_elevator_animation_floor");
                next=state with{PresentationTicks=SilphFieldRules.ElevatorShakeFrames(from,to)};return true;
            default:throw new InvalidDataException("silph_elevator_special: "+special);
        }
    }

    private CampaignState RecordSilphElevatorEntrance(CampaignState previous,CampaignState moved)
    {
        if(!_content.SilphEnabled||moved.World.Player.MapId.Value!=SilphElevatorMap||
            previous.World.Player.MapId==moved.World.Player.MapId)return moved;
        var p=previous.World.Player;var(dx,dy)=Delta(p.Facing);
        var entrance=_content.SilphElevatorEntrances.SingleOrDefault(e=>
            e.SourceMapId==p.MapId&&e.ReturnX==p.X+dx&&e.ReturnY==p.Y+dy)
            ??throw new InvalidDataException("silph_elevator_entry_warp");
        var map=_content.Maps[p.MapId];
        var warp=map.FieldWarps.SingleOrDefault(w=>w.Index==entrance.SourceWarpId&&
            w.X==entrance.ReturnX&&w.Y==entrance.ReturnY&&w.DestinationMapId.Value==SilphElevatorMap&&
            w.DestinationWarpId==entrance.ElevatorWarpId)
            ??throw new InvalidDataException("silph_elevator_entry_source");
        var tile=map.GetTile(warp.X,warp.Y)??throw new InvalidDataException("silph_elevator_entry_cell");
        if(tile.Elevation!=entrance.ReturnElevation||warp.Elevation!=0&&warp.Elevation!=tile.Elevation)
            throw new InvalidDataException("silph_elevator_entry_elevation");
        var floor=_content.SilphElevatorFloors.Single(f=>f.DestinationMapId==p.MapId);
        return moved with{SilphElevator=new(p.MapId,entrance.SourceWarpId,warp.X,warp.Y,tile.Elevation,floor.SourceFloorValue)};
    }

    private Decision<CampaignState>? TrySilphDynamicExit(CampaignState state,CampaignCommand command)
    {
        if(!_content.SilphEnabled||command.Kind!="move"||state.World.Player.MapId.Value!=SilphElevatorMap)return null;
        var p=state.World.Player;var source=_content.Maps[p.MapId];
        if(command.Direction!=Direction.South||p.Facing!=Direction.South||source.SourceBehaviorAt(p.X,p.Y)!=0x65)return null;
        var exit=_content.SilphDynamicExits.SingleOrDefault(e=>e.X==p.X&&e.Y==p.Y&&
            e.SourceWarpId==127&&(e.Elevation==0||e.Elevation==p.Elevation));
        if(exit is null||!source.FieldWarps.Any(w=>w.Index==exit.WarpIndex&&w.X==p.X&&w.Y==p.Y&&
            w.DestinationMapId.Value=="map.dynamic"&&w.DestinationWarpId==127)||
            state.SilphElevator is not {} target||!ValidSilphDestination(target)||
            target.FloorWindowVisible||target.MenuDefault is not null)
            return new(CommandStatus.Fault,state,"silph_dynamic_destination_missing");
        var map=_content.Maps[target.DestinationMapId];int x=target.X,y=target.Y;
        if(target.DestinationWarpId!=255)
        {
            var warp=map.FieldWarps.Single(w=>w.Index==target.DestinationWarpId);x=warp.X;y=warp.Y;
        }
        if(map.SourceBehaviorAt(x,y)!=0x69)throw new InvalidDataException("silph_dynamic_arrival_door");
        y++;
        var tile=map.GetTile(x,y)??throw new InvalidDataException("silph_dynamic_arrival_cell");
        if(tile.Blocked)throw new InvalidDataException("silph_dynamic_arrival_blocked");
        var moved=state with{World=state.World with{Revision=checked(state.World.Revision+1),
            Player=new(map.Id,x,y,tile.Elevation,Direction.South)},Exploration=ResetEncounterModifiers(state.Exploration!)};
        return new(CommandStatus.Accepted,Pump(EnterMap(moved,Direction.South)));
    }

    private bool ValidSilphDestination(CampaignSilphElevator elevator)
    {
        var floor=_content.SilphElevatorFloors.SingleOrDefault(f=>
            f.DestinationMapId==elevator.DestinationMapId&&f.SourceFloorValue==elevator.Floor);
        if(floor is null||!_content.Maps.TryGetValue(elevator.DestinationMapId,out var map)||
            map.GetTile(elevator.X,elevator.Y) is not {} tile||tile.Elevation!=elevator.Elevation)return false;
        if(elevator.DestinationWarpId==255)
            return floor.DestinationWarpId==255&&elevator.X==floor.X&&elevator.Y==floor.Y&&elevator.Elevation==floor.Elevation;
        var entrance=_content.SilphElevatorEntrances.SingleOrDefault(e=>e.SourceMapId==elevator.DestinationMapId&&
            e.SourceWarpId==elevator.DestinationWarpId&&e.ReturnX==elevator.X&&e.ReturnY==elevator.Y&&e.ReturnElevation==elevator.Elevation);
        return entrance is not null&&map.FieldWarps.Any(w=>w.Index==entrance.SourceWarpId&&w.X==elevator.X&&w.Y==elevator.Y&&
            w.DestinationMapId.Value==SilphElevatorMap&&w.DestinationWarpId==entrance.ElevatorWarpId);
    }

    private string? ValidateSilphElevatorProgress(CampaignState state)
    {
        if(!_content.SilphEnabled)return state.SilphElevator is not null?"save_silph_profile_missing":null;
        bool inside=state.World.Player.MapId.Value==SilphElevatorMap;
        if(state.SilphElevator is not {} elevator)return inside?"save_silph_elevator_return_missing":null;
        if(elevator.FloorWindowVisible||elevator.MenuDefault is not null)return "save_silph_elevator_menu_active";
        if(!ValidSilphDestination(elevator))return "save_silph_elevator_destination";
        if(inside&&state.Script.Flags.GetValueOrDefault("FLAG_TEMP_2")&&
            state.Script.Variables.GetValueOrDefault("VAR_ELEVATOR_FLOOR")!=elevator.Floor)
            return "save_silph_elevator_floor_mismatch";
        return null;
    }
}
