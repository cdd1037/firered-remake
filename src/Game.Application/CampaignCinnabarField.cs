using System.Collections.Immutable;
using Game.Domain;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.World;

namespace Game.Application;

/// <summary>The source hole script owns departure; the field callback owns landing.</summary>
public sealed record CampaignCinnabarFall(string PresentationId,TilePosition From,TilePosition Hole,TilePosition To,bool Landing=false);

public sealed partial class CampaignSession
{
    private const string CinnabarGymFieldMap="map.cinnabar-island-gym";
    private const string CinnabarGymFieldSource="data/maps/CinnabarIsland_Gym/scripts.inc";
    private const string MansionSwitchSource="data/scripts/pokemon_mansion.inc";
    private const string MansionSwitchFlag="FLAG_POKEMON_MANSION_SWITCH_STATE";
    private const string MansionFallSource="data/scripts/hole.inc";
    private const string MansionFallProgram="EventScript_DoFallWarp";
    private static string? MansionFloor(MapId map)=>map.Value switch
    {
        "map.pokemon-mansion-1f"=>"1F","map.pokemon-mansion-2f"=>"2F",
        "map.pokemon-mansion-3f"=>"3F","map.pokemon-mansion-b1f"=>"B1F",_=>null
    };
    private static bool CinnabarFieldMap(MapId map)=>MansionFloor(map) is not null||map.Value==CinnabarGymFieldMap;
    private int CinnabarFieldValue(string token)=>int.TryParse(token,out int n)?n:
        _content.TryConstant(token,out n)?n:throw new InvalidDataException("cinnabar_field_constant_missing: "+token);

    // Extract only authenticated source instructions. Quiz choices/branches still run in StoryRules.
    private ImmutableSortedDictionary<string,FieldMetatileEdit> CinnabarPatch(MapId map,string label)
    {
        var floor=MansionFloor(map);bool mansion=floor is not null;
        string source=mansion?MansionSwitchSource:CinnabarGymFieldSource;
        int expected;
        if(mansion)
        {
            if(label!="PokemonMansion_EventScript_PressSwitch_"+floor&&label!="PokemonMansion_EventScript_ResetSwitch_"+floor)
                throw new InvalidDataException("mansion_patch_label");
            expected=floor switch{"1F"=>25,"2F"=>17,"3F"=>12,_=>24};
        }
        else
        {
            if(map.Value!=CinnabarGymFieldMap||!Enumerable.Range(1,6).Any(n=>label=="CinnabarIsland_Gym_EventScript_OpenDoor"+n))
                throw new InvalidDataException("cinnabar_gym_patch_label");
            expected=label.EndsWith('4')?3:7;
        }
        var program=_content.FindProgram(label)??throw new InvalidDataException("cinnabar_patch_program_missing");
        var edits=ImmutableSortedDictionary<string,FieldMetatileEdit>.Empty;
        foreach(var i in program.Instructions.Where(i=>i.Opcode=="setmetatile"))
        {
            if(i.Source.Path!=source||i.Source.Symbol!=label||i.Args.Length!=4||
                !i.Args[2].StartsWith(mansion?"METATILE_PokemonMansion_":"METATILE_CinnabarGym_",StringComparison.Ordinal))
                throw new InvalidDataException("cinnabar_patch_source");
            int x=CinnabarFieldValue(i.Args[0]),y=CinnabarFieldValue(i.Args[1]),id=CinnabarFieldValue(i.Args[2]),collision=CinnabarFieldValue(i.Args[3]);
            var cell=_content.Maps[map].GetTile(x,y)??throw new InvalidDataException("cinnabar_patch_cell");
            if(collision is not (0 or 1)||_content.MetatileDefinition(map,id) is null)throw new InvalidDataException("cinnabar_patch_metatile");
            edits=edits.Add(x+":"+y,new(x,y,id,collision==0?0:3,cell.Elevation));
        }
        if(edits.Count!=expected)throw new InvalidDataException("cinnabar_patch_count");
        return edits;
    }

