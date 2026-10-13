using System.Collections.Immutable;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.World;

namespace Game.Application;

public sealed partial class CampaignSession
{
    private const string PokemonLeagueSharedSource="data/scripts/pokemon_league.inc";
    private const string PokemonLeagueHallSource="data/scripts/hall_of_fame.inc";
    private const string PokemonLeagueHallEntry="PokemonLeague_HallOfFame_EventScript_EnterRoom";
    private IReadOnlyDictionary<MapId,IFieldMap> PokemonLeagueMovementMaps(IReadOnlyDictionary<MapId,IFieldMap> source,CampaignState state,CampaignCommand command)
    {
        if(!_content.PokemonLeagueEnabled)return source;
        var p=state.World.Player;int room=PokemonLeagueRoomIndex(p.MapId);
        Dictionary<MapId,IFieldMap>? maps=null;
        if(room is >=0 and <=3&&state.MapEdits is {Count:>0})
        {
            maps=source.ToDictionary(x=>x.Key,x=>x.Value);
            maps[p.MapId]=new RocketEditedMap(source[p.MapId],state.MapEdits,_content);
        }
        // The first three original room warps place the avatar on a blocked bottom
        // border cell. Their on-frame script walks up five tiles before closing it.
        // Admit that authenticated destination only for this incoming warp; never
        // turn the same cell into walkable ground during ordinary room movement.
        if(command.Kind=="move"&&(room is 0 or 1||p.MapId.Value==PokemonLeagueCenterMap))
        {
            var(dx,dy)=Delta(command.Direction);
            foreach(var warp in source[p.MapId].FieldWarps.Where(w=>(w.X==p.X+dx&&w.Y==p.Y+dy||w.X==p.X&&w.Y==p.Y)&&w.DestinationWarpId==0))
            {
                int nextRoom=PokemonLeagueRoomIndex(warp.DestinationMapId);
                if(nextRoom!=room+1||nextRoom is <0 or >2)continue;
                var destination=source[warp.DestinationMapId];var arrival=destination.FieldWarps.Single(w=>w.Index==0);
                if(arrival.X!=6||arrival.Y!=12||arrival.Elevation!=0||destination.SourceBehaviorAt(6,12)!=0||destination.GetTile(6,12) is not {Blocked:true,Elevation:0})
                    throw new InvalidDataException("pokemon_league_source_entry_cell");
                maps??=source.ToDictionary(x=>x.Key,x=>x.Value);
                maps[destination.Id]=new PokemonLeagueArrivalMap(destination);
            }
        }
        return maps??source;
    }
    private sealed class PokemonLeagueArrivalMap(IFieldMap source):IFieldMap
    {
        public MapId Id=>source.Id;public int Width=>source.Width;public int Height=>source.Height;
        public ImmutableArray<FieldWarp> FieldWarps=>source.FieldWarps;
        public int? SourceBehaviorAt(int x,int y)=>source.SourceBehaviorAt(x,y);
        public int? SourceEncounterTypeAt(int x,int y)=>source.SourceEncounterTypeAt(x,y);
        public TileCell? GetTile(int x,int y)=>x==6&&y==12?source.GetTile(x,y)! with{Blocked=false}:source.GetTile(x,y);
    }
    private int PokemonLeagueValue(string token)=>int.TryParse(token,out int n)?n:_content.TryConstant(token,out n)?n:throw new InvalidDataException("pokemon_league_constant_missing: "+token);
    private ImmutableSortedDictionary<string,FieldMetatileEdit> PokemonLeaguePatch(MapId map,string label)
    {
        var program=_content.FindProgram(label)??throw new InvalidDataException("pokemon_league_patch_missing");
        var result=ImmutableSortedDictionary<string,FieldMetatileEdit>.Empty;
        foreach(var instruction in program.Instructions.Where(i=>i.Opcode=="setmetatile"))
        {
            var a=instruction.Args;
            if(a.Length!=4||!a[2].StartsWith("METATILE_PokemonLeague_",StringComparison.Ordinal))throw new InvalidDataException("pokemon_league_patch_source");
            int x=PokemonLeagueValue(a[0]),y=PokemonLeagueValue(a[1]),tile=PokemonLeagueValue(a[2]),collision=PokemonLeagueValue(a[3])==0?0:3;
            var cell=_content.Maps[map].GetTile(x,y)??throw new InvalidDataException("pokemon_league_patch_bounds");
            if(_content.MetatileDefinition(map,tile) is null)throw new InvalidDataException("pokemon_league_patch_tile");
            result=result.SetItem(x+":"+y,new(x,y,tile,collision,cell.Elevation));
        }
        if(result.Count is not (2 or 6 or 8))throw new InvalidDataException("pokemon_league_patch_count");
        return result;
    }
    private ImmutableSortedDictionary<string,FieldMetatileEdit>? PokemonLeagueExpectedEdits(CampaignState state)
    {
        int room=PokemonLeagueRoomIndex(state.World.Player.MapId);if(room is <0 or >3)return null;
        var result=ImmutableSortedDictionary<string,FieldMetatileEdit>.Empty;var map=state.World.Player.MapId;
        if(state.Script.Variables.GetValueOrDefault("VAR_MAP_SCENE_POKEMON_LEAGUE")==room+1)
            result=result.SetItems(PokemonLeaguePatch(map,room==3?"PokemonLeague_LancesRoom_EventScript_SetEntryClosed":"PokemonLeague_EventScript_CloseEntry"));
        if(state.Script.Flags.GetValueOrDefault(PokemonLeagueDefeatedFlags[room]))
            result=result.SetItems(PokemonLeaguePatch(map,room==3?"PokemonLeague_EventScript_SetDoorOpenLance":"PokemonLeague_EventScript_SetDoorOpen"));
        return result;
    }
    private bool TryApplyPokemonLeagueEffect(CampaignState state,StoryEffect effect,out CampaignState next)
    {
        next=state;if(!_content.PokemonLeagueEnabled)return false;
        var a=effect.Args;string path=effect.Source.Path,symbol=effect.Source.Symbol;int room=PokemonLeagueRoomIndex(state.World.Player.MapId);
        bool shared=path==PokemonLeagueSharedSource,hall=path==PokemonLeagueHallSource;
        bool roomSource=path.StartsWith("data/maps/PokemonLeague_",StringComparison.Ordinal)&&PokemonLeagueMap(state.World.Player.MapId);
        bool exterior=path=="data/maps/IndigoPlateau_Exterior/scripts.inc"&&state.World.Player.MapId.Value=="map.indigo-plateau-exterior";
        bool center=path=="data/maps/IndigoPlateau_PokemonCenter_1F/scripts.inc"&&state.World.Player.MapId.Value==PokemonLeagueCenterMap;
        if(!(shared||hall||roomSource||exterior||center))return false;
        void Source()
        {
            var instruction=_content.FindProgram(symbol)?.Instructions.SingleOrDefault(i=>i.Source==effect.Source&&i.Opcode==effect.Opcode);
            if(instruction is null)throw new InvalidDataException("pokemon_league_effect_source");
        }
        if(effect.Opcode=="setmetatile")
        {
            Source();if(room is <0 or >3)throw new InvalidDataException("pokemon_league_patch_map");
            string[] labels=room==3?["PokemonLeague_LancesRoom_EventScript_SetEntryClosed","PokemonLeague_EventScript_OpenDoorLance","PokemonLeague_EventScript_SetDoorOpenLance"]:
                ["PokemonLeague_EventScript_CloseEntry","PokemonLeague_EventScript_SetDoorOpen"];
            if(!labels.Contains(symbol)||a.Length!=4)throw new InvalidDataException("pokemon_league_patch_label");
            var patch=PokemonLeaguePatch(state.World.Player.MapId,symbol);string key=a[0]+":"+a[1];
            if(!patch.TryGetValue(key,out var tile)||tile.SourceMetatileId!=PokemonLeagueValue(a[2])||tile.Collision!=(PokemonLeagueValue(a[3])==0?0:3))throw new InvalidDataException("pokemon_league_patch_arguments");
            if(room==0&&symbol=="PokemonLeague_EventScript_CloseEntry"&&state.Script.Variables.GetValueOrDefault("VAR_MAP_SCENE_POKEMON_LEAGUE")==0)
                state=BeginPokemonLeagueChallenge(state);
            next=state with{MapEdits=(state.MapEdits??ImmutableSortedDictionary<string,FieldMetatileEdit>.Empty).SetItem(key,tile)};return true;
        }
        if(effect.Opcode=="goto_if_defeated"&&room==4)
        {
            Source();if(a.Length!=2||!PokemonLeagueChampionSymbols.Contains(a[0])||a[1]!="PokemonLeague_ChampionsRoom_EventScript_StopSceneTrigger")throw new InvalidDataException("pokemon_league_champion_resume_source");
            if(state.BattledTrainers.Contains("pret:"+a[0]))next=state with{Script=state.Script with{Stack=state.Script.Stack.SetItem(state.Script.Stack.Length-1,new(a[1],0))}};
            return true;
        }
        if(effect.Opcode=="cleartrainerflag"&&hall)
        {
            Source();if(symbol!="EventScript_ResetEliteFour"||a.Length!=1||!PokemonLeagueChampionSymbols.Contains(a[0])||state.PokemonLeagueCompletion is not{FieldEffectCompleted:true})throw new InvalidDataException("pokemon_league_reset_source");
            next=state with{BattledTrainers=state.BattledTrainers.Remove("pret:"+a[0])};
            if(a[0]==PokemonLeagueChampionSymbols[^1])next=ResetPokemonLeague(next,"hall-of-fame");
            return true;
        }
        if(effect.Opcode=="dofieldeffect"&&symbol==PokemonLeagueHallEntry)
        {
            Source();if(state.World.Player.MapId.Value!=PokemonLeagueHallMap||!a.SequenceEqual(new[]{"FLDEFF_HALL_OF_FAME_RECORD"})||
                state.PokemonLeagueCompletion is not null||PokemonLeagueDefeatedFlags.Any(f=>!state.Script.Flags.GetValueOrDefault(f))||state.Party.Length is <1 or >6||
                (state.HallOfFame?.Teams??[]).Any(t=>t.ChallengeNumber==PokemonLeagueChallenge(state))||
                !(state.ForestTrainerHistory??[]).Any(h=>h.PokemonLeagueChallenge==PokemonLeagueChallenge(state)&&h.TrainerId==PokemonLeagueTrainerAt(state,4).Value&&h.Outcome==Game.Domain.WildBattle.WildBattleOutcome.Won))
                throw new InvalidDataException("pokemon_league_record_effect_context");
            int challenge=PokemonLeagueChallenge(state);
            next=state with{PokemonLeagueCompletion=new(CampaignPokemonLeagueCompletionStage.RecordingFieldEffect,"hall-of-fame:"+challenge,challenge,state.Party.Length,false,false)};return true;
        }
        if(effect.Opcode=="waitfieldeffect"&&symbol==PokemonLeagueHallEntry)
        {
            Source();if(!a.SequenceEqual(new[]{"FLDEFF_HALL_OF_FAME_RECORD"})||state.PokemonLeagueCompletion is not{Stage:CampaignPokemonLeagueCompletionStage.RecordingFieldEffect,FieldEffectCompleted:false})throw new InvalidDataException("pokemon_league_record_wait_context");
            return true;
        }
        if(effect.Opcode=="fadescreenspeed"&&symbol==PokemonLeagueHallEntry)
        {
            Source();if(!a.SequenceEqual(new[]{"FADE_TO_BLACK","24"}))throw new InvalidDataException("pokemon_league_fade_source");
            // BeginNormalPaletteFade delay24: BG reaches16 at208, OBJ at209, five finishing callbacks.
            next=state with{ScreenIsBlack=true,PresentationTicks=214};return true;
        }
        if(effect.Opcode=="specialvar")
        {
            Source();
            if(a.Length==2&&a[0]=="VAR_RESULT"&&a[1]=="GetStarterSpecies"&&room==4)
            {
                int index=state.Script.Variables.GetValueOrDefault("VAR_STARTER_MON");
                var choice=_content.PokemonContent!.StarterChoices.Single(c=>c.Index==index);
                next=state with{Script=state.Script with{Variables=state.Script.Variables.SetItem("VAR_RESULT",choice.SourceSpeciesId)}};return true;
            }
            if(a.SequenceEqual(new[]{"VAR_RESULT","IsNationalPokedexEnabled"})&&(hall||center))
            {
                if(!_content.NationalDexEnabled&&state.Script.Flags.GetValueOrDefault("FLAG_SYS_NATIONAL_DEX"))throw new InvalidDataException("pokemon_league_national_dex_not_admitted");
                next=state with{Script=state.Script with{Variables=state.Script.Variables.SetItem("VAR_RESULT",NationalDexEnabledIn(state)?1:0)}};return true;
            }
            return false;
        }
        if(effect.Opcode!="special")return false;
        Source();if(a.Length!=1)return false;
        switch(a[0])
        {
            case "DoPokemonLeagueLightingEffect":
                if(!shared||room<0||state.Script.Variables.GetValueOrDefault("VAR_0x8004")!=room)throw new InvalidDataException("pokemon_league_lighting_context");
                return true; // Visual native effect is retained in PresentationEffects.
            case "DrawWholeMapView":
                if(room is <0 or >3)throw new InvalidDataException("pokemon_league_redraw_context");return true;
            case "Script_TryGainNewFanFromCounter":
                if(room!=3||symbol!="PokemonLeague_LancesRoom_EventScript_DefeatedLance"||state.Script.Variables.GetValueOrDefault("VAR_0x8004")!=0)throw new InvalidDataException("pokemon_league_fan_counter_context");
                // Fan-club state is outside the inherited model; preserve the invocation,
                // explicitly mark this deferred subsystem in the completed archive.
                return true;
            case "Script_UpdateTrainerFanClubGameClear":
                if(!hall||symbol!="EventScript_SetDefeatedEliteFourFlagsVars")throw new InvalidDataException("pokemon_league_fan_clear_context");return true;
            case "EnterHallOfFame":
                if(symbol!=PokemonLeagueHallEntry||state.World.Player.MapId.Value!=PokemonLeagueHallMap)throw new InvalidDataException("pokemon_league_hof_source");
                next=EnterPokemonLeagueHallOfFame(state);return true;
            case "SpawnCameraObject":case "RemoveCameraObject":
                if(!exterior||state.PokemonLeagueCompletion is not{Stage:CampaignPokemonLeagueCompletionStage.ExteriorCredits,SaveSucceeded:true})throw new InvalidDataException("pokemon_league_credits_camera_context");
                return true; // Render-only camera ownership is represented by this source effect.
            case "DoCredits":
                if(!exterior||state.PokemonLeagueCompletion is not{Stage:CampaignPokemonLeagueCompletionStage.ExteriorCredits,SaveSucceeded:true} completion||
                    state.Script.Variables.GetValueOrDefault("VAR_MAP_SCENE_INDIGO_PLATEAU_EXTERIOR")!=0)throw new InvalidDataException("pokemon_league_credits_context");
                next=state with{PokemonLeagueCompletion=completion with{Stage=CampaignPokemonLeagueCompletionStage.Credits}};return true;
            default:return false;
        }
    }
}
