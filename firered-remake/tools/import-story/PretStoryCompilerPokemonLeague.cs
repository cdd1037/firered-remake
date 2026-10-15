using System.Collections.Immutable;
using Game.Domain.Events;

namespace Game.Content;

public static partial class PretStoryCompiler
{
    private static bool PokemonLeagueSource(PretSourceScriptFile file,string path)=>
        file.SourceRef.Kind==SourceKind.Pret&&file.Path==path&&file.SourceRef.Path==path;

    private static bool IsPokemonLeagueAssemblyData(PretSourceScriptFile file,string label,string[] body)=>
        PokemonLeagueSource(file,"data/maps/IndigoPlateau_PokemonCenter_1F/scripts.inc")&&
        label=="IndigoPlateau_PokemonCenter_1F_Items"&&body.SequenceEqual(new[]{
            ".2byte ITEM_ULTRA_BALL",".2byte ITEM_GREAT_BALL",".2byte ITEM_FULL_RESTORE",".2byte ITEM_MAX_POTION",".2byte ITEM_REVIVE",".2byte ITEM_FULL_HEAL",".2byte ITEM_MAX_REPEL",".2byte ITEM_NONE","release","end"});

    private static (StoryOp,int,int)? PokemonLeagueInstructionShape(PretSourceScriptFile file,string label,string opcode,ImmutableArray<string> operands)
    {
        if(opcode=="cleartrainerflag"&&
            PokemonLeagueSource(file,"data/scripts/hall_of_fame.inc")&&label=="EventScript_ResetEliteFour")
        {
            // All six original reset entries are preserved. Clearing an unearned rematch
            // flag is not admission of the rematch battle or postgame campaign.
            if(operands.Length!=1||operands[0] is not ("TRAINER_CHAMPION_FIRST_SQUIRTLE" or "TRAINER_CHAMPION_FIRST_BULBASAUR" or
                "TRAINER_CHAMPION_FIRST_CHARMANDER" or "TRAINER_CHAMPION_REMATCH_SQUIRTLE" or "TRAINER_CHAMPION_REMATCH_BULBASAUR" or "TRAINER_CHAMPION_REMATCH_CHARMANDER"))
                throw new InvalidDataException("pokemon_league_cleartrainerflag_operands");
            return(StoryOp.Special,1,1);
        }
        if(opcode=="fadescreenspeed"&&
            PokemonLeagueSource(file,"data/maps/PokemonLeague_HallOfFame/scripts.inc")&&
            label=="PokemonLeague_HallOfFame_EventScript_EnterRoom")
        {
            if(!operands.SequenceEqual(new[]{"FADE_TO_BLACK","24"}))throw new InvalidDataException("pokemon_league_fadescreenspeed_operands");
            return(StoryOp.Special,2,2);
        }
        if(opcode=="trainerbattle_no_intro"&&file.Path.StartsWith("data/maps/PokemonLeague_",StringComparison.Ordinal))
        {
            var expected=(file.Path,label) switch
            {
                ("data/maps/PokemonLeague_LoreleisRoom/scripts.inc","PokemonLeague_LoreleisRoom_EventScript_Battle")=>"TRAINER_ELITE_FOUR_LORELEI",
                ("data/maps/PokemonLeague_BrunosRoom/scripts.inc","PokemonLeague_BrunosRoom_EventScript_Battle")=>"TRAINER_ELITE_FOUR_BRUNO",
                ("data/maps/PokemonLeague_AgathasRoom/scripts.inc","PokemonLeague_AgathasRoom_EventScript_Battle")=>"TRAINER_ELITE_FOUR_AGATHA",
                ("data/maps/PokemonLeague_LancesRoom/scripts.inc","PokemonLeague_LancesRoom_EventScript_Battle")=>"TRAINER_ELITE_FOUR_LANCE",
                ("data/maps/PokemonLeague_ChampionsRoom/scripts.inc","PokemonLeague_ChampionsRoom_EventScript_BattleSquirtle")=>"TRAINER_CHAMPION_FIRST_SQUIRTLE",
                ("data/maps/PokemonLeague_ChampionsRoom/scripts.inc","PokemonLeague_ChampionsRoom_EventScript_BattleBulbasaur")=>"TRAINER_CHAMPION_FIRST_BULBASAUR",
                ("data/maps/PokemonLeague_ChampionsRoom/scripts.inc","PokemonLeague_ChampionsRoom_EventScript_BattleCharmander")=>"TRAINER_CHAMPION_FIRST_CHARMANDER",
                ("data/maps/PokemonLeague_LoreleisRoom/scripts.inc","PokemonLeague_LoreleisRoom_EventScript_Rematch")=>"TRAINER_ELITE_FOUR_LORELEI_2",
                ("data/maps/PokemonLeague_BrunosRoom/scripts.inc","PokemonLeague_BrunosRoom_EventScript_Rematch")=>"TRAINER_ELITE_FOUR_BRUNO_2",
                ("data/maps/PokemonLeague_AgathasRoom/scripts.inc","PokemonLeague_AgathasRoom_EventScript_Rematch")=>"TRAINER_ELITE_FOUR_AGATHA_2",
                ("data/maps/PokemonLeague_LancesRoom/scripts.inc","PokemonLeague_LancesRoom_EventScript_Rematch")=>"TRAINER_ELITE_FOUR_LANCE_2",
                ("data/maps/PokemonLeague_ChampionsRoom/scripts.inc","PokemonLeague_ChampionsRoom_EventScript_RematchSquirtle")=>"TRAINER_CHAMPION_REMATCH_SQUIRTLE",
                ("data/maps/PokemonLeague_ChampionsRoom/scripts.inc","PokemonLeague_ChampionsRoom_EventScript_RematchBulbasaur")=>"TRAINER_CHAMPION_REMATCH_BULBASAUR",
                ("data/maps/PokemonLeague_ChampionsRoom/scripts.inc","PokemonLeague_ChampionsRoom_EventScript_RematchCharmander")=>"TRAINER_CHAMPION_REMATCH_CHARMANDER",
                _=>null
            };
            if(expected is null||!PokemonLeagueSource(file,file.Path))return(StoryOp.Unsupported,0,int.MaxValue);
            var defeat=file.Path.Split('/')[2]+"_Text_Defeat";
            if(!operands.SequenceEqual(new[]{expected,defeat}))throw new InvalidDataException("pokemon_league_trainer_operands");
            return(StoryOp.TrainerBattle,2,2);
        }
        return null;
    }
}
