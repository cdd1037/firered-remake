using System.Collections.Immutable;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Trainers;

namespace Game.Content;

public sealed partial class PretCampaignCatalog
{
    public const string LeagueRivalContentProfile="firered-league-rival-campaign-v1";
    public LeagueRivalTrainerCatalog? LeagueRivalTrainerParties { get; }
    public bool LeagueRivalEnabled=>LeagueRivalTrainerParties is not null;

    // The exact eighth-gym composition gains only its three existing Route22 battle
    // definitions. Its maps, scripts, ownership, items and encounters are inherited.
    public static PretCampaignCatalog LoadLeagueRivalProfile(string root,bool useCompactMapData=true,
        string? leagueRivalRulesRoot=null,string? viridianGymContentRoot=null,string? viridianGymRulesRoot=null,
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
        includeLeagueRival:true,leagueRivalRulesRoot:leagueRivalRulesRoot);

    private static void ValidateLeagueRivalComposition(ImmutableDictionary<MapId,PretWorldMap> maps,
        LeagueRivalTrainerCatalog trainers,PretStoryCompilation scripts,IEnumerable<PretSourceScriptFile> sources,
        ImmutableDictionary<string,string>.Builder text,CampaignPokemonContent pokemon,
        CinnabarOwnedPokemonCatalog owned,CinnabarOpponentPokemonCatalog opponents)
    {
        if(maps.Count!=207||!maps.ContainsKey(new("map.route-22"))||!maps.ContainsKey(new("map.viridian-city-gym"))||
            owned.Catalog.Species.Count!=115||owned.Catalog.Moves.Count!=354||
            !ReferenceEquals(pokemon.Catalog,owned.Catalog)||!ReferenceEquals(opponents.Owned,owned))
            throw new InvalidDataException("league_rival_composition_scope");
        LeagueRivalTrainerPartyFactory.ValidateCatalog(trainers);
        foreach(var battle in trainers.Battles.Values)
        {
            var source=sources.SingleOrDefault(s=>s.Path==battle.Source.Path);
            var program=scripts.FindProgram(battle.ScriptEntry);
            if(source?.SourceRef.Sha256!=battle.Source.Sha256||program is null||
                !program.Instructions.Any(i=>i.Op==StoryOp.TrainerBattle&&i.Opcode=="trainerbattle_no_intro"&&
                    i.Source.Path==battle.Source.Path&&i.Source.Symbol==battle.ScriptEntry&&i.Source.Line==battle.Source.StartLine+1&&
                    i.Args.SequenceEqual(new[]{battle.Trainer.Value[5..],battle.DefeatText}))||
                !text.ContainsKey(battle.IntroText)||!text.ContainsKey(battle.DefeatText)||!text.ContainsKey(battle.PostBattleText))
                throw new InvalidDataException("league_rival_source_script_mismatch");
        }
        foreach(var member in trainers.Trainers.Values.SelectMany(t=>t.Party))
            if(!opponents.Catalog.Species.ContainsKey(member.Species)||
                member.CustomMoves.Any(move=>!opponents.Catalog.Moves.ContainsKey(move)))
                throw new InvalidDataException("league_rival_predecessor_pokemon_missing");
    }
}
