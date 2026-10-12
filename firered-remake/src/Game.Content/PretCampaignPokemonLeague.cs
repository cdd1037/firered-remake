using System.Collections.Immutable;
using System.Text.Json;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;
using Game.Domain.Trainers;
using Game.Domain.World;

namespace Game.Content;

/// <summary>First League successor. Seven source interiors, seven teams and unchanged ownership/inventory proofs.</summary>
public sealed partial class PretCampaignCatalog
{
    public const string PokemonLeagueContentProfile="firered-pokemon-league-first-clear-v1";
    public PretPokemonLeagueRegion? PokemonLeagueRegion { get; }
    public PokemonLeagueTrainerCatalog? PokemonLeagueTrainerParties { get; }
    public PokemonLeagueOpponentPokemonCatalog? PokemonLeagueOpponents { get; }
    public PokemonLeagueMartStock? PokemonLeagueMartStock { get; }
    public bool PokemonLeagueEnabled=>PokemonLeagueRegion is not null;

    public static PretCampaignCatalog LoadPokemonLeagueProfile(string root,bool useCompactMapData=true,
        string? pokemonLeagueContentRoot=null,string? pokemonLeagueRulesRoot=null,
        string? victoryRoadContentRoot=null,string? victoryRoadRulesRoot=null,string? leagueRivalRulesRoot=null,string? viridianGymContentRoot=null,string? viridianGymRulesRoot=null,
        string? firstSeviiContentRoot=null,string? firstSeviiRulesRoot=null,string? firstSeviiPresentationRoot=null)=>Load(root,
        includeStarterEvents:true,includeFirstBattle:true,includeOpeningItems:true,
        includeViridianRegion:true,includeParcelQuest:true,includeCapture:true,includeViridianCenter:true,
        includeOldManTutorial:true,includePartyTraining:true,includeForest:true,includeForestTrainers:true,
        includePewter:true,includeEarlyShop:true,includeEarlyStarter:true,includeEarlyParty:true,
        includePokemonStorage:true,includeFieldPickups:true,includeRepel:true,includeTownMap:true,
        includeOakRating:true,includeRoute22:true,useCompactMapData:useCompactMapData,includeRoute3:true,
        includeMagikarp:true,includeMtMoon:true,includeCerulean:true,includeBill:true,includeVermilion:true,
        includeSsAnne:true,includeVermilionGym:true,includeCityServices:true,includeRoute11:true,
        includeRockTunnel:true,includeCeladonApproach:true,includeCeladonGym:true,
        includeRocketHideout:true,includePokemonTower:true,includeSnorlax:true,
        includeRegionalFlavor:true,includeRegionalServices:true,includeRegionalInteriorServices:true,
        includeFuchsiaRoute:true,includeFuchsiaGym:true,includePresetDoubles:true,includeSafari:true,
        includeSurf:true,includeSilph:true,includeSaffronGym:true,includeCinnabar:true,includeFirstSevii:true,
        firstSeviiContentRoot:firstSeviiContentRoot,firstSeviiRulesRoot:firstSeviiRulesRoot,
        firstSeviiPresentationRoot:firstSeviiPresentationRoot,includeViridianGym:true,
        viridianGymContentRoot:viridianGymContentRoot,viridianGymRulesRoot:viridianGymRulesRoot,
        includeLeagueRival:true,leagueRivalRulesRoot:leagueRivalRulesRoot,includeVictoryRoad:true,
        victoryRoadContentRoot:victoryRoadContentRoot,victoryRoadRulesRoot:victoryRoadRulesRoot,
        includePokemonLeague:true,pokemonLeagueContentRoot:pokemonLeagueContentRoot,pokemonLeagueRulesRoot:pokemonLeagueRulesRoot);

    private static void AppendPokemonLeagueSources(PretPokemonLeagueRegion region,List<PretSourceScriptFile> sources,
        List<string> texts,Dictionary<string,int> constants,List<string> hashes)
    {
        var prior=sources.ToDictionary(s=>s.Path,StringComparer.Ordinal);
        foreach(var source in region.Scripts)
        {
            if(prior.TryGetValue(source.Path,out var old))
            {
                if(old.SourceRef.Sha256!=source.SourceRef.Sha256||old.Text!=source.Text)
                    throw new InvalidDataException("pokemon_league_source_conflict: "+source.Path);
            }
            else{prior.Add(source.Path,source);sources.Add(source);}
            if(!texts.Contains(source.Text,StringComparer.Ordinal))texts.Add(source.Text);
        }
        foreach(var text in region.TextSources.Values)if(!texts.Contains(text,StringComparer.Ordinal))texts.Add(text);
        MergeRegionalServiceConstants(constants,region.Constants);
        hashes.AddRange(region.AdditionalSourceHashes);
        hashes.Add("pokemon-league-trainers:"+PretPokemonLeagueRules.TrainersSha256);
        hashes.Add("pokemon-league-opponents:"+PretPokemonLeagueRules.PokemonSha256);
        hashes.Add("pokemon-league-rules:"+PretPokemonLeagueRules.RulesSha256);
        hashes.Add("pokemon-league-source-inputs:"+PretPokemonLeagueRules.SourceInputsSha256);
        hashes.Add("pokemon-league-campaign:"+PokemonLeagueContentProfile);
    }