    private ImmutableSortedDictionary<string,FieldMetatileEdit> CinnabarExpectedEdits(CampaignState state)
    {
        var map=state.World.Player.MapId;
        if(MansionFloor(map) is {} floor)return CinnabarPatch(map,"PokemonMansion_EventScript_"+
            (state.Script.Flags.GetValueOrDefault(MansionSwitchFlag)?"PressSwitch_":"ResetSwitch_")+floor);
        var edits=ImmutableSortedDictionary<string,FieldMetatileEdit>.Empty;
        if(map.Value==CinnabarGymFieldMap)
            for(int n=1;n<=6;n++)
                if(state.Script.Flags.GetValueOrDefault("FLAG_CINNABAR_GYM_QUIZ_"+n)||state.Script.Flags.GetValueOrDefault("FLAG_DEFEATED_BLAINE"))
                    edits=edits.AddRange(CinnabarPatch(map,"CinnabarIsland_Gym_EventScript_OpenDoor"+n));
        return edits;
    }

    private bool TryApplyCinnabarFieldEffect(CampaignState state,StoryEffect effect,out CampaignState next)
    {
        next=state;        if(effect.Opcode=="special"&&effect.Source.Path==MansionFallSource&&effect.Args.SequenceEqual(["DoFallWarp"]))
        {
            if(effect.Source.Symbol!=MansionFallProgram||state.CinnabarFall is not {Landing:false} fall||
                state.World.Player!=fall.Hole||state.PlayerVisible||!state.Script.Locked||CinnabarFallDestination(fall.Hole)!=fall.To)
                throw new InvalidDataException("cinnabar_fall_effect_context");
            // CB2_LoadMap resets the old script context. Landing is not a walking step or encounter.
            var moved=state with{World=state.World with{Player=fall.To},IsCycling=false,
                Script=state.Script with{Stack=[],Wait=null,Completed=true,Locked=false},
                Exploration=ResetEncounterModifiers(state.Exploration!),Motions=[],PresentationEffects=[],PresentationTicks=0};
            moved=Pump(EnterMap(moved,Direction.South));
            next=moved with{CinnabarFall=fall with{Landing=true},PlayerVisible=true};
            return true;
        }
        bool mansion=effect.Source.Path==MansionSwitchSource;
        bool gym=effect.Source.Path==CinnabarGymFieldSource;
        if(effect.Opcode=="setmetatile"&&(mansion||gym))
        {
            var map=state.World.Player.MapId;var patch=CinnabarPatch(map,effect.Source.Symbol);
            var instruction=_content.FindProgram(effect.Source.Symbol)!.Instructions.SingleOrDefault(i=>i.Source==effect.Source&&i.Opcode==effect.Opcode);
            if(instruction is null||!instruction.Args.SequenceEqual(effect.Args))throw new InvalidDataException("cinnabar_patch_effect_mismatch");
            if(mansion)
            {
                bool pressed=effect.Source.Symbol.Contains("_PressSwitch_",StringComparison.Ordinal);
                if(pressed!=state.Script.Flags.GetValueOrDefault(MansionSwitchFlag))throw new InvalidDataException("mansion_patch_flag_mismatch");
            }
            else
            {
                string number=effect.Source.Symbol[^1..];
                bool complete=state.Script.Stack.Any(f=>f.ProgramId=="CinnabarIsland_Gym_EventScript_Quiz"+number+"Complete");
                if(!(complete&&state.Script.Locked)&&!state.Script.Flags.GetValueOrDefault("FLAG_CINNABAR_GYM_QUIZ_"+number)&&!state.Script.Flags.GetValueOrDefault("FLAG_DEFEATED_BLAINE"))
                    throw new InvalidDataException("cinnabar_gym_patch_context");
            }
            string key=CinnabarFieldValue(effect.Args[0])+":"+CinnabarFieldValue(effect.Args[1]);
            next=state with{MapEdits=(state.MapEdits??ImmutableSortedDictionary<string,FieldMetatileEdit>.Empty).SetItem(key,patch[key])};return true;
        }
        if(effect.Opcode=="special"&&effect.Args.SequenceEqual(["DrawWholeMapView"]))
        {
            var map=state.World.Player.MapId;
            bool statue=MansionFloor(map) is {} floor&&effect.Source.Path=="data/maps/PokemonMansion_"+floor+"/scripts.inc"&&
                effect.Source.Symbol.StartsWith("PokemonMansion_"+floor+"_EventScript_Statue",StringComparison.Ordinal);
            bool quiz=gym&&map.Value==CinnabarGymFieldMap&&Enumerable.Range(1,6).Any(n=>effect.Source.Symbol=="CinnabarIsland_Gym_EventScript_Quiz"+n+"Complete");
            if(!statue&&!quiz)return false;
            if(!state.Script.Locked)throw new InvalidDataException("cinnabar_redraw_unlocked");
            var required=statue?CinnabarExpectedEdits(state):CinnabarPatch(map,"CinnabarIsland_Gym_EventScript_OpenDoor"+effect.Source.Symbol[^9]);
            if(required.Any(p=>state.MapEdits?.GetValueOrDefault(p.Key)!=p.Value))throw new InvalidDataException("cinnabar_redraw_incomplete");
            return true;
        }
        return false;
    }

