using System.Collections.Immutable;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Trainers;
using Game.Domain.WildBattle;
using Game.Domain.World;

namespace Game.Application;

public sealed record CampaignPokemonLeagueReset(int ChallengeNumber, int TrainerHistoryCount, string Reason);
public sealed record CampaignPokemonLeagueProgress(int ChallengeNumber, ImmutableArray<CampaignPokemonLeagueReset> Resets);

/// <summary>The original first challenge. Fought flags are source state; a challenge receipt
/// distinguishes a legitimate later attempt from settling the same victory twice.</summary>
public sealed partial class CampaignSession
{
    public const string PokemonLeagueRulesVersion="firered-pokemon-league-first-clear-v1";
    private const string PokemonLeagueCenterMap="map.indigo-plateau-pokemon-center-1f";
    private const string PokemonLeagueHealingLocation="HEAL_LOCATION_INDIGO_PLATEAU";
    private const string PokemonLeagueHallMap="map.pokemon-league-hall-of-fame";
    private static readonly string[] PokemonLeagueRooms=["loreleis","brunos","agathas","lances","champions"];
    private static readonly string[] PokemonLeagueNames=["Lorelei","Bruno","Agatha","Lance"];
    private static readonly string[] PokemonLeagueDefeatedFlags=["FLAG_DEFEATED_LORELEI","FLAG_DEFEATED_BRUNO","FLAG_DEFEATED_AGATHA","FLAG_DEFEATED_LANCE","FLAG_DEFEATED_CHAMP"];
    private static readonly string[] PokemonLeagueChampionSymbols=["TRAINER_CHAMPION_FIRST_SQUIRTLE","TRAINER_CHAMPION_FIRST_BULBASAUR","TRAINER_CHAMPION_FIRST_CHARMANDER","TRAINER_CHAMPION_REMATCH_SQUIRTLE","TRAINER_CHAMPION_REMATCH_BULBASAUR","TRAINER_CHAMPION_REMATCH_CHARMANDER"];
    private static int PokemonLeagueRoomIndex(MapId map)=>Array.IndexOf(PokemonLeagueRooms,map.Value.StartsWith("map.pokemon-league-",StringComparison.Ordinal)&&map.Value.EndsWith("-room",StringComparison.Ordinal)?map.Value[19..^5]:"");
    private static bool PokemonLeagueMap(MapId map)=>map.Value is PokemonLeagueCenterMap or PokemonLeagueHallMap||PokemonLeagueRoomIndex(map)>=0;
    private bool IsPokemonLeagueTrainer(TrainerId id)=>_content.PokemonLeagueEnabled&&_content.PokemonLeagueTrainerParties?.Trainers.ContainsKey(id)==true;
    private bool IsPokemonLeagueChampion(TrainerId id)=>IsPokemonLeagueTrainer(id)&&id.Value.StartsWith("pret:TRAINER_CHAMPION_FIRST_",StringComparison.Ordinal);
    private static int PokemonLeagueChallenge(CampaignState state)=>state.PokemonLeague?.ChallengeNumber??1;
    private static TrainerId PokemonLeagueChampion(int starter)=>new(starter switch
    {
        0=>"pret:TRAINER_CHAMPION_FIRST_CHARMANDER",1=>"pret:TRAINER_CHAMPION_FIRST_BULBASAUR",2=>"pret:TRAINER_CHAMPION_FIRST_SQUIRTLE",
        _=>throw new InvalidDataException("pokemon_league_starter_invalid")
    });
    private TrainerId PokemonLeagueTrainerAt(CampaignState state,int index)=>index==4?PokemonLeagueChampion(state.Script.Variables.GetValueOrDefault("VAR_STARTER_MON")):
        new("pret:TRAINER_ELITE_FOUR_"+PokemonLeagueNames[index].ToUpperInvariant());
    private MapId PokemonLeagueTrainerMap(TrainerId id)
    {
        int index=IsPokemonLeagueChampion(id)?4:Array.FindIndex(PokemonLeagueNames,n=>id.Value=="pret:TRAINER_ELITE_FOUR_"+n.ToUpperInvariant());
        return index<0?throw new InvalidDataException("pokemon_league_trainer_map"):new("map.pokemon-league-"+PokemonLeagueRooms[index]+"-room");
    }
    private string PokemonLeagueTrainerActor(TrainerId id)=>_content.MapDefinition(PokemonLeagueTrainerMap(id)).Actors.Single(a=>a.SourceLocalId==1).LocalId;
    private string PokemonLeagueTrainerActorScript(TrainerId id)=>IsPokemonLeagueChampion(id)?"0x0":
        "PokemonLeague_"+PokemonLeagueNames[PokemonLeagueRoomIndex(PokemonLeagueTrainerMap(id))]+"sRoom_EventScript_"+PokemonLeagueNames[PokemonLeagueRoomIndex(PokemonLeagueTrainerMap(id))];
    private ForestTrainerBattleDefinition PokemonLeagueTrainerScript(TrainerId id,string? script)
    {
        var b=_content.PokemonLeagueTrainerParties!.Battles[id];
        if(b.Mode!=3||b.BattleTypeFlags!=8||script is not null&&script!=b.ScriptEntry)throw new InvalidDataException("pokemon_league_trainer_script");
        return new(b.Trainer,b.Mode,b.BattleTypeFlags,b.ScriptEntry,b.IntroText,b.DefeatText,b.PostBattleText,b.EntryScript,b.Callback,b.Source);
    }
    private bool PokemonLeagueHistoryOriginMatches(CampaignForestTrainerHistory history)=>history.PokemonLeagueChallenge is >0&&
        _content.PokemonLeagueTrainerParties?.Battles.TryGetValue(new(history.TrainerId),out var b)==true&&
        history.SourceScriptEntry==b.ScriptEntry&&history.SourceScriptLine==b.Source.StartLine+1;
    private void RequirePokemonLeagueOccurrence(CampaignState state,TrainerId trainer,ForestTrainerBattleDefinition definition,bool noIntro)
    {
        int room=PokemonLeagueRoomIndex(state.World.Player.MapId),challenge=PokemonLeagueChallenge(state);
        if(!noIntro||room<0||trainer!=PokemonLeagueTrainerAt(state,room)||definition.Mode!=3||definition.BattleTypeFlags!=8||
            state.HallOfFame is not null||state.Script.Flags.GetValueOrDefault("FLAG_SYS_CAN_LINK_WITH_RS")||state.Script.Flags.GetValueOrDefault("FLAG_SYS_GAME_CLEAR")||
            state.Script.Variables.GetValueOrDefault("VAR_MAP_SCENE_POKEMON_LEAGUE")!=Math.Min(room+1,4)||
            state.LastHealingLocation!=PokemonLeagueHealingLocation||!state.Script.Locked||
            state.Script.Flags.GetValueOrDefault(PokemonLeagueDefeatedFlags[room])||
            (state.ForestTrainerHistory??[]).Any(h=>h.PokemonLeagueChallenge==challenge&&h.TrainerId==trainer.Value&&h.Outcome==WildBattleOutcome.Won)||
            Enumerable.Range(0,room).Any(i=>!state.Script.Flags.GetValueOrDefault(PokemonLeagueDefeatedFlags[i])))
            throw new InvalidDataException("pokemon_league_battle_occurrence");
        var actor=state.Actors.GetValueOrDefault(PokemonLeagueTrainerActor(trainer));
        if(actor is null||!actor.Active||actor.ScriptId!=PokemonLeagueTrainerActorScript(trainer))throw new InvalidDataException("pokemon_league_battle_actor");
    }
    private static void ValidatePokemonLeagueCatalog(ICampaignCatalog content)
    {
        if(!content.PokemonLeagueEnabled)
        {
            if(content.PokemonLeagueTrainerParties is not null||content.PokemonLeagueOpponents is not null)throw new InvalidDataException("pokemon_league_profile_missing");
            return;
        }
        if(!content.VictoryRoadEnabled||content.Maps.Count!=218||content.PokemonLeagueTrainerParties is null||content.PokemonLeagueOpponents is null||content.PokemonLeagueMartStock is null||
            !ReferenceEquals(content.PokemonLeagueOpponents.Predecessor,content.VictoryRoadOpponents)||
            content.PokemonLeagueOpponents.Catalog.Species.Count!=133||content.PokemonLeagueOpponents.Owned.Catalog.Species.Count!=115)
            throw new InvalidDataException("pokemon_league_catalog_scope");
        PokemonLeagueTrainerPartyFactory.ValidateCatalog(content.PokemonLeagueTrainerParties);
        content.PokemonLeagueMartStock.Validate(content.EarlyShop!);
        foreach(var b in content.PokemonLeagueTrainerParties.Battles.Values)
        {
            var call=content.FindProgram(b.ScriptEntry)?.Instructions.SingleOrDefault(i=>i.Op==StoryOp.TrainerBattle);
            if(call is null||call.Opcode!="trainerbattle_no_intro"||call.Source.Path!=b.Source.Path||call.Source.Symbol!=b.ScriptEntry||
                call.Source.Line!=b.Source.StartLine+1||!call.Args.SequenceEqual(new[]{b.Trainer.Value[5..],b.DefeatText}))throw new InvalidDataException("pokemon_league_battle_source");
        }
        var nurse=content.MapDefinition(new(PokemonLeagueCenterMap)).Actors.SingleOrDefault(a=>a.GraphicsId=="OBJ_EVENT_GFX_NURSE");
        if(nurse is null||nurse.SourceLocalId!=2||nurse.X!=13||nurse.Y!=10)throw new InvalidDataException("pokemon_league_nurse_source");
    }
    private CampaignState ResetPokemonLeague(CampaignState state,string reason)
    {
        if(!_content.PokemonLeagueEnabled)return state;
        var progress=state.PokemonLeague??new(1,[]);int history=(state.ForestTrainerHistory??[]).Length;
        var flags=state.Script.Flags;
        foreach(string flag in PokemonLeagueDefeatedFlags)flags=flags.Remove(flag);
        var trainers=state.BattledTrainers.Except(PokemonLeagueChampionSymbols.Select(s=>"pret:"+s));
        return state with{PokemonLeague=new(checked(progress.ChallengeNumber+1),progress.Resets.Add(new(progress.ChallengeNumber,history,reason))),
            BattledTrainers=trainers,Script=state.Script with{Flags=flags,Variables=state.Script.Variables.SetItem("VAR_MAP_SCENE_POKEMON_LEAGUE",0)}};
    }
    private CampaignState ResetPokemonLeagueForWhiteOut(CampaignState state)=>ResetPokemonLeague(state,"whiteout");

