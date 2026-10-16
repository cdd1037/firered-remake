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
    public GameOpponentPokemonCatalog? PokemonLeagueOpponents { get; }
    public PokemonLeagueMartStock? PokemonLeagueMartStock { get; }
    public bool PokemonLeagueEnabled=>PokemonLeagueRegion is not null;


    private static PokemonLeagueMartStock CreatePokemonLeagueMartStock(PretPokemonLeagueRegion region,EarlyShopCatalog shop,PretStoryCompilation scripts)
    {
        const string stockId=Game.Domain.Items.PokemonLeagueMartStock.ExpectedStockId;
        string[] expected=["ITEM_ULTRA_BALL","ITEM_GREAT_BALL","ITEM_FULL_RESTORE","ITEM_MAX_POTION","ITEM_REVIVE","ITEM_FULL_HEAL","ITEM_MAX_REPEL","ITEM_NONE"];
        if(!scripts.AssemblyData.TryGetValue(stockId,out var data)||!data.All(d=>d.Directive==".2byte"&&d.Operands.Length==1)||!data.Select(d=>d.Operands[0]).SequenceEqual(expected))
            throw new InvalidDataException("pokemon_league_mart_declaration");
        var result=new PokemonLeagueMartStock(PretWorldMap.SourceCommit,expected.Take(7).Select(s=>new ItemId("pret:"+s)).ToImmutableArray(),
            new(Game.Domain.Items.PokemonLeagueMartStock.ExpectedSourcePath,stockId,53,63));
        result.Validate(shop);
        return result;
    }

    private static void ValidatePokemonLeagueComposition(PretPokemonLeagueRegion region,ImmutableDictionary<MapId,PretWorldMap> maps,PokemonLeagueTrainerCatalog trainers,GameOpponentPokemonCatalog opponents,GamePokemonCatalog owned,PretStoryCompilation scripts,ImmutableDictionary<string,string>.Builder texts)
    {
        PokemonLeagueTrainerPartyFactory.ValidateCatalog(trainers);
        if(trainers.Trainers.Count!=7||trainers.Trainers.Values.Sum(t=>t.Party.Length)!=38||
            trainers.Trainers.Values.SelectMany(t=>t.Party).Any(p=>!opponents.Catalog.Species.ContainsKey(p.Species)))
            throw new InvalidDataException("pokemon_league_trainer_scope");
        bool Has(string label,string opcode,params string[] args)=>scripts.FindProgram(label)?.Instructions.Any(i=>i.Opcode==opcode&&i.Args.SequenceEqual(args))==true;
        foreach(var battle in trainers.Battles.Values)
        {
            var program=scripts.FindProgram(battle.ScriptEntry)??throw new InvalidDataException("pokemon_league_trainer_script");
            if(battle.Mode!=3||battle.RivalFlags!=0||battle.BattleTypeFlags!=8||
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
        if(leagueBattles.Length!=14||leagueBattles.Any(i=>i.Op!=StoryOp.TrainerBattle))
            throw new InvalidDataException("pokemon_league_battle_instruction_scope");
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