    private string? ValidateCinnabarFieldProgress(CampaignState state)
    {
        if(state.CinnabarFall is not null)return "save_cinnabar_fall_active";
        var map=state.World.Player.MapId;
        if(!CinnabarFieldMap(map))return null;
        if(_content.Maps[map].SourceBehaviorAt(state.World.Player.X,state.World.Player.Y)==0x66)return "save_cinnabar_fall_tile";
        var expected=CinnabarExpectedEdits(state);var actual=state.MapEdits??ImmutableSortedDictionary<string,FieldMetatileEdit>.Empty;
        return actual.SequenceEqual(expected)?null:"save_cinnabar_patch_mismatch";
    }

    // Walking owns its ordinary collision/elevation checks. Mask only the source hole behavior
    // while completing that step; the post-coordinate-event hook starts the source fall script.
    private IReadOnlyDictionary<MapId,ConnectedFieldMap> CinnabarWalkingMaps(IReadOnlyDictionary<MapId,ConnectedFieldMap> maps)
    {
        return new CampaignMapView<ConnectedFieldMap>(maps,(id,map)=>id.Value is "map.pokemon-mansion-3f" or "map.victory-road-3f"
            ? map with{Cells=map.Cells.Select(c=>c.SourceBehavior==0x66?c with{SourceBehavior=0x08}:c).ToImmutableArray()} : map);
    }
    private IReadOnlyDictionary<MapId,IFieldMap> CinnabarMovementMaps(IReadOnlyDictionary<MapId,IFieldMap> maps,CampaignState state)
    {
        if(!CinnabarFieldMap(state.World.Player.MapId)&&!(VictoryRoadFieldMap(state.World.Player.MapId)))return maps;
        var id=state.World.Player.MapId;
        return ReplaceMap(maps,id,(IFieldMap)new CinnabarMovementMap(maps[id],state.MapEdits,_content));
    }
    private sealed class CinnabarMovementMap(IFieldMap original,ImmutableSortedDictionary<string,FieldMetatileEdit>? edits,ICampaignCatalog content):IFieldMap
    {
        public MapId Id=>original.Id;public int Width=>original.Width;public int Height=>original.Height;
        public ImmutableArray<FieldWarp> FieldWarps=>original.FieldWarps;
        public int? SourceEncounterTypeAt(int x,int y)=>edits?.TryGetValue(x+":"+y,out var edit)==true?content.MetatileDefinition(Id,edit.SourceMetatileId)!.EncounterType:original.SourceEncounterTypeAt(x,y);
        public int? SourceBehaviorAt(int x,int y)
        {
            int? b=edits?.TryGetValue(x+":"+y,out var edit)==true?content.MetatileDefinition(Id,edit.SourceMetatileId)!.Behavior:original.SourceBehaviorAt(x,y);
            if(VictoryRoadFieldMap(Id)&&b==VictoryRoadButtonBehavior)return 0x08;
            return (Id.Value=="map.pokemon-mansion-3f"||Id.Value=="map.victory-road-3f")&&b==0x66?0x08:b;
        }
        public TileCell? GetTile(int x,int y)
        {
            var cell=original.GetTile(x,y);
            if(edits?.TryGetValue(x+":"+y,out var edit)==true)
                return new(edit.Collision!=0,edit.Elevation,SourceBehaviorAt(x,y) is 0 or 0x08?TileBehavior.Floor:TileBehavior.Unsupported);
            return cell;
        }
    }

