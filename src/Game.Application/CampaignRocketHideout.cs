using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Game.Domain;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.World;

namespace Game.Application;

/// <summary>The source dynamic warp persists independently of the elevator's temporary UI.</summary>
public sealed record CampaignRocketElevator(MapId DestinationMapId,int DestinationWarpId,int X,int Y,int Elevation,int Floor)
{
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingDefault)] public bool FloorWindowVisible{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public int? MenuDefault{get;init;}
}

public sealed partial class CampaignSession
{
    private const string RocketElevatorMap="map.rocket-hideout-elevator";
    private const string RocketElevatorSource="data/maps/RocketHideout_Elevator/scripts.inc";
    private const string RocketB4Source="data/maps/RocketHideout_B4F/scripts.inc";
    private static bool RocketMap(MapId id)=>id.Value.StartsWith("map.rocket-hideout-",StringComparison.Ordinal)||id.Value=="map.celadon-city-game-corner";
    private bool IsRocketHideoutButton(CampaignState state)=>state.World.Player.MapId.Value=="map.rocket-hideout-b4f"&&state.Script.Wait?.Kind==StoryWaitKind.ButtonPress&&state.Script.Stack.LastOrDefault()?.ProgramId=="RocketHideout_B4F_EventScript_Giovanni";
    private Decision<CampaignState> DecideRocketHideoutButton(CampaignState state,CampaignCommand command)
    {
        if(!IsRocketHideoutButton(state)||command.Text is not("A" or "B"))return new(CommandStatus.Rejected,state,"rocket_giovanni_button_invalid");
        var next=state with{PresentationEffects=[],PresentationTicks=0,Motions=[]};
        return new(CommandStatus.Accepted,Pump(Advance(next,StoryRules.Continue(next.Script,next.Script.Wait!.CorrelationId,_content))));
    }
    private int RocketValue(CampaignState state,string token)=>token.StartsWith("VAR_",StringComparison.Ordinal)?state.Script.Variables.GetValueOrDefault(token):int.TryParse(token,out int n)?n:_content.TryConstant(token,out n)?n:throw new InvalidDataException("rocket_operand_missing: "+token);
    private ImmutableSortedDictionary<string,FieldMetatileEdit> RocketExpectedEdits(CampaignState state)
    {
        var result=ImmutableSortedDictionary<string,FieldMetatileEdit>.Empty;
        foreach(var patch in _content.RocketHideoutInitialPatches(state.Script.Flags.GetValueOrDefault("FLAG_OPENED_ROCKET_HIDEOUT"),state.BattledTrainers.Contains("pret:TRAINER_TEAM_ROCKET_GRUNT_12"),state.BattledTrainers.Contains("pret:TRAINER_TEAM_ROCKET_GRUNT_16")&&state.BattledTrainers.Contains("pret:TRAINER_TEAM_ROCKET_GRUNT_17")))
            if(patch.MapId==state.World.Player.MapId)
                foreach(var c in patch.Cells)result=result.SetItem(c.X+":"+c.Y,new(c.X,c.Y,c.SourceMetatileId,c.Collision==0?0:3,c.Elevation));
        return result;
    }
    private bool TryApplyRocketHideoutEffect(CampaignState state,StoryEffect effect,out CampaignState next)
    {
        next=state;        var a=effect.Args;int Value(string t)=>RocketValue(state,t);
        bool mapSource=effect.Source.Path.StartsWith("data/maps/RocketHideout_",StringComparison.Ordinal)||effect.Source.Path=="data/maps/CeladonCity_GameCorner/scripts.inc";
        if(mapSource&&!RocketMap(state.World.Player.MapId))throw new InvalidDataException("rocket_effect_map_context");
        if(mapSource&&effect.Opcode is "call_if_defeated" or "call_if_not_defeated")
        {
            if(a.Length!=2||!TrainerRegisteredOnMap(new("pret:"+a[0]),state.World.Player.MapId.Value)||_content.FindProgram(a[1]) is null)throw new InvalidDataException("rocket_trainer_branch_context");
            bool won=state.BattledTrainers.Contains("pret:"+a[0]);
            if(won==(effect.Opcode=="call_if_defeated"))next=state with{Script=state.Script with{Stack=state.Script.Stack.Add(new(a[1],0))}};
            return true;
        }
        if(mapSource&&effect.Opcode=="addvar")
        {
            if(effect.Source.Path!=RocketB4Source||a.Length!=2||a[0]!="VAR_TEMP_1"||Value(a[1])!=1)throw new InvalidDataException("rocket_counter_context");
            next=state with{Script=state.Script with{Variables=state.Script.Variables.SetItem(a[0],unchecked((ushort)(Value(a[0])+1)))}};return true;
        }
        if(mapSource&&effect.Opcode=="setmetatile")
        {
            var patch=_content.RocketHideoutMetatilePatches.SingleOrDefault(p=>p.Label==effect.Source.Symbol&&p.MapId==state.World.Player.MapId)??throw new InvalidDataException("rocket_patch_source");
            int x=Value(a[0]),y=Value(a[1]);var c=patch.Cells.SingleOrDefault(c=>c.X==x&&c.Y==y)??throw new InvalidDataException("rocket_patch_cell");
            if(c.SourceMetatileId!=Value(a[2])||c.Collision!=Value(a[3]))throw new InvalidDataException("rocket_patch_operands");
            next=state with{MapEdits=(state.MapEdits??ImmutableSortedDictionary<string,FieldMetatileEdit>.Empty).SetItem(x+":"+y,new(x,y,c.SourceMetatileId,c.Collision==0?0:3,c.Elevation))};return true;
        }
        if(mapSource&&effect.Opcode=="special"&&a.SequenceEqual(new[]{"DrawWholeMapView"}))return true;
        if(effect.Opcode=="waitbuttonpress"&&effect.Source.Path==RocketB4Source)
        {
            if(!IsRocketHideoutButton(state)||state.Dialogue?.TextSymbol!="RocketHideout_B4F_Text_GiovanniIntro")throw new InvalidDataException("rocket_giovanni_button_context");return true;
        }
        if(state.FieldPickup is {} pickup&&_content.FieldPickups.Single(p=>p.Id==pickup.Id) is {} definition&&definition.ItemSymbol is "ITEM_LIFT_KEY" or "ITEM_SILPH_SCOPE"&&effect.Opcode is "checkitemspace" or "additem" or "bufferitemname" or "bufferitemnameplural" or "checkitemtype")
        {
            if(effect.Source.Path is not("data/scripts/obtain_item.inc" or "data/event_scripts.s")||state.World.Player.MapId.Value!="map.rocket-hideout-b4f")throw new InvalidDataException("rocket_key_item_source");
            bool buffer=effect.Opcode.StartsWith("bufferitem",StringComparison.Ordinal);
            if(Value(a[buffer?1:0])!=definition.SourceItemId)throw new InvalidDataException("rocket_key_item_identity");
            if(buffer){next=state with{TextBuffers=state.TextBuffers.SetItem(a[0],definition.ItemName)};return true;}
            if(effect.Opcode=="checkitemtype"){next=state with{Script=state.Script with{Variables=state.Script.Variables.SetItem("VAR_RESULT",Value("POCKET_KEY_ITEMS"))}};return true;}
            if((a.Length==1?1:Value(a[1]))!=1||pickup.InventoryAdded)throw new InvalidDataException("rocket_key_quantity_or_repeat");
            var inventory=state.Inventory??throw new InvalidDataException("inventory_missing");
            var keys=inventory.KeyItems??[];bool success=keys.Length<ShopCatalog.QuestLimits.KeyItemSlots&&!keys.Any(k=>k.Item==definition.Item);
            if(!success&&effect.Opcode=="additem")throw new InvalidDataException("rocket_key_capacity_changed");
            bool add=success&&effect.Opcode=="additem";
            next=state with{Inventory=add?inventory with{KeyItems=keys.Add(new(definition.Item,1))}:inventory,FieldPickup=add?pickup with{InventoryAdded=true}:pickup,Script=state.Script with{Variables=state.Script.Variables.SetItem("VAR_RESULT",success?1:0)}};return true;
        }
        bool elevatorSource=effect.Source.Path==RocketElevatorSource||effect.Source.Path=="data/event_scripts.s"&&effect.Source.Symbol=="EventScript_GetElevatorFloor";
        if(elevatorSource&&effect.Opcode is "special" or "specialvar" or "multichoicedefault" or "setdynamicwarp")
        {
            if(state.World.Player.MapId.Value!=RocketElevatorMap||state.RocketElevator is not {} elevator||!state.Script.Locked)throw new InvalidDataException("rocket_elevator_context");
            if(effect.Opcode=="setdynamicwarp")
            {
                var floor=_content.RocketHideoutElevatorFloors.SingleOrDefault(f=>_content.MapFromSourceSymbol(a[0])==f.DestinationMapId&&Value(a[1])==f.DestinationWarpId&&Value(a[2])==f.X&&Value(a[3])==f.Y)??throw new InvalidDataException("rocket_elevator_destination");
                next=state with{RocketElevator=elevator with{DestinationMapId=floor.DestinationMapId,DestinationWarpId=floor.DestinationWarpId,X=floor.X,Y=floor.Y,Elevation=floor.Elevation,Floor=floor.SourceFloorValue}};return true;
            }
            if(effect.Opcode=="multichoicedefault")
            {
                if(a.Length!=5||a[0]!="0"||a[1]!="0"||a[2]!="MULTICHOICE_ROCKET_HIDEOUT_ELEVATOR"||Value(a[3]) is <0 or >2||a[4]!="FALSE")throw new InvalidDataException("rocket_elevator_menu");
                next=state with{RocketElevator=elevator with{MenuDefault=Value(a[3])}};return true;
            }
            string special=effect.Opcode=="specialvar"?a[1]:a[0];
            switch(special)
            {
                case "GetElevatorFloor":next=state with{Script=state.Script with{Variables=state.Script.Variables.SetItem("VAR_ELEVATOR_FLOOR",elevator.Floor)}};return true;
                case "DrawElevatorCurrentFloorWindow":next=state with{RocketElevator=elevator with{FloorWindowVisible=true}};return true;
                case "CloseElevatorCurrentFloorWindow":next=state with{RocketElevator=elevator with{FloorWindowVisible=false,MenuDefault=null}};return true;
                case "InitElevatorFloorSelectMenuPos":
                    if(a[0]!="VAR_RESULT")throw new InvalidDataException("rocket_elevator_result_lvalue");
                    int choice=_content.RocketHideoutElevatorFloors.Single(f=>f.DestinationMapId==elevator.DestinationMapId).MenuIndex;
                    next=state with{Script=state.Script with{Variables=state.Script.Variables.SetItem("VAR_RESULT",choice)}};return true;
                case "AnimateElevator":
                    int distance=Math.Abs(state.Script.Variables.GetValueOrDefault("VAR_0x8005")-state.Script.Variables.GetValueOrDefault("VAR_0x8006"));
                    if(distance is <1 or >3)throw new InvalidDataException("rocket_elevator_animation_distance");
                    next=state with{PresentationTicks=24*(distance+1)};return true;
                default:throw new InvalidDataException("rocket_elevator_special: "+special);
            }
        }
        return false;
    }
    private string? ValidateRocketHideoutProgress(CampaignState state)
    {
        if(state.RocketSpin is not null)return "save_rocket_spin_active";
        bool Won(string trainer)=>state.BattledTrainers.Contains("pret:"+trainer);
        bool flag(string name)=>state.Script.Flags.GetValueOrDefault(name);
        int Count(string item)=>OpeningInventoryRules.Quantity(state.Inventory?.KeyItems??[],new("pret:ITEM_"+item));
        if(flag("FLAG_HIDE_GAME_CORNER_ROCKET")!=Won("TRAINER_TEAM_ROCKET_GRUNT_7")||flag("FLAG_OPENED_ROCKET_HIDEOUT")&&!Won("TRAINER_TEAM_ROCKET_GRUNT_7"))return "save_rocket_corner_progress";
        bool keyWon=Won("TRAINER_TEAM_ROCKET_GRUNT_18"),giovanni=Won("TRAINER_BOSS_GIOVANNI");
        if(flag("FLAG_CAN_USE_ROCKET_HIDEOUT_LIFT")!=(Count("LIFT_KEY")==1)||Count("LIFT_KEY")>1||Count("SILPH_SCOPE")>1||flag("FLAG_HIDE_LIFT_KEY")!=(!keyWon||Count("LIFT_KEY")==1)||flag("FLAG_HIDE_SILPH_SCOPE")!=(!giovanni||Count("SILPH_SCOPE")==1)||flag("FLAG_HIDE_CELADON_ROCKETS")!=giovanni)return "save_rocket_key_scope_progress";
        if(giovanni&&(!Won("TRAINER_TEAM_ROCKET_GRUNT_16")||!Won("TRAINER_TEAM_ROCKET_GRUNT_17")||Count("LIFT_KEY")!=1))return "save_rocket_giovanni_gate";
        if(RocketMap(state.World.Player.MapId)&&state.World.Player.MapId.Value!="map.celadon-city-game-corner"&&!flag("FLAG_OPENED_ROCKET_HIDEOUT"))return "save_rocket_entrance_gate";
        if(state.RocketElevator is {} elevator)
        {
            if(elevator.FloorWindowVisible||elevator.MenuDefault is not null)return "save_rocket_elevator_menu_active";
            var floor=_content.RocketHideoutElevatorFloors.SingleOrDefault(f=>f.DestinationMapId==elevator.DestinationMapId&&f.SourceFloorValue==elevator.Floor);
            bool valid=floor is not null&&(elevator.DestinationWarpId==255?elevator.X==floor.X&&elevator.Y==floor.Y&&elevator.Elevation==floor.Elevation:_content.RocketHideoutElevatorEntrances.Any(e=>e.SourceMapId==elevator.DestinationMapId&&e.SourceWarpId==elevator.DestinationWarpId&&e.ReturnX==elevator.X&&e.ReturnY==elevator.Y&&e.ReturnElevation==elevator.Elevation));
            if(!valid)return "save_rocket_elevator_destination";
        }
        else if(state.World.Player.MapId.Value==RocketElevatorMap)return "save_rocket_elevator_return_missing";
        if(RocketMap(state.World.Player.MapId))
        {
            var expected=RocketExpectedEdits(state);var actual=state.MapEdits??ImmutableSortedDictionary<string,FieldMetatileEdit>.Empty;
            // Source opened layouts need no patch on reentry; an already-opened in-map script
            // may explicitly retain the equivalent open cells. Compare effective cells.
            foreach(var c in expected.Values)
            {
                var cell=actual.GetValueOrDefault(c.X+":"+c.Y);
                if(cell!=c)return "save_rocket_map_patch_mismatch";
            }
            foreach(var c in actual.Values)
                if(!_content.RocketHideoutMetatilePatches.Any(p=>p.MapId==state.World.Player.MapId&&p.Cells.Any(t=>t.X==c.X&&t.Y==c.Y&&t.SourceMetatileId==c.SourceMetatileId&&(t.Collision==0?0:3)==c.Collision&&t.Elevation==c.Elevation)))return "save_rocket_map_patch_unknown";
        }
        return null;
    }
}
