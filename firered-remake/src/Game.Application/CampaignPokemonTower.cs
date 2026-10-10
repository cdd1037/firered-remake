using System.Collections.Immutable;
using Game.Domain;
using Game.Domain.Encounters;
using Game.Domain.Events;
using Game.Domain.Items;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;
using Game.Domain.World;
namespace Game.Application;

public sealed partial class CampaignSession
{
    private const string TowerSixSource="data/maps/PokemonTower_6F/scripts.inc";
    private const string TowerHouseSource="data/maps/LavenderTown_VolunteerPokemonHouse/scripts.inc";
    private static bool TowerMap(string map)=>map.StartsWith("map.pokemon-tower-",StringComparison.Ordinal);
    private bool HasSilphScope(CampaignState s)=>OpeningInventoryRules.Quantity(s.Inventory?.KeyItems??[],RocketHideoutItemRules.SilphScopeId)==1;
    private bool SeenByTowerMarowak(CampaignState s,int species)=>_content.PokemonTowerEnabled&&species==105&&HasSilphScope(s)&&(s.Exploration?.Battles.Any(b=>b.ScriptedMarowak&&b.SourceSpeciesId==105)??false);
    private bool IsTowerButton(CampaignState s)=>_content.PokemonTowerEnabled&&s.Script.Wait?.Kind==StoryWaitKind.ButtonPress&&s.World.Player.MapId.Value=="map.pokemon-tower-6f"&&!s.Script.Stack.IsEmpty&&s.Script.Stack[^1].ProgramId=="PokemonTower_6F_EventScript_DefeatedMarowakGhost";
    private Decision<CampaignState> DecideTowerButton(CampaignState s,CampaignCommand c)
    {
        if(!IsTowerButton(s)||c.Text is not("A" or "B"))return new(CommandStatus.Rejected,s,"tower_button_invalid");
        return new(CommandStatus.Accepted,Pump(Advance(s with{PresentationEffects=[],PresentationTicks=0,Motions=[]},StoryRules.Continue(s.Script,s.Script.Wait!.CorrelationId,_content))));
    }
    private bool TryApplyTowerEffect(CampaignState s,StoryEffect e,out CampaignState next)
    {
        next=s;if(!_content.PokemonTowerEnabled)return false;
        var a=e.Args;int Value(string t)=>s.Script.Variables.TryGetValue(t,out int v)?v:int.TryParse(t,out v)?v:_content.TryConstant(t,out v)?v:throw new InvalidDataException("tower_operand: "+t);
        CampaignState Result(bool ok)=>s with{Script=s.Script with{Variables=s.Script.Variables.SetItem("VAR_RESULT",ok?1:0)}};
        bool six=e.Source.Path==TowerSixSource;
        if(e.Opcode=="getplayerxy"&&e.Source.Path=="data/maps/PokemonTower_7F/scripts.inc")
        {
            if(s.World.Player.MapId.Value!="map.pokemon-tower-7f"||!a.SequenceEqual(new[]{"VAR_0x8004","VAR_0x8005"})||e.Source.Symbol is not("PokemonTower_7F_EventScript_DefeatedGrunt1" or "PokemonTower_7F_EventScript_DefeatedGrunt2" or "PokemonTower_7F_EventScript_DefeatedGrunt3"))throw new InvalidDataException("tower_exit_playerxy_context");
            next=s with{Script=s.Script with{Variables=s.Script.Variables.SetItem(a[0],s.World.Player.X).SetItem(a[1],s.World.Player.Y)}};return true;
        }

        if(e.Opcode=="setwildbattle")
        {
            if(!six||e.Source.Symbol!="PokemonTower_6F_EventScript_MarowakGhost"||s.World.Player.MapId.Value!="map.pokemon-tower-6f"||a.Length is <2 or >3||Value(a[0])!=105||Value(a[1])!=30||a.Length==3&&Value(a[2])!=0||s.TowerWildPreparation is not null)
                throw new InvalidDataException("tower_scripted_wild_context");
            // ScrCmd_setwildbattle creates the normal random-IV/PID opponent first. The
            // scoped StartMarowakBattle subsequently overwrites it, retaining these draws.
            var made=PokemonFactory.Create(BattlePokemon,new(new("pret:SPECIES_MAROWAK"),30,s.TrainerId,IvPolicy.Random,PersonalityPolicy.Random),s.World.Rng);
            next=s with{TowerWildPreparation=new(made.Pokemon,made.Rng,made.Trace),World=s.World with{Rng=made.Rng}};return true;
        }
        if(six&&e.Opcode=="special"&&a.Length==1&&a[0]=="StartMarowakBattle")
        {
            var original=s.TowerWildPreparation??throw new InvalidDataException("tower_marowak_not_prepared");
            if(HasSilphScope(s))
            {
                var mother=PokemonTowerMarowakFactory.Create(BattlePokemon,s.TrainerId,s.World.Rng,snorlaxRules:_content.SnorlaxEnabled,fuchsiaRules:_content.FuchsiaRouteEnabled);
                original=new(mother.Pokemon,mother.Rng,[..original.RandomTrace,..mother.Trace]);
            }
            next=BeginWildBattle(s with{TowerWildPreparation=null,World=s.World with{Rng=original.GlobalRng}},original,scriptedMarowak:true);return true;
        }
        if(six&&e.Opcode=="special"&&a.SequenceEqual(new[]{"QuestLog_CutRecording"}))return true;
        if(six&&e.Opcode=="waitbuttonpress")
        {if(!IsTowerButton(s))throw new InvalidDataException("tower_button_context");return true;}
        if(e.Opcode=="fadescreen"&&e.Source.Path=="data/maps/PokemonTower_5F/scripts.inc")
        {
            if(a.Length!=1||a[0] is not("FADE_TO_WHITE" or "FADE_FROM_WHITE"))throw new InvalidDataException("tower_white_fade_source");
            next=s with{ScreenIsWhite=a[0]=="FADE_TO_WHITE",PresentationTicks=16};return true;
        }
        if(e.Opcode is "release" or "releaseall"&&e.Source.Path==TowerHouseSource)
        {next=s with{MessagePointer=null,Dialogue=null,SignMessage=null};return true;}
        if(e.Opcode is not("checkitemspace" or "additem" or "checkitemtype" or "bufferitemname" or "bufferitemnameplural"))return false;
        bool buffer=e.Opcode.StartsWith("bufferitem",StringComparison.Ordinal);
        if(a.Length<(buffer?2:1)||Value(a[buffer?1:0])!=350)return false;
        if(s.World.Player.MapId.Value!="map.lavender-town-volunteer-pokemon-house"||!s.Script.Flags.GetValueOrDefault("FLAG_RESCUED_MR_FUJI")||e.Source.Path!=TowerHouseSource&&e.Source.Path is not("data/scripts/obtain_item.inc" or "data/event_scripts.s"))
            throw new InvalidDataException("tower_flute_source_context");
        var item=ShopCatalog.Items.Single(i=>i.Id==PokemonTowerItemRules.PokeFluteId);
        if(buffer){next=s with{TextBuffers=s.TextBuffers.SetItem(a[0],item.Name)};return true;}
        if(e.Opcode=="checkitemtype"){next=s with{Script=s.Script with{Variables=s.Script.Variables.SetItem("VAR_RESULT",Value("POCKET_KEY_ITEMS"))}};return true;}
        if(a.Length>2||(a.Length==2?Value(a[1]):1)!=1)throw new InvalidDataException("tower_flute_quantity");
        var inventory=s.Inventory??throw new InvalidDataException("inventory_missing");var keys=inventory.KeyItems??[];
        bool has=keys.Any(k=>k.Item==item.Id),space=keys.Length<ShopCatalog.QuestLimits.KeyItemSlots&&!has;
        if(e.Opcode=="checkitemspace")
        {if(s.Script.Flags.GetValueOrDefault("FLAG_GOT_POKE_FLUTE"))throw new InvalidDataException("tower_flute_repeat_capacity");next=Result(space);return true;}
        if(!s.Script.Flags.GetValueOrDefault("FLAG_GOT_POKE_FLUTE")||!space)throw new InvalidDataException("tower_flute_award_context");
        next=Result(true) with{Inventory=inventory with{KeyItems=keys.Add(new(item.Id,1))}};return true;
    }
    private string? ValidateTowerProgress(CampaignState s)
    {
        if(s.TowerWildPreparation is not null||s.ScreenIsWhite)return "save_tower_event_active";
        if(!_content.PokemonTowerEnabled)return null;
        bool Flag(string f)=>s.Script.Flags.GetValueOrDefault(f);
        int Count(string item)=>OpeningInventoryRules.Quantity(s.Inventory?.KeyItems??[],new("pret:ITEM_"+item));
        int rivalScene=s.Script.Variables.GetValueOrDefault("VAR_MAP_SCENE_POKEMON_TOWER_2F");
        var rivalWins=s.BattledTrainers.Where(t=>t.StartsWith("pret:TRAINER_RIVAL_POKEMON_TOWER_",StringComparison.Ordinal)).ToArray();
        string expectedRival="pret:TRAINER_RIVAL_POKEMON_TOWER_"+(s.Script.Variables.GetValueOrDefault("VAR_STARTER_MON") switch{2=>"SQUIRTLE",1=>"BULBASAUR",_=>"CHARMANDER"});
        if(rivalScene is <0 or >1||rivalWins.Length>1||rivalWins.Any(t=>t!=expectedRival)||rivalScene!=(rivalWins.Length==1?1:0)||Flag("FLAG_HIDE_TOWER_RIVAL")!=(rivalScene==1))return "save_tower_rival_progress";
        for(int n=1;n<=3;n++)if(Flag("FLAG_HIDE_TOWER_ROCKET_"+n)!=s.BattledTrainers.Contains("pret:TRAINER_TEAM_ROCKET_GRUNT_"+(n+18)))return "save_tower_rocket_progress";
        int scene=s.Script.Variables.GetValueOrDefault("VAR_MAP_SCENE_POKEMON_TOWER_6F");
        if(scene is <0 or >1)return "save_tower_marowak_scene";
        bool rescued=Flag("FLAG_RESCUED_MR_FUJI");
        if(Flag("FLAG_HIDE_TOWER_FUJI")!=rescued||Flag("FLAG_HIDE_POKEHOUSE_FUJI")==rescued)return "save_tower_fuji_visibility";
        if(Flag("FLAG_GOT_POKE_FLUTE")!=(Count("POKE_FLUTE")==1)||Count("POKE_FLUTE")>1||Flag("FLAG_GOT_POKE_FLUTE")&&!rescued)return "save_tower_flute_reward";
        if(rescued&&(scene!=1||Enumerable.Range(19,3).Any(n=>!s.BattledTrainers.Contains("pret:TRAINER_TEAM_ROCKET_GRUNT_"+n))))return "save_tower_fuji_rescue_gate";
        if(scene==1&&!s.Exploration!.Battles.Any(b=>b.ScriptedMarowak&&b.Outcome==WildBattleOutcome.Won))return "save_tower_marowak_victory_missing";
        return null;
    }
}