    private TilePosition CinnabarFallDestination(TilePosition hole)
    {
        if(hole.MapId.Value=="map.victory-road-3f")return VictoryRoadPlayerFallDestination(hole);
        if(hole.MapId.Value!="map.pokemon-mansion-3f"||hole.Y!=18||hole.X is not (18 or 19 or 20 or 23 or 24))
            throw new InvalidDataException("cinnabar_fall_source_position");
        var map=_content.Maps[hole.MapId];
        if(map.SourceBehaviorAt(hole.X,hole.Y)!=0x66||map.GetTile(hole.X,hole.Y) is not {Blocked:false,Elevation:3}||hole.Elevation!=3)
            throw new InvalidDataException("cinnabar_fall_source_behavior");
        var warp=map.FieldWarps.SingleOrDefault(w=>w.X==hole.X&&w.Y==hole.Y&&w.Elevation==hole.Elevation)
            ??throw new InvalidDataException("cinnabar_fall_source_warp");
        string destination=hole.X<23?"map.pokemon-mansion-1f":"map.pokemon-mansion-2f";
        int target=hole.X==18?7:hole.X<23?8:4;
        if(warp.DestinationMapId.Value!=destination||warp.DestinationWarpId!=target)throw new InvalidDataException("cinnabar_fall_destination_contract");
        var dest=_content.Maps[warp.DestinationMapId];var arrival=dest.FieldWarps.SingleOrDefault(w=>w.Index==target)
            ??throw new InvalidDataException("cinnabar_fall_destination_warp");
        var cell=dest.GetTile(arrival.X,arrival.Y);
        if(cell is not {Blocked:false}||dest.SourceBehaviorAt(arrival.X,arrival.Y) is not (0 or 0x08)||
            arrival.Elevation!=0&&arrival.Elevation!=cell.Elevation)throw new InvalidDataException("cinnabar_fall_destination_cell");
        return new(dest.Id,arrival.X,arrival.Y,cell.Elevation,Direction.South);
    }
    private CampaignState? TryStartCinnabarFall(CampaignState previous,CampaignState moved)
    {
        var hole=moved.World.Player;
        if(hole.MapId.Value!="map.pokemon-mansion-3f"&&!(hole.MapId.Value=="map.victory-road-3f")||
            previous.World.Player.MapId!=hole.MapId||previous.World.StepCount==moved.World.StepCount||
            _content.Maps[hole.MapId].SourceBehaviorAt(hole.X,hole.Y)!=0x66)return null;
        if(moved.CinnabarFall is not null||!moved.Script.Completed||moved.Script.Wait is not null||moved.Script.Locked||
            moved.World.StepCount!=previous.World.StepCount+1)throw new InvalidDataException("cinnabar_fall_start_context");
        var destination=CinnabarFallDestination(hole);
        var script=_content.FindProgram(MansionFallProgram)??throw new InvalidDataException("cinnabar_fall_script_missing");
        if(script.Instructions.Any(i=>i.Source.Path!=MansionFallSource)||
            !script.Instructions.Where(i=>i.Opcode=="delay").Select(i=>i.Args.Single()).SequenceEqual(["20","60"]))
            throw new InvalidDataException("cinnabar_fall_script_source");
        // This script returns before happiness/poison/Safari/Repel and encounter checks.
        var next=moved with{CinnabarFall=new("cinnabar-fall:"+moved.World.Revision,previous.World.Player,hole,destination)};
        return Pump(StartStory(next,MansionFallProgram));
    }
    private Decision<CampaignState>? TryDecideCinnabarFall(CampaignState state,CampaignCommand command)
    {
        if(state.CinnabarFall is not {} fall)return null;
        if(!fall.Landing)
            return command.Kind=="ack"?null:new(CommandStatus.Rejected,state,"cinnabar_fall_departure_pending");
        if(command.Kind!="cinnabar_fall_ack"||command.Text!=fall.PresentationId)
            return new(CommandStatus.Rejected,state,"cinnabar_fall_landing_pending");
        if(state.World.Player!=fall.To||state.Script.Wait is not null||!state.Script.Completed)
            return new(CommandStatus.Fault,state,"cinnabar_fall_landing_context");
        return new(CommandStatus.Accepted,state with{CinnabarFall=null,PresentationTicks=0,Motions=[],PresentationEffects=[]});
    }
}
