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

    private static void ValidateLeagueRivalComposition(ImmutableDictionary<MapId,PretWorldMap> maps,LeagueRivalTrainerCatalog trainers,PretStoryCompilation scripts,ImmutableDictionary<string,string>.Builder text,CampaignPokemonContent pokemon,GamePokemonCatalog owned,GameOpponentPokemonCatalog opponents)
    {
        if(!maps.ContainsKey(new("map.route-22"))||!maps.ContainsKey(new("map.viridian-city-gym")))
            throw new InvalidDataException("league_rival_composition_scope");
        LeagueRivalTrainerPartyFactory.ValidateCatalog(trainers);
        foreach(var battle in trainers.Battles.Values)
        {
            var program=scripts.FindProgram(battle.ScriptEntry);
            if(program is null||
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