    private string? ValidatePokemonLeagueProgress(CampaignState state)
    {
        if(!_content.PokemonLeagueEnabled)return state.PokemonLeague is not null||state.HallOfFame is not null||state.PokemonLeagueCompletion is not null||
            OwnedPokemon(state).Any(p=>p.ChampionRibbon)||(state.ForestTrainerHistory??[]).Any(h=>h?.PokemonLeagueChallenge is not null)?"save_pokemon_league_profile_missing":null;
        if(state.Script.Flags.GetValueOrDefault("FLAG_SYS_CAN_LINK_WITH_RS")||state.Script.Flags.GetValueOrDefault("FLAG_SYS_NATIONAL_DEX")||
            state.Script.Variables.GetValueOrDefault("VAR_NATIONAL_DEX")!=0)return "save_pokemon_league_postgame_not_admitted";
        var progress=state.PokemonLeague??new(1,[]);var history=state.ForestTrainerHistory??[];
        if(progress.Resets.IsDefault||progress.ChallengeNumber<1||progress.ChallengeNumber!=progress.Resets.Length+1)return "save_pokemon_league_challenge";
        int lastCount=0;
        for(int i=0;i<progress.Resets.Length;i++)
        {
            var reset=progress.Resets[i];
            if(reset is null||reset.ChallengeNumber!=i+1||reset.TrainerHistoryCount<lastCount||reset.TrainerHistoryCount>history.Length||
                reset.Reason is not("whiteout" or "hall-of-fame"))return "save_pokemon_league_reset";
            lastCount=reset.TrainerHistoryCount;
        }
        int next=0,activeChallenge=1;
        for(int i=0;i<history.Length;i++)
        {
            var h=history[i];if(h is null||h.TrainerId is null)continue;
            int challenge=1+progress.Resets.Count(r=>r.TrainerHistoryCount<=i);
            if(challenge!=activeChallenge){activeChallenge=challenge;next=0;}
            if(!IsPokemonLeagueTrainer(new(h.TrainerId)))
            {if(h.PokemonLeagueChallenge is not null)return "save_pokemon_league_foreign_receipt";continue;}
            if(next>=5||!PokemonLeagueHistoryOriginMatches(h)||h.PokemonLeagueChallenge!=challenge||h.TrainerId!=PokemonLeagueTrainerAt(state,next).Value)
                return "save_pokemon_league_history_order";
            if(h.Outcome==WildBattleOutcome.Won)next++;
            else if(!progress.Resets.Any(r=>r.ChallengeNumber==challenge&&r.TrainerHistoryCount==i+1&&r.Reason=="whiteout"))return "save_pokemon_league_loss_without_reset";
        }
        if(activeChallenge!=progress.ChallengeNumber)next=0;
        int scene=state.Script.Variables.GetValueOrDefault("VAR_MAP_SCENE_POKEMON_LEAGUE"),room=PokemonLeagueRoomIndex(state.World.Player.MapId);
        if(scene is <0 or >4||PokemonLeagueMap(state.World.Player.MapId)&&
            (!Enumerable.Range(1,8).All(i=>state.Script.Flags.GetValueOrDefault("FLAG_BADGE0"+i+"_GET"))||state.Script.Variables.GetValueOrDefault("VAR_MAP_SCENE_ROUTE23")!=8))return "save_pokemon_league_scene";
        for(int i=0;i<5;i++)if(state.Script.Flags.GetValueOrDefault(PokemonLeagueDefeatedFlags[i])!=(i<next))return "save_pokemon_league_defeated_flags";
        foreach(var symbol in PokemonLeagueChampionSymbols)
            if(state.BattledTrainers.Contains("pret:"+symbol)!=(next==5&&"pret:"+symbol==PokemonLeagueChampion(state.Script.Variables.GetValueOrDefault("VAR_STARTER_MON")).Value))return "save_pokemon_league_champion_flag";
        if(room>=0&&(scene!=Math.Min(room+1,4)||next<room||next>room+1))return "save_pokemon_league_room_progress";
        if(PokemonLeagueExpectedEdits(state) is {} expected&&!(state.MapEdits??ImmutableSortedDictionary<string,FieldMetatileEdit>.Empty).SequenceEqual(expected))return "save_pokemon_league_doors";
        return ValidatePokemonLeagueHallOfFame(state);
    }
}
