using System.Collections.Immutable;
using Game.Domain.Events;

namespace Game.Content;

public static partial class PretStoryCompiler
{
    private static bool PokemonLeagueSource(PretSourceScriptFile file,string path,string hash)=>
        file.SourceRef.Kind==SourceKind.Pret&&file.SourceRef.Repository=="pret/pokefirered"&&
        file.SourceRef.Commit==PretWorldMap.SourceCommit&&file.Path==path&&file.SourceRef.Path==path&&file.SourceRef.Sha256==hash;

    private static bool IsPokemonLeagueAssemblyData(PretSourceScriptFile file,string label,string[] body)=>
        PokemonLeagueSource(file,"data/maps/IndigoPlateau_PokemonCenter_1F/scripts.inc","4f83364d4a0b4dfeefc93d993f83ed4bab769ccc3e939df06e331bc9ffbc9f81")&&
        label=="IndigoPlateau_PokemonCenter_1F_Items"&&body.SequenceEqual(new[]{
            ".2byte ITEM_ULTRA_BALL",".2byte ITEM_GREAT_BALL",".2byte ITEM_FULL_RESTORE",".2byte ITEM_MAX_POTION",
            ".2byte ITEM_REVIVE",".2byte ITEM_FULL_HEAL",".2byte ITEM_MAX_REPEL",".2byte ITEM_NONE","release","end"});

    private static (StoryOp,int,int)? PokemonLeagueInstructionShape(PretSourceScriptFile file,string label,string opcode,
        ImmutableArray<string> operands)
    {
        if(opcode=="cleartrainerflag"&&
            PokemonLeagueSource(file,"data/scripts/hall_of_fame.inc","c47616f23fe35a6c11228143a4277c86b4e4b9a803378389f8b9c30bce10e6c4")&&label=="EventScript_ResetEliteFour")
        {
            // All six original reset entries are preserved. Clearing an unearned rematch
            // flag is not admission of the rematch battle or postgame campaign.
            if(operands.Length!=1||operands[0] is not ("TRAINER_CHAMPION_FIRST_SQUIRTLE" or "TRAINER_CHAMPION_FIRST_BULBASAUR" or
                "TRAINER_CHAMPION_FIRST_CHARMANDER" or "TRAINER_CHAMPION_REMATCH_SQUIRTLE" or "TRAINER_CHAMPION_REMATCH_BULBASAUR" or "TRAINER_CHAMPION_REMATCH_CHARMANDER"))
                throw new InvalidDataException("pokemon_league_cleartrainerflag_operands");
            return(StoryOp.Special,1,1);
        }
        if(opcode=="fadescreenspeed"&&
            PokemonLeagueSource(file,"data/maps/PokemonLeague_HallOfFame/scripts.inc","cdd15328720aae302433909003a895b8afab553dc8da9c729d64e7a74b67784b")&&
            label=="PokemonLeague_HallOfFame_EventScript_EnterRoom")
        {
            if(!operands.SequenceEqual(new[]{"FADE_TO_BLACK","24"}))throw new InvalidDataException("pokemon_league_fadescreenspeed_operands");
            return(StoryOp.Special,2,2);
        }
        if(opcode=="trainerbattle_no_intro"&&file.Path.StartsWith("data/maps/PokemonLeague_",StringComparison.Ordinal))
        {
            var expected=(file.Path,file.SourceRef.Sha256,label) switch
            {
                ("data/maps/PokemonLeague_LoreleisRoom/scripts.inc","62206bd5b51860c75293bf53021a845ee1495cae5ea28703c78ca142b7c32e15","PokemonLeague_LoreleisRoom_EventScript_Battle")=>"TRAINER_ELITE_FOUR_LORELEI",
                ("data/maps/PokemonLeague_BrunosRoom/scripts.inc","d1de5447c4968e608082afedb1512319f11287555f92ce92cd3cc2229c76009a","PokemonLeague_BrunosRoom_EventScript_Battle")=>"TRAINER_ELITE_FOUR_BRUNO",
                ("data/maps/PokemonLeague_AgathasRoom/scripts.inc","3d3af744133659ca0644bbdb1aefcb04d2c0e1255f65a18904fd6e59b7452e4f","PokemonLeague_AgathasRoom_EventScript_Battle")=>"TRAINER_ELITE_FOUR_AGATHA",
                ("data/maps/PokemonLeague_LancesRoom/scripts.inc","796a87867040647f4f0cf22eb516455b296f5a1b967378797528421421f55830","PokemonLeague_LancesRoom_EventScript_Battle")=>"TRAINER_ELITE_FOUR_LANCE",
                ("data/maps/PokemonLeague_ChampionsRoom/scripts.inc","41d2f054b4569798cd3e11e848f3ac3da62781db22320241ef18efb6604f72f3","PokemonLeague_ChampionsRoom_EventScript_BattleSquirtle")=>"TRAINER_CHAMPION_FIRST_SQUIRTLE",
                ("data/maps/PokemonLeague_ChampionsRoom/scripts.inc","41d2f054b4569798cd3e11e848f3ac3da62781db22320241ef18efb6604f72f3","PokemonLeague_ChampionsRoom_EventScript_BattleBulbasaur")=>"TRAINER_CHAMPION_FIRST_BULBASAUR",
                ("data/maps/PokemonLeague_ChampionsRoom/scripts.inc","41d2f054b4569798cd3e11e848f3ac3da62781db22320241ef18efb6604f72f3","PokemonLeague_ChampionsRoom_EventScript_BattleCharmander")=>"TRAINER_CHAMPION_FIRST_CHARMANDER",
                _=>null
            };
            if(expected is null||!PokemonLeagueSource(file,file.Path,file.SourceRef.Sha256!))return(StoryOp.Unsupported,0,int.MaxValue);
            var defeat=file.Path.Split('/')[2]+"_Text_Defeat";
            if(!operands.SequenceEqual(new[]{expected,defeat}))throw new InvalidDataException("pokemon_league_trainer_operands");
            return(StoryOp.TrainerBattle,2,2);
        }
        return null;
    }
}
