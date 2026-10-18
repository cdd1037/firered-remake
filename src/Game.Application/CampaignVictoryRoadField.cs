using System.Collections.Immutable;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.World;

namespace Game.Application;

public sealed partial class CampaignSession
{
    private const string VictoryRoadLowerHide="FLAG_HIDE_VICTORY_ROAD_2F_BOULDER",VictoryRoadUpperHide="FLAG_HIDE_VICTORY_ROAD_3F_BOULDER";
    private const int VictoryRoadButtonBehavior=0x20;
    private sealed record VictoryRoadSwitch(string Floor,string Suffix,string Variable,int X,int Y,int BarrierX,int BarrierY,ImmutableArray<int> CopyIds)
    {
        public string Map=>"map.victory-road-"+Floor.ToLowerInvariant();
        public string Source=>"data/maps/VictoryRoad_"+Floor+"/scripts.inc";
        public string Press=>"VictoryRoad_"+Floor+"_EventScript_FloorSwitch"+Suffix;
        public string Barrier=>"VictoryRoad_"+Floor+"_EventScript_SetRockBarrier"+Suffix;
    }
    private static readonly ImmutableArray<VictoryRoadSwitch> VictoryRoadSwitches=
    [
        new("1F","","VAR_MAP_SCENE_VICTORY_ROAD_1F",20,16,12,14,[5]),
        new("2F","1","VAR_MAP_SCENE_VICTORY_ROAD_2F_BOULDER1",2,19,13,10,[11]),
        new("2F","2","VAR_MAP_SCENE_VICTORY_ROAD_2F_BOULDER2",14,19,33,16,[12]),
        new("3F","","VAR_MAP_SCENE_VICTORY_ROAD_3F",7,7,12,12,[7,10])
    ];
    private static bool VictoryRoadFieldMap(MapId map)=>map.Value is "map.victory-road-1f" or "map.victory-road-2f" or "map.victory-road-3f";
    private static bool VictoryRoadBoulderDefinition(FieldActorDefinition d,MapId map)
    {
        (int X,int Y,int Elevation,string Hide,string Trainer)? expected=(map.Value,d.SourceLocalId) switch
        {
            ("map.victory-road-1f",5)=>(7,18,3,"0","TRAINER_TYPE_NONE"),
            ("map.victory-road-1f",6)=>(4,12,3,"0","TRAINER_TYPE_NONE"),
            ("map.victory-road-1f",7)=>(16,3,3,"0","TRAINER_TYPE_NONE"),
            ("map.victory-road-2f",10)=>(8,7,3,"0","TRAINER_TYPE_NONE"),
            ("map.victory-road-2f",11)=>(6,17,3,"0","TRAINER_TYPE_NONE"),
            ("map.victory-road-2f",12)=>(33,19,3,VictoryRoadLowerHide,"TRAINER_TYPE_NONE"),
            ("map.victory-road-3f",7)=>(19,15,3,"0","TRAINER_TYPE_NONE"),
            ("map.victory-road-3f",8)=>(33,18,0,VictoryRoadUpperHide,VictoryRoadLowerHide),
            ("map.victory-road-3f",9)=>(35,13,3,"0","TRAINER_TYPE_NONE"),
            ("map.victory-road-3f",10)=>(32,5,3,"0","TRAINER_TYPE_NONE"),_=>null
        };
        return expected is {} e&&(d.X,d.Y,d.Elevation,d.HideFlag,d.TrainerType)==e&&!d.IsClone&&
            d.GraphicsId==FieldHmSourceRules.BoulderGraphics&&d.ScriptId=="EventScript_StrengthBoulder"&&
            d.MovementType=="MOVEMENT_TYPE_FACE_DOWN"&&d.TrainerSight==0;
    }
    private CampaignActor? AdjacentVictoryRoadBoulder(CampaignState state,Direction direction)
    {
        var p=state.World.Player;var (dx,dy)=Delta(direction);
        var actor=state.Actors.Values.SingleOrDefault(a=>a.Active&&!a.Invisible&&a.X==p.X+dx&&a.Y==p.Y+dy&&
            a.GraphicsId==FieldHmSourceRules.BoulderGraphics&&(a.Elevation==0||p.Elevation==0||a.Elevation==p.Elevation));
        if(actor is null)return null;
        var definition=_content.MapDefinition(p.MapId).Actors.SingleOrDefault(a=>a.LocalId==actor.LocalId);
        if(definition is null||!VictoryRoadBoulderDefinition(definition,p.MapId)||actor.SourceLocalId!=definition.SourceLocalId||
            actor.ScriptId!=definition.ScriptId||actor.HideFlag!=definition.HideFlag||actor.MovementType!=definition.MovementType||
            actor.HideFlag!="0"&&state.Script.Flags.GetValueOrDefault(actor.HideFlag))throw new InvalidDataException("victory_road_boulder_definition_changed");
        return actor;
    }
    private int VictoryRoadValue(string token)=>int.TryParse(token,out int n)?n:_content.TryConstant(token,out n)?n:throw new InvalidDataException("victory_road_constant_missing: "+token);
    private ImmutableSortedDictionary<string,FieldMetatileEdit> VictoryRoadPatch(MapId map,VictoryRoadSwitch button,bool pressed)
    {
        string label=pressed?button.Press:button.Barrier;
        var program=_content.FindProgram(label)??throw new InvalidDataException("victory_road_patch_program_missing");
        var instructions=program.Instructions.Where(i=>i.Opcode=="setmetatile").ToArray();
        if(map.Value!=button.Map||instructions.Length!=2)throw new InvalidDataException("victory_road_patch_scope");
        var result=ImmutableSortedDictionary<string,FieldMetatileEdit>.Empty;
        for(int index=0;index<2;index++)
        {
            var i=instructions[index];string tile="METATILE_Cave_"+(pressed?"Floor_Ledge_":"RockBarrier_")+(index==0?"Top":"Bottom");
            int x=button.BarrierX,y=button.BarrierY+index,id=VictoryRoadValue(tile),collision=pressed?0:3;
            if(i.Source.Path!=button.Source||i.Source.Symbol!=label||!i.Args.SequenceEqual(new[]{x.ToString(),y.ToString(),tile,pressed?"0":"1"})||
                _content.MetatileDefinition(map,id) is null)throw new InvalidDataException("victory_road_patch_source");
            var cell=_content.Maps[map].GetTile(x,y)??throw new InvalidDataException("victory_road_patch_cell");
            result=result.Add(x+":"+y,new(x,y,id,collision,cell.Elevation));
        }
        return result;
    }
    private bool TryApplyVictoryRoadFieldEffect(CampaignState state,StoryEffect effect,out CampaignState next)
    {
        next=state;if(!VictoryRoadFieldMap(state.World.Player.MapId))return false;
        var button=VictoryRoadSwitches.SingleOrDefault(b=>b.Map==state.World.Player.MapId.Value&&b.Source==effect.Source.Path&&
            (b.Press==effect.Source.Symbol||b.Barrier==effect.Source.Symbol));
        if(button is null)return false;
        if(effect.Opcode is not ("setmetatile" or "copyobjectxytoperm" or "special"))return false;
        var instruction=_content.FindProgram(effect.Source.Symbol)!.Instructions.SingleOrDefault(i=>i.Source==effect.Source&&i.Opcode==effect.Opcode);
        if(instruction is null||!instruction.Args.SequenceEqual(effect.Args))throw new InvalidDataException("victory_road_effect_source");
        bool pressed=effect.Source.Symbol==button.Press;
        if(effect.Opcode=="setmetatile")
        {
            if(pressed&&!state.Script.Locked||!pressed&&state.Script.Variables.GetValueOrDefault(button.Variable)==100)throw new InvalidDataException("victory_road_patch_context");
            var patch=VictoryRoadPatch(state.World.Player.MapId,button,pressed);string key=effect.Args[0]+":"+effect.Args[1];
            next=state with{MapEdits=(state.MapEdits??ImmutableSortedDictionary<string,FieldMetatileEdit>.Empty).SetItem(key,patch[key])};return true;
        }
        if(!pressed||!state.Script.Locked)throw new InvalidDataException("victory_road_switch_context");
        if(effect.Opcode=="copyobjectxytoperm")
        {
            int local=VictoryRoadValue(effect.Args[0]);
            var d=_content.MapDefinition(state.World.Player.MapId).Actors.SingleOrDefault(a=>a.SourceLocalId==local);
            if(!button.CopyIds.Contains(local)||d is null||!VictoryRoadBoulderDefinition(d,state.World.Player.MapId)||!state.Actors.TryGetValue(d.LocalId,out var actor))
                throw new InvalidDataException("victory_road_copy_actor_context");
            // Native CopyObjectEventCoordsToSave refuses an object that is out of the live pool.
            if(actor.Active)next=state with{Actors=state.Actors.SetItem(actor.LocalId,actor with{TemplateX=actor.X,TemplateY=actor.Y})};return true;
        }
        if(!effect.Args.SequenceEqual(new[]{"DrawWholeMapView"}))return false;
        if(VictoryRoadPatch(state.World.Player.MapId,button,true).Any(e=>state.MapEdits?.GetValueOrDefault(e.Key)!=e.Value))throw new InvalidDataException("victory_road_redraw_incomplete");
        return true;
    }
    private bool VictoryRoadBoulderFallSupported(CampaignState state,CampaignActor actor,TilePosition target)
    {
        if(state.World.Player.MapId.Value!="map.victory-road-3f"||actor.SourceLocalId!=8||
            target.MapId!=state.World.Player.MapId||target.X!=34||target.Y!=18)return false;
        var d=_content.MapDefinition(target.MapId).Actors.Single(a=>a.SourceLocalId==8);
        return VictoryRoadBoulderDefinition(d,target.MapId)&&actor.HideFlag==VictoryRoadUpperHide&&
            _content.Maps[target.MapId].SourceBehaviorAt(34,18)==FieldHmSourceRules.FallWarpBehavior;
    }
    private CampaignState CompleteVictoryRoadBoulderPush(CampaignState state,CampaignActor boulder)
    {
        if(!VictoryRoadFieldMap(state.World.Player.MapId))return state;
        var map=state.World.Player.MapId;var cell=EditedFieldMaps(state)[map].CellAt(boulder.X,boulder.Y)!;
        if(cell.SourceBehavior==FieldHmSourceRules.FallWarpBehavior)
        {
            if(!VictoryRoadBoulderFallSupported(state,boulder,new(map,boulder.X,boulder.Y,boulder.Elevation,boulder.Facing)))throw new InvalidDataException("victory_road_boulder_fall_context");
            long sequence=checked(state.Script.EffectSequence+1);
            // Remove the current object, then reveal the independently defined lower-floor object.
            return state with{Actors=state.Actors.SetItem(boulder.LocalId,boulder with{Active=false}),
                ObjectSlots=state.ObjectSlots?.RemoveRange(state.ObjectSlots.Where(s=>s.Value==boulder.LocalId).Select(s=>s.Key)),
                Script=state.Script with{EffectSequence=sequence,Flags=state.Script.Flags.SetItem(VictoryRoadUpperHide,true).SetItem(VictoryRoadLowerHide,false)},
                PresentationEffects=[new(sequence,"playse",map,["SE_FALL"])]};
        }
        if(cell.SourceBehavior!=VictoryRoadButtonBehavior)return state;
        var button=VictoryRoadSwitches.SingleOrDefault(b=>b.Map==map.Value&&b.X==boulder.X&&b.Y==boulder.Y)
            ??throw new InvalidDataException("victory_road_button_not_admitted");
        var trigger=_content.MapDefinition(map).CoordTriggers.SingleOrDefault(t=>t.X==boulder.X&&t.Y==boulder.Y);
        if(trigger is null||trigger.Variable!=button.Variable||trigger.Expected!=99||trigger.ScriptId!=button.Press||trigger.Elevation!=3)
            throw new InvalidDataException("victory_road_button_source");
        // HandleBoulderActivateVictoryRoadSwitch dispatches by the boulder's completed position.
        // It deliberately bypasses the player's VAR==99 coordinate-event condition.
        return Pump(StartStory(state,trigger.ScriptId));
    }
    private void ValidateVictoryRoadMapEntry(CampaignState state)
    {
        if(state.World.Player.MapId.Value!="map.route-23")return;
        var definition=_content.MapDefinition(state.World.Player.MapId);
        var program=_content.FindProgram("Route23_OnTransition");
        string[] expected=["clearflag "+VictoryRoadUpperHide,"setflag "+VictoryRoadLowerHide,
            ..VictoryRoadSwitches.Select(b=>"setvar "+b.Variable+", 0"),"end"];
        if(definition.Programs.OnTransition!="Route23_OnTransition"||program is null||program.Instructions.Any(i=>i.Source.Path!="data/maps/Route23/scripts.inc"||i.Source.Symbol!="Route23_OnTransition")||
            !program.Instructions.Select(i=>i.Opcode+(i.Args.IsEmpty?"":" "+string.Join(", ",i.Args))).SequenceEqual(expected))throw new InvalidDataException("victory_road_route23_reset_source");
    }
    private string? ValidateVictoryRoadFieldProgress(CampaignState state)
    {
        if(VictoryRoadSwitches.Any(b=>state.Script.Variables.GetValueOrDefault(b.Variable) is not (0 or 100)))return "save_victory_road_switch_scene";
        var map=state.World.Player.MapId;if(!VictoryRoadFieldMap(map))return null;
        if(state.Script.Flags.GetValueOrDefault(VictoryRoadUpperHide)==state.Script.Flags.GetValueOrDefault(VictoryRoadLowerHide))return "save_victory_road_fall_flags";
        var remaining=(state.MapEdits??ImmutableSortedDictionary<string,FieldMetatileEdit>.Empty).ToDictionary(p=>p.Key,p=>p.Value);
        foreach(var button in VictoryRoadSwitches.Where(b=>b.Map==map.Value))
        {
            bool pressed=state.Script.Variables.GetValueOrDefault(button.Variable)==100;
            var patch=VictoryRoadPatch(map,button,pressed);
            // Fresh source entry with scene100 retains the original open map grid; a
            // switch pressed on the saved current map retains both explicit edits.
            if(pressed&&patch.Keys.All(k=>!remaining.ContainsKey(k)))continue;
            foreach(var pair in patch)if(!remaining.Remove(pair.Key,out var actual)||actual!=pair.Value)return "save_victory_road_patch_mismatch";
        }
        if(remaining.Count!=0)return "save_victory_road_extra_patch";
        if(_content.Maps[map].SourceBehaviorAt(state.World.Player.X,state.World.Player.Y)==FieldHmSourceRules.FallWarpBehavior)return "save_victory_road_fall_tile";
        return null;
    }
    private IReadOnlyDictionary<MapId,ConnectedFieldMap> VictoryRoadWalkingMaps(IReadOnlyDictionary<MapId,ConnectedFieldMap> maps)
    {
        // Route23's collision0/elevation3 shallow shoreline is source land, including
        // dismount validation. Raw terrain stays available to Surf and encounters.
        return new CampaignMapView<ConnectedFieldMap>(maps,(id,map)=>
            VictoryRoadFieldMap(id) ? map with{Cells=map.Cells.Select(c=>c.SourceBehavior==VictoryRoadButtonBehavior?c with{SourceBehavior=0x08}:c).ToImmutableArray()} :
            id.Value=="map.route-23" ? map with{Cells=map.Cells.Select(c=>c is {SourceBehavior:0x17,Collision:0,Elevation:3}?c with{SourceBehavior=0x00}:c).ToImmutableArray()} : map);
    }
    private TilePosition VictoryRoadPlayerFallDestination(TilePosition hole)
    {
        if(hole.MapId.Value!="map.victory-road-3f"||hole.X!=34||hole.Y!=18||hole.Elevation!=3||
            _content.Maps[hole.MapId].SourceBehaviorAt(hole.X,hole.Y)!=FieldHmSourceRules.FallWarpBehavior)
            throw new InvalidDataException("victory_road_player_fall_source");
        var warp=_content.Maps[hole.MapId].FieldWarps.SingleOrDefault(w=>w.Index==4);
        if(warp is null||warp.X!=34||warp.Y!=18||warp.Elevation!=3||warp.DestinationMapId.Value!="map.victory-road-2f"||warp.DestinationWarpId!=8)
            throw new InvalidDataException("victory_road_player_fall_warp");
        var destination=_content.Maps[warp.DestinationMapId];var arrival=destination.FieldWarps.SingleOrDefault(w=>w.Index==8);
        if(arrival is null||arrival.X!=34||arrival.Y!=19||arrival.Elevation!=3||destination.GetTile(34,19) is not {Blocked:false,Elevation:3}||
            destination.SourceBehaviorAt(34,19) is not (0 or 0x08))throw new InvalidDataException("victory_road_player_fall_destination");
        return new(destination.Id,34,19,3,Direction.South);
    }
    private string? ValidateVictoryRoadBoulders(CampaignState state)
    {
        var map=state.World.Player.MapId;bool active=state.Script.Flags.GetValueOrDefault(FieldHmSourceRules.StrengthActiveFlag);
        foreach(var d in _content.MapDefinition(map).Actors.Where(a=>a.GraphicsId==FieldHmSourceRules.BoulderGraphics))
        {
            if(!VictoryRoadBoulderDefinition(d,map)||!state.Actors.TryGetValue(d.LocalId,out var a)||a.Invisible||a.MovementType!=d.MovementType)return "save_victory_road_boulder_identity";
            bool hidden=d.HideFlag!="0"&&state.Script.Flags.GetValueOrDefault(d.HideFlag);
            if(hidden&&a.Active)return "save_victory_road_hidden_boulder_active";
            if((a.TemplateX,a.TemplateY)!=(d.X,d.Y)&&!VictoryRoadSwitches.Any(b=>b.Map==map.Value&&b.CopyIds.Contains(d.SourceLocalId)&&state.Script.Variables.GetValueOrDefault(b.Variable)==100))return "save_victory_road_boulder_template";
            bool fallen=map.Value=="map.victory-road-3f"&&d.SourceLocalId==8&&hidden&&(a.X,a.Y)==(34,18);
            if(fallen){if(a.Elevation!=3)return "save_victory_road_boulder_tile";continue;}
            if((a.X,a.Y)!=(d.X,d.Y)&&!active)return "save_victory_road_boulder_moved_without_strength";
            var cell=EditedFieldMaps(state)[map].CellAt(a.X,a.Y);var template=EditedFieldMaps(state)[map].CellAt(a.TemplateX,a.TemplateY);
            bool Valid(ConnectedFieldCell? c)=>c is not null&&c.Collision==0&&c.SourceBehavior is not (FieldHmSourceRules.FallWarpBehavior or FieldHmSourceRules.NonAnimatedDoorBehavior)&&c.Elevation is 0 or 3;
            if(!Valid(cell)||!Valid(template)||a.Elevation is not (0 or 3)||a.Elevation==0&&d.Elevation!=0)return "save_victory_road_boulder_tile";
            if(!a.Active)continue;
            bool Same(int e)=>a.Elevation==0||e==0||e==a.Elevation;
            if(state.World.Player.X==a.X&&state.World.Player.Y==a.Y&&Same(state.World.Player.Elevation)||state.Actors.Values.Any(other=>other.LocalId!=a.LocalId&&other.Active&&other.X==a.X&&other.Y==a.Y&&Same(other.Elevation)))return "save_victory_road_boulder_overlap";
        }
        return null;
    }
}