    private static void ValidatePokemonLeagueAnchors(PretPokemonLeagueRegion region,ImmutableDictionary<MapId,PretWorldMap> maps)
    {
        if(maps.Count!=211||region.NewMaps.Count!=7||region.NewMaps.Keys.Any(maps.ContainsKey)||
            maps.ContainsKey(new("map.indigo-plateau-pokemon-center-2f")))
            throw new InvalidDataException("pokemon_league_predecessor_maps");
        var exterior=maps[new("map.indigo-plateau-exterior")];
        if(!JsonElement.DeepEquals(exterior.Events,region.ReusedAnchorEvents["IndigoPlateau_Exterior"]))
            throw new InvalidDataException("pokemon_league_exterior_anchor");
        // The upper-floor source map is retained as boundary evidence, never composed.
        if(region.NewMaps.ContainsKey(new("map.indigo-plateau-pokemon-center-2f")))
            throw new InvalidDataException("pokemon_league_link_floor_unsupported");
    }

    private static PokemonLeagueMartStock CreatePokemonLeagueMartStock(PretPokemonLeagueRegion region,
        EarlyShopCatalog shop,PretStoryCompilation scripts)
    {
        const string stockId=Game.Domain.Items.PokemonLeagueMartStock.ExpectedStockId;
        string[] expected=["ITEM_ULTRA_BALL","ITEM_GREAT_BALL","ITEM_FULL_RESTORE","ITEM_MAX_POTION","ITEM_REVIVE","ITEM_FULL_HEAL","ITEM_MAX_REPEL","ITEM_NONE"];
        if(!scripts.AssemblyData.TryGetValue(stockId,out var data)||!data.SequenceEqual(expected.Select(s=>".2byte "+s)))
            throw new InvalidDataException("pokemon_league_mart_declaration");
        var source=region.Scripts.Single(s=>s.Path==Game.Domain.Items.PokemonLeagueMartStock.ExpectedSourcePath);
        var result=new PokemonLeagueMartStock(PretWorldMap.SourceCommit,expected.Take(7).Select(s=>new ItemId("pret:"+s)).ToImmutableArray(),
            new(source.Path,stockId,53,63,source.SourceRef.Sha256!));
        result.Validate(shop);
        return result;
    }

