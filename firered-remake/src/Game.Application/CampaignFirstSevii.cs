using System.Collections.Immutable;
using Game.Domain;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.World;

namespace Game.Application;

public sealed record CampaignSeagallopTransition(string PresentationId,int Origin,int Destination,TilePosition To);
public sealed record CampaignSeagallopMenu(string SourceMenu,ImmutableArray<string> Options);

public sealed partial class CampaignSession
{
    private const string SeagallopSource="data/scripts/seagallop.inc";
    private static bool FirstSeviiMap(string map)=>map.StartsWith("map.one-island",StringComparison.Ordinal)||
        map.StartsWith("map.two-island",StringComparison.Ordinal)||map.StartsWith("map.three-island",StringComparison.Ordinal);
    private static bool FirstSeviiSource(string path)=>path.StartsWith("data/maps/OneIsland",StringComparison.Ordinal)||
        path.StartsWith("data/maps/TwoIsland",StringComparison.Ordinal)||path.StartsWith("data/maps/ThreeIsland",StringComparison.Ordinal);
    private bool InFirstSeviiScript(CampaignState state)=>_content.FirstSeviiEnabled&&
        state.Script.Stack.Any(frame=>_content.FindProgram(frame.ProgramId)?.Instructions.Any(i=>FirstSeviiSource(i.Source.Path))==true);
    private int SeviiValue(CampaignState state,string token)=>token.StartsWith("VAR_",StringComparison.Ordinal)?state.Script.Variables.GetValueOrDefault(token):
        int.TryParse(token,out int n)?n:_content.TryConstant(token,out n)?n:throw new InvalidDataException("first_sevii_operand_missing: "+token);
    private static readonly ImmutableDictionary<string,ImmutableArray<string>> SeagallopMenus=new Dictionary<string,ImmutableArray<string>>
    {
        ["MULTICHOICE_ISLAND_23"]=["TWO ISLAND","THREE ISLAND","EXIT"],
        ["MULTICHOICE_ISLAND_13"]=["ONE ISLAND","THREE ISLAND","EXIT"],
        ["MULTICHOICE_ISLAND_12"]=["ONE ISLAND","TWO ISLAND","EXIT"],
        ["MULTICHOICE_SEAGALLOP_V23"]=["VERMILION","TWO ISLAND","THREE ISLAND","EXIT"],
        ["MULTICHOICE_SEAGALLOP_V13"]=["VERMILION","ONE ISLAND","THREE ISLAND","EXIT"],
        ["MULTICHOICE_SEAGALLOP_V12"]=["VERMILION","ONE ISLAND","TWO ISLAND","EXIT"],
        ["MULTICHOICE_SEAGALLOP_123"]=["ONE ISLAND","TWO ISLAND","THREE ISLAND","EXIT"]
    }.ToImmutableDictionary(StringComparer.Ordinal);
    private TilePosition SeagallopDestination(int destination)
    {
        var (symbol,x,y)=destination switch
        {
            0=>("MAP_VERMILION_CITY",23,32),1=>("MAP_ONE_ISLAND_HARBOR",8,5),
            2=>("MAP_TWO_ISLAND_HARBOR",8,5),3=>("MAP_THREE_ISLAND_HARBOR",8,5),
            4 when _content.FourIslandEnabled=>("MAP_FOUR_ISLAND_HARBOR",8,5),
            5 when _content.FiveIslandEnabled=>("MAP_FIVE_ISLAND_HARBOR",8,5),
            6 when _content.SixIslandEnabled=>("MAP_SIX_ISLAND_HARBOR",8,5),
            8=>("MAP_CINNABAR_ISLAND",21,7),_=>throw new InvalidDataException("seagallop_destination_not_admitted")
        };
        var map=_content.MapFromSourceSymbol(symbol);var cell=_content.Maps[map].GetTile(x,y)??throw new InvalidDataException("seagallop_landing_missing");
        return new(map,x,y,cell.Elevation,Direction.South);
    }
    private Decision<CampaignState> DecideSeagallop(CampaignState state,CampaignCommand command)
    {
        var ferry=state.Seagallop!;
        if(!_content.FirstSeviiEnabled||command.Kind!="seagallop_ack"||command.Text!=ferry.PresentationId)
            return new(CommandStatus.Rejected,state,"seagallop_presentation_pending");
        if(ferry.To!=SeagallopDestination(ferry.Destination)||state.Script.Wait is null)
            return new(CommandStatus.Fault,state,"seagallop_transition_invalid");
        // CB2_LoadMap owns a fresh map context; do not resume the departure script.
        var moved=state with{Seagallop=null,SeagallopMenu=null,IsCycling=false,IsSurfing=false,SurfEffect=null,
            World=state.World with{Player=ferry.To},Script=state.Script with{Stack=[],Wait=null,Completed=true,Locked=false},
            Dialogue=null,Motions=[],PresentationEffects=[],PresentationTicks=0,ScreenIsBlack=false,PlayerVisible=true,
            Exploration=ResetEncounterModifiers(state.Exploration!)};
        return new(CommandStatus.Accepted,Pump(EnterMap(moved,Direction.South)));
    }
    private bool TryApplyFirstSeviiEffect(CampaignState state,StoryEffect effect,out CampaignState next)
    {
        next=state;if(!_content.FirstSeviiEnabled)return false;
        var a=effect.Args;bool own=FirstSeviiSource(effect.Source.Path),ferry=effect.Source.Path==SeagallopSource;
        CampaignState Result(string name,int value)=>state with{Script=state.Script with{Variables=state.Script.Variables.SetItem(name,value)}};
        if(effect.Opcode=="buffernumberstring"&&ferry&&effect.Source.Symbol=="EventScript_SailToDest"&&
            a.Length==2&&a[0]=="STR_VAR_1")
        {
            // StoryEffects already resolves the VAR_RESULT read operand to its value.
            int number=SeviiValue(state,a[1]);
            if(number is not (1 or 2 or 7) && !(number==6&&_content.FourIslandEnabled))throw new InvalidDataException("seagallop_number_invalid");
            next=state with{TextBuffers=state.TextBuffers.SetItem("STR_VAR_1",number.ToString(System.Globalization.CultureInfo.InvariantCulture))};return true;
        }
        if(effect.Opcode=="multichoice"&&(ferry||effect.Source.Path=="data/maps/VermilionCity/scripts.inc")&&
            a.Length==4&&SeagallopMenus.TryGetValue(a[2],out var options))
        {
            if(a[0]!="19"||a[1]!=(options.Length==3?"6":"5")||a[3]!="FALSE"||state.Script.Wait?.Kind!=StoryWaitKind.MenuChoice)
                throw new InvalidDataException("seagallop_menu_source_context");
            next=state with{SeagallopMenu=new(a[2],options)};return true;
        }
        if(effect.Opcode=="specialvar"&&a.Length==2)
        {
            if(ferry&&a[1]=="GetSeagallopNumber")
            {
                int origin=SeviiValue(state,"VAR_0x8004"),destination=SeviiValue(state,"VAR_0x8006");
                _=SeagallopDestination(origin);_=SeagallopDestination(destination);
                next=Result(a[0],origin==8||destination==8?1:origin==0||destination==0?7:origin is >=1 and <=3 && destination is >=1 and <=3?2:6);return true;
            }
            if(ferry&&a[1]=="IsPlayerLeftOfVermilionSailor")
            {next=Result(a[0],state.World.Player.MapId.Value=="map.vermilion-city"&&state.World.Player.X<24?1:0);return true;}
            if(own&&a[1]=="IsNationalPokedexEnabled")
            {next=Result(a[0],NationalDexEnabledIn(state)?1:0);return true;}
        }
        if(effect.Opcode=="special"&&a.Length==1)
        {
            if(a[0]=="DoSeagallopFerryScene"&&ferry&&effect.Source.Symbol=="EventScript_SetSail")
            {
                int origin=SeviiValue(state,"VAR_0x8004"),destination=SeviiValue(state,"VAR_0x8006");
                bool first=origin==8&&destination==1&&state.Script.Variables.GetValueOrDefault("VAR_MAP_SCENE_ONE_ISLAND_HARBOR")==1;
                bool returning=origin==1&&destination==8&&state.Script.Variables.GetValueOrDefault("VAR_MAP_SCENE_ONE_ISLAND_POKEMON_CENTER_1F")==3;
                bool ordinary=RainbowIslandAvailable(origin)&&RainbowIslandAvailable(destination)&&origin!=destination&&
                    (OpeningInventoryRules.Quantity(state.Inventory?.KeyItems??[],FirstSeviiShopCatalog.TriPassId)==1||
                        _content.SeviiRubyEnabled&&OpeningInventoryRules.Quantity(state.Inventory?.KeyItems??[],RainbowPassItem)==1)&&
                    (origin<4&&destination<4||_content.FourIslandEnabled&&OpeningInventoryRules.Quantity(state.Inventory?.KeyItems??[],RainbowPassItem)==1)&&
                    (origin!=0&&destination!=0||state.Script.Variables.GetValueOrDefault("VAR_MAP_SCENE_CINNABAR_ISLAND")>=4);
                if(!(first||returning||ordinary)||state.Seagallop is not null||!state.Script.Locked||!state.ScreenIsBlack)
                    throw new InvalidDataException("seagallop_departure_context");
                next=state with{Seagallop=new("seagallop:"+effect.Sequence,origin,destination,SeagallopDestination(destination)),SeagallopMenu=null};return true;
            }
            if(a[0]=="QuestLog_CutRecording"&&(own||ferry))
            {if(state.Script.QuestLogState!=0)throw new InvalidDataException("quest_log_recording_not_supported");return true;}
            if(a[0]=="BufferBigGuyOrBigGirlString"&&effect.Source.Path is "data/maps/ThreeIsland_BerryForest/scripts.inc" or "data/maps/TwoIsland_JoyfulGameCorner/scripts.inc")
            {next=state with{TextBuffers=state.TextBuffers.SetItem("STR_VAR_1",state.PlayerGender==0?"Big guy":"Big girl")};return true;}
        }
        // Exact original Berry Forest escape-warp: its map allows Escape Rope.
        if(effect.Opcode=="setescapewarp"&&effect.Source.Path=="data/maps/ThreeIsland_BerryForest/scripts.inc"&&
            effect.Source.Symbol=="ThreeIsland_BerryForest_OnTransition"&&a.SequenceEqual(new[]{"MAP_THREE_ISLAND_BOND_BRIDGE","12","6"}))
        {next=state with{EscapeDestination=FirstSeviiEscapeRopeRules.ForestDestination};return true;}
        bool billDepartureWarp=effect.Source.Path=="data/maps/CinnabarIsland_PokemonCenter_1F/scripts.inc"&&
            a.SequenceEqual(new[]{"MAP_CINNABAR_ISLAND","14","11"});
        if(effect.Opcode=="warp"&&(own||billDepartureWarp))
        {
            var map=_content.MapFromSourceSymbol(a[0]);int x,y;
            if(a.Length==3){x=SeviiValue(state,a[1]);y=SeviiValue(state,a[2]);}
            else{int index=SeviiValue(state,a[1]);var w=_content.Maps[map].FieldWarps[index];x=w.X;y=w.Y;}
            var cell=_content.Maps[map].GetTile(x,y)??throw new InvalidDataException("first_sevii_script_warp_outside_map");
            if(_content.Maps[map].SourceBehaviorAt(x,y)==0x69)
            {y++;cell=_content.Maps[map].GetTile(x,y)??throw new InvalidDataException("first_sevii_script_door_exit_missing");}
            next=EnterMap(state with{World=state.World with{Player=new(map,x,y,cell.Elevation,Direction.South)},
                Script=state.Script with{Stack=[],Wait=null,Completed=true,Locked=false},Dialogue=null,Motions=[],
                PresentationEffects=[],PresentationTicks=0,PlayerVisible=true,ScreenIsBlack=false},Direction.South);return true;
        }
        return TryApplyFirstSeviiItemEffect(state,effect,out next);
    }
    private string? FirstSeviiCenterSourceMap(CampaignState state)=>!_content.FirstSeviiEnabled?null:state.LastHealingLocation switch
    {
        "HEAL_LOCATION_ONE_ISLAND"=>"MAP_ONE_ISLAND_POKEMON_CENTER_1F",
        "HEAL_LOCATION_TWO_ISLAND"=>"MAP_TWO_ISLAND_POKEMON_CENTER_1F",
        "HEAL_LOCATION_THREE_ISLAND"=>"MAP_THREE_ISLAND_POKEMON_CENTER_1F",
        "HEAL_LOCATION_FOUR_ISLAND" when _content.FourIslandEnabled=>"MAP_FOUR_ISLAND_POKEMON_CENTER_1F",
        "HEAL_LOCATION_FIVE_ISLAND" when _content.FiveIslandEnabled=>"MAP_FIVE_ISLAND_POKEMON_CENTER_1F",
        "HEAL_LOCATION_SIX_ISLAND" when _content.SixIslandEnabled=>"MAP_SIX_ISLAND_POKEMON_CENTER_1F",_=>null
    };
}
