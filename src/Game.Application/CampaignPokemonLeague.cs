using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Trainers;
using Game.Domain.WildBattle;
using Game.Domain.World;

namespace Game.Application;

public sealed record CampaignPokemonLeagueReset(int ChallengeNumber, int TrainerHistoryCount, string Reason);
public sealed record CampaignPokemonLeagueChallenge(int ChallengeNumber,int TrainerHistoryCount,bool Rematch);
public sealed record CampaignPokemonLeagueProgress(int ChallengeNumber, ImmutableArray<CampaignPokemonLeagueReset> Resets)
{
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)]
    public ImmutableArray<CampaignPokemonLeagueChallenge>? Challenges { get; init; }
}

/// <summary>Both source League teams share one challenge lifecycle. Receipts preserve
/// the tier selected on entry even after a later Celio handoff unlocks rematches.</summary>
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
    private bool IsPokemonLeagueRematchTrainer(TrainerId id)=>_content.LeagueRematchTrainerParties?.Trainers.ContainsKey(id)==true;
    private bool IsPokemonLeagueTrainer(TrainerId id)=>(_content.PokemonLeagueTrainerParties?.Trainers.ContainsKey(id)==true||IsPokemonLeagueRematchTrainer(id));
    private bool IsPokemonLeagueChampion(TrainerId id)=>IsPokemonLeagueTrainer(id)&&PokemonLeagueChampionSymbols.Contains(id.Value[5..]);
    private static int PokemonLeagueChallenge(CampaignState state)=>state.PokemonLeague?.ChallengeNumber??1;
    private static TrainerId PokemonLeagueChampion(int starter,bool rematch=false)=>new("pret:TRAINER_CHAMPION_"+(rematch?"REMATCH_":"FIRST_")+(starter switch
    {
        0=>"CHARMANDER",1=>"BULBASAUR",2=>"SQUIRTLE",
        _=>throw new InvalidDataException("pokemon_league_starter_invalid")
    }));
    private bool PokemonLeagueChallengeIsRematch(CampaignState state,int challenge)
    {
        var receipt=(state.PokemonLeague?.Challenges??[]).SingleOrDefault(c=>c.ChallengeNumber==challenge);
        if(receipt is not null)return receipt.Rematch;
        // Pre-extension first-clear saves have no entry receipts. Their original
        // trainer IDs authenticate their tier; today's world flag never selects it.
        var first=(state.ForestTrainerHistory??[]).FirstOrDefault(h=>h.PokemonLeagueChallenge==challenge);
        return first is not null&&IsPokemonLeagueRematchTrainer(new(first.TrainerId));
    }
    private TrainerId PokemonLeagueTrainerAt(CampaignState state,int index,int? challenge=null)
    {
        bool rematch=PokemonLeagueChallengeIsRematch(state,challenge??PokemonLeagueChallenge(state));
        return index==4?PokemonLeagueChampion(state.Script.Variables.GetValueOrDefault("VAR_STARTER_MON"),rematch):
            new("pret:TRAINER_ELITE_FOUR_"+PokemonLeagueNames[index].ToUpperInvariant()+(rematch?"_2":""));
    }
    private CampaignState BeginPokemonLeagueChallenge(CampaignState state)
    {
        int challenge=PokemonLeagueChallenge(state);var progress=state.PokemonLeague??new(1,[]);
        var receipts=progress.Challenges??[];
        if(receipts.Any(r=>r.ChallengeNumber==challenge))return state;
        if((state.ForestTrainerHistory??[]).Any(h=>h.PokemonLeagueChallenge==challenge))return state;
        bool rematch=state.Script.Flags.GetValueOrDefault("FLAG_SYS_CAN_LINK_WITH_RS");
        if(rematch&&(!CelioCompleted(state)))throw new InvalidDataException("pokemon_league_rematch_unlock");
        return state with{PokemonLeague=progress with{Challenges=receipts.Add(new(challenge,(state.ForestTrainerHistory??[]).Length,rematch))}};
    }
    private CeruleanTrainerBattleDefinition? PokemonLeagueBattle(TrainerId id)=>
        _content.PokemonLeagueTrainerParties?.Battles.GetValueOrDefault(id)??_content.LeagueRematchTrainerParties?.Battles.GetValueOrDefault(id);
    private MapId PokemonLeagueTrainerMap(TrainerId id)
    {
        int index=IsPokemonLeagueChampion(id)?4:Array.FindIndex(PokemonLeagueNames,n=>id.Value=="pret:TRAINER_ELITE_FOUR_"+n.ToUpperInvariant()+(IsPokemonLeagueRematchTrainer(id)?"_2":""));
        return index<0?throw new InvalidDataException("pokemon_league_trainer_map"):new("map.pokemon-league-"+PokemonLeagueRooms[index]+"-room");
    }
    private string PokemonLeagueTrainerActor(TrainerId id)=>_content.MapDefinition(PokemonLeagueTrainerMap(id)).Actors.Single(a=>a.SourceLocalId==1).LocalId;
    private string PokemonLeagueTrainerActorScript(TrainerId id)=>IsPokemonLeagueChampion(id)?"0x0":
        "PokemonLeague_"+PokemonLeagueNames[PokemonLeagueRoomIndex(PokemonLeagueTrainerMap(id))]+"sRoom_EventScript_"+PokemonLeagueNames[PokemonLeagueRoomIndex(PokemonLeagueTrainerMap(id))];
    private bool PokemonLeagueHistoryOriginMatches(CampaignForestTrainerHistory history) =>
        history.PokemonLeagueChallenge is > 0 && PokemonLeagueBattle(new(history.TrainerId)) is not null &&
        RegisteredTrainerHistoryMatches(history);
    private void RequirePokemonLeagueOccurrence(CampaignState state,TrainerId trainer,ForestTrainerBattleDefinition definition,bool noIntro)
    {
        int room=PokemonLeagueRoomIndex(state.World.Player.MapId),challenge=PokemonLeagueChallenge(state);
        if(!noIntro||room<0||trainer!=PokemonLeagueTrainerAt(state,room)||definition.Mode!=3||definition.BattleTypeFlags!=8||
            state.PokemonLeagueCompletion is not null||
            IsPokemonLeagueRematchTrainer(trainer)!=state.Script.Flags.GetValueOrDefault("FLAG_SYS_CAN_LINK_WITH_RS")||
            IsPokemonLeagueRematchTrainer(trainer)&&(!CelioCompleted(state)||!(state.PokemonLeague?.Challenges??[]).Any(c=>c.ChallengeNumber==challenge&&c.Rematch))||
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
        if(content.PokemonLeagueTrainerParties is null||content.OpponentPokemon is null||content.PokemonLeagueMartStock is null)
            throw new InvalidDataException("pokemon_league_catalog_scope");
        PokemonLeagueTrainerPartyFactory.ValidateCatalog(content.PokemonLeagueTrainerParties);
        content.PokemonLeagueMartStock.Validate(content.EarlyShop!);
        {
            if(content.LeagueRematchTrainerParties is null||content.OpponentPokemon is null)
                throw new InvalidDataException("pokemon_league_rematch_catalog_scope");
            LeagueRematchTrainerPartyFactory.ValidateCatalog(content.LeagueRematchTrainerParties);
        }
        foreach(var b in content.PokemonLeagueTrainerParties.Battles.Values.Concat(content.LeagueRematchTrainerParties?.Battles.Values??[]))
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
        var progress=state.PokemonLeague??new(1,[]);int history=(state.ForestTrainerHistory??[]).Length;
        var flags=state.Script.Flags;
        foreach(string flag in PokemonLeagueDefeatedFlags)flags=flags.Remove(flag);
        var trainers=state.BattledTrainers.Except(PokemonLeagueChampionSymbols.Select(s=>"pret:"+s));
        return state with{PokemonLeague=progress with{ChallengeNumber=checked(progress.ChallengeNumber+1),Resets=progress.Resets.Add(new(progress.ChallengeNumber,history,reason))},
            BattledTrainers=trainers,Script=state.Script with{Flags=flags,Variables=state.Script.Variables.SetItem("VAR_MAP_SCENE_POKEMON_LEAGUE",0)}};
    }
    private CampaignState ResetPokemonLeagueForWhiteOut(CampaignState state)=>ResetPokemonLeague(state,"whiteout");

    private string? ValidatePokemonLeagueProgress(CampaignState state)
    {
        if(state.Script.Flags.GetValueOrDefault("FLAG_SYS_CAN_LINK_WITH_RS")&&!CelioCompleted(state))return "save_pokemon_league_postgame_not_admitted";
        var progress=state.PokemonLeague??new(1,[]);var history=state.ForestTrainerHistory??[];
        if(progress.Resets.IsDefault||progress.ChallengeNumber<1||progress.ChallengeNumber!=progress.Resets.Length+1)return "save_pokemon_league_challenge";
        if(history.IsDefault||history.Any(h=>h is null||h.TrainerId is null))return "save_pokemon_league_history_order";
        int lastCount=0;
        for(int i=0;i<progress.Resets.Length;i++)
        {
            var reset=progress.Resets[i];
            if(reset is null||reset.ChallengeNumber!=i+1||reset.TrainerHistoryCount<lastCount||reset.TrainerHistoryCount>history.Length||
                reset.Reason is not("whiteout" or "hall-of-fame"))return "save_pokemon_league_reset";
            lastCount=reset.TrainerHistoryCount;
        }
        var starts=progress.Challenges??[];
        if(starts.IsDefault||starts.Any(c=>c is null)||starts.Select(c=>c.ChallengeNumber).Distinct().Count()!=starts.Length||
            !starts.Select(c=>c.ChallengeNumber).SequenceEqual(starts.Select(c=>c.ChallengeNumber).Order()))return "save_pokemon_league_entry_receipt";
        foreach(var entry in starts)
        {
            int lower=entry.ChallengeNumber==1?0:progress.Resets.ElementAtOrDefault(entry.ChallengeNumber-2)?.TrainerHistoryCount??-1;
            int upper=entry.ChallengeNumber==progress.ChallengeNumber?history.Length:progress.Resets.ElementAtOrDefault(entry.ChallengeNumber-1)?.TrainerHistoryCount??-1;
            if(entry.ChallengeNumber<1||entry.ChallengeNumber>progress.ChallengeNumber||lower<0||upper<0||entry.TrainerHistoryCount<lower||entry.TrainerHistoryCount>upper||
                entry.Rematch&&(!CelioCompleted(state)))return "save_pokemon_league_entry_receipt";
        }
        int next=0,activeChallenge=1;
        for(int i=0;i<history.Length;i++)
        {
            var h=history[i];if(h is null||h.TrainerId is null)continue;
            int challenge=1+progress.Resets.Count(r=>r.TrainerHistoryCount<=i);
            if(challenge!=activeChallenge){activeChallenge=challenge;next=0;}
            if(!IsPokemonLeagueTrainer(new(h.TrainerId)))
            {if(h.PokemonLeagueChallenge is not null)return "save_pokemon_league_foreign_receipt";continue;}
            if(next>=5||!PokemonLeagueHistoryOriginMatches(h)||h.PokemonLeagueChallenge!=challenge||h.TrainerId!=PokemonLeagueTrainerAt(state,next,challenge).Value)
                return "save_pokemon_league_history_order";
            var entry=starts.SingleOrDefault(c=>c.ChallengeNumber==challenge);
            if(entry is not null&&entry.TrainerHistoryCount>i||IsPokemonLeagueRematchTrainer(new(h.TrainerId))&&entry is not{Rematch:true})return "save_pokemon_league_entry_receipt";
            if(h.Outcome==WildBattleOutcome.Won)next++;
            else if(!progress.Resets.Any(r=>r.ChallengeNumber==challenge&&r.TrainerHistoryCount==i+1&&r.Reason=="whiteout"))return "save_pokemon_league_loss_without_reset";
        }
        foreach(var reset in progress.Resets.Where(r=>r.Reason=="hall-of-fame"))
        {
            var wins=history.Where(h=>h.PokemonLeagueChallenge==reset.ChallengeNumber&&h.Outcome==WildBattleOutcome.Won).ToArray();
            if(wins.Length!=5||!IsPokemonLeagueChampion(new(wins[^1].TrainerId)))return "save_pokemon_league_hof_reset";
        }
        if(activeChallenge!=progress.ChallengeNumber)next=0;
        int scene=state.Script.Variables.GetValueOrDefault("VAR_MAP_SCENE_POKEMON_LEAGUE"),room=PokemonLeagueRoomIndex(state.World.Player.MapId);
        if(scene is <0 or >4||PokemonLeagueMap(state.World.Player.MapId)&&
            (!Enumerable.Range(1,8).All(i=>state.Script.Flags.GetValueOrDefault("FLAG_BADGE0"+i+"_GET"))||state.Script.Variables.GetValueOrDefault("VAR_MAP_SCENE_ROUTE23")!=8))return "save_pokemon_league_scene";
        for(int i=0;i<5;i++)if(state.Script.Flags.GetValueOrDefault(PokemonLeagueDefeatedFlags[i])!=(i<next))return "save_pokemon_league_defeated_flags";
        foreach(var symbol in PokemonLeagueChampionSymbols)
            if(state.BattledTrainers.Contains("pret:"+symbol)!=(next==5&&"pret:"+symbol==PokemonLeagueTrainerAt(state,4).Value))return "save_pokemon_league_champion_flag";
        if(room>=0&&(PokemonLeagueChallengeIsRematch(state,progress.ChallengeNumber)!=state.Script.Flags.GetValueOrDefault("FLAG_SYS_CAN_LINK_WITH_RS")||
            state.Script.Flags.GetValueOrDefault("FLAG_SYS_CAN_LINK_WITH_RS")&&!starts.Any(c=>c.ChallengeNumber==progress.ChallengeNumber&&c.Rematch)))return "save_pokemon_league_entry_receipt";
        if(room>=0&&(scene!=Math.Min(room+1,4)||next<room||next>room+1))return "save_pokemon_league_room_progress";
        if(PokemonLeagueExpectedEdits(state) is {} expected&&!(state.MapEdits??ImmutableSortedDictionary<string,FieldMetatileEdit>.Empty).SequenceEqual(expected))return "save_pokemon_league_doors";
        return ValidatePokemonLeagueHallOfFame(state);
    }
}