    private static void ValidatePokemonLeagueComposition(PretPokemonLeagueRegion region,ImmutableDictionary<MapId,PretWorldMap> maps,
        ImmutableDictionary<MapId,PretWorldMap> previous,PokemonLeagueTrainerCatalog trainers,PokemonLeagueOpponentPokemonCatalog opponents,
        CinnabarOwnedPokemonCatalog owned,PretStoryCompilation scripts,ImmutableDictionary<string,string>.Builder texts)
    {
        if(maps.Count!=218||previous.Count!=211||previous.Any(p=>!ReferenceEquals(p.Value,maps[p.Key]))||
            region.NewMaps.Any(p=>!ReferenceEquals(p.Value,maps[p.Key]))||owned.Catalog.Species.Count!=115||owned.Catalog.Moves.Count!=354||
            !ReferenceEquals(opponents.Owned,owned)||opponents.Predecessor.Catalog.Species.Count!=124||opponents.Catalog.Species.Count!=133||
            maps.ContainsKey(new("map.indigo-plateau-pokemon-center-2f")))
            throw new InvalidDataException("pokemon_league_composition_scope");
        PokemonLeagueTrainerPartyFactory.ValidateCatalog(trainers);
        if(trainers.Trainers.Count!=7||trainers.Trainers.Values.Sum(t=>t.Party.Length)!=38||
            trainers.Trainers.Values.SelectMany(t=>t.Party).Any(p=>!opponents.Catalog.Species.ContainsKey(p.Species)))
            throw new InvalidDataException("pokemon_league_trainer_scope");
        bool Has(string label,string opcode,params string[] args)=>scripts.FindProgram(label)?.Instructions.Any(i=>i.Opcode==opcode&&i.Args.SequenceEqual(args))==true;
        foreach(var battle in trainers.Battles.Values)
        {
            var source=region.Scripts.Single(s=>s.Path==battle.Source.Path);
            var program=scripts.FindProgram(battle.ScriptEntry)??throw new InvalidDataException("pokemon_league_trainer_script");
            if(source.SourceRef.Sha256!=battle.Source.Sha256||battle.Mode!=3||battle.RivalFlags!=0||battle.BattleTypeFlags!=8||
                battle.EntryScript!="EventScript_DoNoIntroTrainerBattle"||battle.Callback!="CB2_EndTrainerBattle"||
                !program.Instructions.Any(i=>i.Op==StoryOp.TrainerBattle&&i.Opcode=="trainerbattle_no_intro"&&i.Source.Path==battle.Source.Path&&
                    i.Source.Line>=battle.Source.StartLine&&i.Source.Line<=battle.Source.EndLine&&i.Args.SequenceEqual(new[]{battle.Trainer.Value[5..],battle.DefeatText}))||
                !texts.ContainsKey(battle.DefeatText))throw new InvalidDataException("pokemon_league_trainer_instruction");
        }
        foreach(var (room,person) in new[]{("LoreleisRoom","Lorelei"),("BrunosRoom","Bruno"),("AgathasRoom","Agatha"),("LancesRoom","Lance")})
        {
            string root="PokemonLeague_"+room,flag="FLAG_DEFEATED_"+person.ToUpperInvariant();
            if(!Has(root+"_EventScript_"+person,"goto_if_set",flag,root+"_EventScript_PostBattle")||
                !Has(root+"_EventScript_"+person,"call_if_unset","FLAG_SYS_CAN_LINK_WITH_RS",root+"_EventScript_Battle")||
                !Has(root+"_EventScript_Defeated"+person,"setflag",flag)||
                !Has(root+"_OnLoad","call_if_set",flag,root+"_EventScript_SetDoorOpen"))
                throw new InvalidDataException("pokemon_league_source_reward_gate");
        }
        var champion=scripts.FindProgram("PokemonLeague_ChampionsRoom_EventScript_EnterRoom")!;
        int battleIndex=Array.FindIndex(champion.Instructions.ToArray(),i=>i.Opcode=="call_if_unset"&&i.Args.SequenceEqual(new[]{"FLAG_SYS_CAN_LINK_WITH_RS","PokemonLeague_ChampionsRoom_EventScript_Battle"}));
        int flagIndex=Array.FindIndex(champion.Instructions.ToArray(),i=>i.Opcode=="setflag"&&i.Args.SequenceEqual(new[]{"FLAG_DEFEATED_CHAMP"}));
        if(battleIndex<0||flagIndex<=battleIndex)throw new InvalidDataException("pokemon_league_champion_reward_order");
        var leagueBattles=scripts.Programs.Values.SelectMany(p=>p.Instructions).Where(i=>i.Source.Path.StartsWith("data/maps/PokemonLeague_",StringComparison.Ordinal)&&i.Opcode=="trainerbattle_no_intro").ToArray();
        if(leagueBattles.Count(i=>i.Op==StoryOp.TrainerBattle)!=7||leagueBattles.Count(i=>i.Op==StoryOp.Unsupported)!=7)
            throw new InvalidDataException("pokemon_league_rematch_boundary");
        var reset=scripts.FindProgram("EventScript_ResetEliteFour")!;
        if(reset.Instructions.Count(i=>i.Opcode=="cleartrainerflag"&&i.Op==StoryOp.Special)!=6||
            !Has("EventScript_ResetEliteFour","setvar","VAR_MAP_SCENE_POKEMON_LEAGUE","0")||
            !Has("PokemonLeague_HallOfFame_EventScript_EnterRoom","fadescreenspeed","FADE_TO_BLACK","24"))
            throw new InvalidDataException("pokemon_league_completion_instruction");
        var center=maps[new("map.indigo-plateau-pokemon-center-1f")];
        var nurse=center.Events.GetProperty("object_events")[1];
        if(nurse.GetProperty("local_id").GetString()!="LOCALID_LEAGUE_NURSE"||nurse.GetProperty("x").GetInt32()!=13||nurse.GetProperty("y").GetInt32()!=10||
            !Has("IndigoPlateau_PokemonCenter_1F_EventScript_Nurse","call","EventScript_PkmnCenterNurse")||
            !Has("IndigoPlateau_PokemonCenter_1F_OnTransition","setrespawn","HEAL_LOCATION_INDIGO_PLATEAU"))
            throw new InvalidDataException("pokemon_league_center_service");
    }
}
