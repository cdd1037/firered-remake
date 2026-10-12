using System.Collections.Immutable;
using System.Text;
using System.Text.RegularExpressions;
using Game.Domain.Events;
namespace Game.Content;

public sealed record PretSourceScriptFile(string Path,string Text,SourceRef SourceRef);
public sealed record PretMapScriptHeader(string EventKind,string Target);
public sealed record PretMapScriptCondition(string Variable,string Expected,string Target);
public sealed record PretMovementCommand(string Opcode,int SourceLine);
public sealed record PretStoryCompilation(
    ImmutableDictionary<string,StoryProgram> Programs,
    ImmutableDictionary<string,ImmutableArray<PretMapScriptHeader>> MapHeaders,
    ImmutableDictionary<string,ImmutableArray<PretMapScriptCondition>> MapConditions,
    ImmutableDictionary<string,ImmutableArray<PretMovementCommand>> Movements,
    ImmutableArray<ContentDiagnostic> UnsupportedInstructions, ImmutableDictionary<string,int> Constants) : IStoryCatalog
{
    public ImmutableDictionary<string,ImmutableArray<string>> AssemblyData { get; init; }=ImmutableDictionary<string,ImmutableArray<string>>.Empty;
    public StoryProgram? FindProgram(string id)=>Programs.GetValueOrDefault(id);
    public bool TryConstant(string symbol,out int value)
    {
        var constants=new Dictionary<string,int>(StringComparer.Ordinal){{"FALSE",0},{"TRUE",1},{"DIR_SOUTH",1},{"DIR_NORTH",2},{"DIR_WEST",3},{"DIR_EAST",4},{"FCPICKSTATE_COLORED",2},{"FCPICKSTATE_SILHOUETTE",1},{"FCPICKSTATE_NO_DRAW",0},{"CRY_MODE_NORMAL",0}};
        return Constants.TryGetValue(symbol,out value) || constants.TryGetValue(symbol,out value);
    }
}
/// <summary>Compiles source syntax to finite typed IR. Unsupported instructions remain explicit fault nodes.</summary>
public static partial class PretStoryCompiler
{
    public static PretStoryCompilation Compile(IEnumerable<PretSourceScriptFile> files, IReadOnlyDictionary<string,int>? constants = null,bool includeGiftOpcodes=false,bool includeRoute3Opcodes=false,bool includeMtMoonOpcodes=false,bool includeBillOpcodes=false,bool includeSsAnneOpcodes=false,bool includeVermilionGymOpcodes=false,bool includeRockTunnelOpcodes=false,bool includeRocketHideoutOpcodes=false,bool includePokemonTowerOpcodes=false,bool includeSnorlaxOpcodes=false,bool includePresetDoubleOpcodes=false,bool includeSafariOpcodes=false,bool includeSilphOpcodes=false,bool includeSaffronGymData=false,bool includeCinnabarData=false)
    {
        var programs=ImmutableDictionary.CreateBuilder<string,StoryProgram>(StringComparer.Ordinal);
        var headers=ImmutableDictionary.CreateBuilder<string,ImmutableArray<PretMapScriptHeader>>(StringComparer.Ordinal);
        var conditions=ImmutableDictionary.CreateBuilder<string,ImmutableArray<PretMapScriptCondition>>(StringComparer.Ordinal);
        var movements=ImmutableDictionary.CreateBuilder<string,ImmutableArray<PretMovementCommand>>(StringComparer.Ordinal);
        var unsupported=ImmutableArray.CreateBuilder<ContentDiagnostic>();
        var assemblyData=ImmutableDictionary.CreateBuilder<string,ImmutableArray<string>>(StringComparer.Ordinal);
        foreach(var file in files)
        {
            var errors=ContentValidator.ValidateSource(file.SourceRef,file.Path);if(errors.Count>0)throw new ContentValidationException(errors);
            var hash=Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(file.Text)));
            if(file.SourceRef.Sha256 is null||hash!=file.SourceRef.Sha256)throw ContentJson.Error("source_hash_mismatch",file.Path,"Story source must match an explicit pinned SHA-256.");
            var aliases=new Dictionary<string,string>(StringComparer.Ordinal);
            var blocks=new Dictionary<string,List<(int Line,string Text)>>(StringComparer.Ordinal);List<(int,string)>? current=null;
            var rawLines=file.Text.Replace("\r\n","\n",StringComparison.Ordinal).Split('\n');
            var macros=new Dictionary<string,List<(int Line,string Text)>>(StringComparer.Ordinal);
            var sourceLines=new List<(int Line,string Text)>();
            for(int index=0;index<rawLines.Length;index++)
            {
                var macro=Regex.Match(rawLines[index].Trim(),@"^\.macro\s+([A-Za-z_][A-Za-z0-9_]*)$");
                if(macro.Success)
                {
                    var body=new List<(int,string)>();bool ended=false;
                    while(++index<rawLines.Length){if(rawLines[index].Trim()==".endm"){ended=true;break;}body.Add((index+1,rawLines[index]));}
                    if(!ended||!macros.TryAdd(macro.Groups[1].Value,body))throw ContentJson.Error("macro_definition",file.Path,macro.Groups[1].Value);
                }
                else sourceLines.Add((index+1,rawLines[index]));
            }
            IEnumerable<(int Line,string Text)> Expand((int Line,string Text) line,int depth)
            {
                if(depth>16)throw ContentJson.Error("macro_recursion",file.Path,line.Text);
                if(macros.TryGetValue(line.Text.Trim(),out var body)){foreach(var item in body)foreach(var expanded in Expand(item,depth+1))yield return expanded;}
                else yield return line;
            }
            var lines=sourceLines.SelectMany(line=>Expand(line,0));
            foreach(var (sourceLine,sourceText) in lines)
            {
                var line=sourceText.Trim();if(line.StartsWith(".string",StringComparison.Ordinal)) {current?.Add((sourceLine,line));continue;}
                line=line.Split('@')[0].Trim();if(line.Length==0)continue;
                var equ=Equ().Match(line);if(equ.Success){aliases[equ.Groups[1].Value]=equ.Groups[2].Value;continue;}
                var label=Label().Match(line);if(label.Success){current=[];if(!blocks.TryAdd(label.Groups[1].Value,current))throw ContentJson.Error("duplicate_label",file.Path,label.Groups[1].Value);continue;}
                if(line.StartsWith(".include",StringComparison.Ordinal)||line.StartsWith(".align",StringComparison.Ordinal))continue;
                current?.Add((sourceLine,line));
            }
            string Alias(string operand)
            {
                var seen=new HashSet<string>(StringComparer.Ordinal);
                while(aliases.TryGetValue(operand,out var next)){if(!seen.Add(operand))throw ContentJson.Error("alias_cycle",file.Path,operand);operand=next;}
                return operand;
            }
            foreach(var (label,body) in blocks)
            {
                if(body.Count==0)continue;
                // Exact authenticated declarations are data, never swallowed script effects.
                if(includeSaffronGymData&&file.SourceRef.Commit==PretWorldMap.SourceCommit&&
                    file.SourceRef.Repository=="pret/pokefirered"&&
                    (file.Path,file.SourceRef.Sha256) is
                        ("data/maps/SaffronCity_Gym/scripts.inc","54a8956e1f0aa8d60250c13de4919ffa581f0bda0a9c41a8c97a0c02b1fb421b") or
                        ("data/maps/SaffronCity_Mart/scripts.inc","d45b8f0282dde2cdfb5a31a02b91b7c417a058cb07b6bb819561e9a0379bb3bb"))
                {
                    if(label is "SaffronCity_Gym_MapScripts" or "SaffronCity_Mart_MapScripts"&&body.Count==1&&body[0].Text==".byte 0")
                    {headers.Add(label,[]);assemblyData.Add(label,[".byte 0"]);continue;}
                    if(label=="SaffronCity_Mart_Items"&&body.Select(b=>b.Text).SequenceEqual(new[]{
                        ".2byte ITEM_GREAT_BALL",".2byte ITEM_HYPER_POTION",".2byte ITEM_REVIVE",".2byte ITEM_FULL_HEAL",
                        ".2byte ITEM_ESCAPE_ROPE",".2byte ITEM_MAX_REPEL",".2byte ITEM_NONE","release","end"}))
                    {assemblyData.Add(label,body.Take(7).Select(b=>b.Text).ToImmutableArray());continue;}
                }
                if(includeCinnabarData&&file.SourceRef.Commit==PretWorldMap.SourceCommit&&file.SourceRef.Repository=="pret/pokefirered"&&
                    (file.Path,file.SourceRef.Sha256) is
                        ("data/maps/CinnabarIsland_Mart/scripts.inc","f9a3d744dab7e62f3517cab34d7542f2fe1e0bda64fb63c159800c3886ebe4ac") or
                        ("data/maps/Route21_North/scripts.inc","6400ed7650028810cf2dec75a80f09c8f2141c75d7c6f051b10c01e868613ce5") or
                        ("data/maps/Route21_South/scripts.inc","905cecbf72714346866843161d1f120ffe8aa422c4bc5141131c1513974bdc06"))
                {
                    if(label is "CinnabarIsland_Mart_MapScripts" or "Route21_North_MapScripts" or "Route21_South_MapScripts"&&body.Count==1&&body[0].Text==".byte 0")
                    {headers.Add(label,[]);assemblyData.Add(label,[".byte 0"]);continue;}
                    if(label=="CinnabarIsland_Mart_Items"&&body.Select(b=>b.Text).SequenceEqual(new[]{
                        ".2byte ITEM_ULTRA_BALL",".2byte ITEM_GREAT_BALL",".2byte ITEM_HYPER_POTION",".2byte ITEM_REVIVE",
                        ".2byte ITEM_FULL_HEAL",".2byte ITEM_ESCAPE_ROPE",".2byte ITEM_MAX_REPEL",".2byte ITEM_NONE","release","end"}))
                    {assemblyData.Add(label,body.Take(8).Select(b=>b.Text).ToImmutableArray());continue;}
                }
                if(body[0].Text.StartsWith(".string",StringComparison.Ordinal))continue;
                if(body.Any(x=>x.Text.StartsWith("map_script ",StringComparison.Ordinal)))
                {
                    headers.Add(label,body.Where(x=>x.Text.StartsWith("map_script ",StringComparison.Ordinal)).Select(x=>{var a=x.Text[11..].Split(',').Select(s=>s.Trim()).ToArray();if(a.Length!=2)throw ContentJson.Error("map_header_arity",label,x.Text);return new PretMapScriptHeader(a[0],a[1]);}).ToImmutableArray());continue;
                }
                if(body.Any(x=>x.Text.StartsWith("map_script_2 ",StringComparison.Ordinal)))
                {
                    conditions.Add(label,body.Where(x=>x.Text.StartsWith("map_script_2 ",StringComparison.Ordinal)).Select(x=>{var a=x.Text[13..].Split(',').Select(s=>Alias(s.Trim())).ToArray();if(a.Length!=3)throw ContentJson.Error("map_condition_arity",label,x.Text);return new PretMapScriptCondition(a[0],a[1],a[2]);}).ToImmutableArray());continue;
                }
                if(body[^1].Text=="step_end")
                {movements.Add(label,body.Select(x=>new PretMovementCommand(x.Text,x.Line)).ToImmutableArray());continue;}
                var instructions=ImmutableArray.CreateBuilder<StoryInstruction>();
                foreach(var (lineNumber,line) in body)
                {
                    var split=line.Split((char[]?)null,2,StringSplitOptions.RemoveEmptyEntries);var op=split[0];
                    var args=split.Length==1?ImmutableArray<string>.Empty:split[1].Split(',').Select(s=>Alias(s.Trim())).ToImmutableArray();
                    // GNU assembler macro arguments may be separated by whitespace; the
                    // pinned nurse script uses `case 1 Label` without a comma.
                    if((op=="case"||includeRocketHideoutOpcodes&&op is "call_if_defeated" or "call_if_not_defeated")&&args.Length==1)
                    {
                        var spaced=Regex.Match(args[0],@"^(\S+)\s+([A-Za-z_][A-Za-z0-9_]*)$",RegexOptions.CultureInvariant);
                        if(spaced.Success)args=[Alias(spaced.Groups[1].Value),Alias(spaced.Groups[2].Value)];
                    }
                    void Emit(string opcode,ImmutableArray<string> operands)
                    {
                        bool snorlaxSource=includeSnorlaxOpcodes&&IsSnorlaxEventSource(file,label);
                        var (kind,min,max)=Shape(opcode,includeGiftOpcodes,includeRoute3Opcodes,includeMtMoonOpcodes,includeBillOpcodes,includeSsAnneOpcodes,includeVermilionGymOpcodes,includeRockTunnelOpcodes,includeRocketHideoutOpcodes,includePokemonTowerOpcodes,snorlaxSource);
                        if(includeSafariOpcodes&&opcode=="setwarp"&&file.Path=="data/scripts/safari_zone.inc"&&label=="SafariZone_EventScript_OutOfBallsMidBattle")
                        {
                            if(!operands.SequenceEqual(new[]{"MAP_FUCHSIA_CITY_SAFARI_ZONE_ENTRANCE","4","1"}))
                                throw ContentJson.Error("safari_setwarp_operands",$"{file.Path}:{lineNumber}",opcode);
                            (kind,min,max)=(StoryOp.Special,3,3);
                        }
                        if(includePresetDoubleOpcodes&&opcode is "trainerbattle_double" or "trainerbattle_rematch_double"&&file.Path=="data/scripts/trainers.inc"&&
                            label is "Route8_EventScript_Eli" or "Route8_EventScript_Anne" or "Route8_EventScript_EliRematch" or "Route8_EventScript_AnneRematch")
                        {
                            if(operands.Length!=4||operands[0]!="TRAINER_TWINS_ELI_ANNE")
                                throw ContentJson.Error("preset_double_script_operands",$"{file.Path}:{lineNumber}",opcode);
                            (kind,min,max)=(StoryOp.TrainerBattle,4,4);
                        }
                        if(includeCinnabarData&&includePresetDoubleOpcodes&&opcode is "trainerbattle_double" or "trainerbattle_rematch_double"&&
                            file.Path=="data/scripts/trainers.inc"&&file.SourceRef.Sha256=="d145a6d45e4143e3b215fdff82de07b78fd361aa695855e4065c043ffba36945"&&
                            label is "Route21_North_EventScript_Lil" or "Route21_North_EventScript_Ian" or "Route21_North_EventScript_LilRematch" or "Route21_North_EventScript_IanRematch")
                        {
                            if(operands.Length!=4||operands[0]!="TRAINER_SIS_AND_BRO_LIL_IAN")throw ContentJson.Error("cinnabar_double_script_operands",$"{file.Path}:{lineNumber}",opcode);
                            (kind,min,max)=(StoryOp.TrainerBattle,4,4);
                        }
                        // The successor only admits the two pinned static Snorlax call sites
                        // and the already-supported Tower preparation. Earlier profiles keep
                        // their original IR and diagnostics byte-for-byte.
                        if(includeSnorlaxOpcodes&&opcode=="setwildbattle"&&!snorlaxSource&&!IsTowerWildBattleSource(file,label))
                            (kind,min,max)=(StoryOp.Unsupported,0,int.MaxValue);
                        if(snorlaxSource&&opcode=="setwildbattle"&&!operands.SequenceEqual(new[]{"SPECIES_SNORLAX","30"}))
                            throw ContentJson.Error("snorlax_scripted_wild_operands",$"{file.Path}:{lineNumber}",opcode);
                        if(kind!=StoryOp.Unsupported&&(operands.Length<min||operands.Length>max))throw ContentJson.Error("story_opcode_arity",$"{file.Path}:{lineNumber}",$"{opcode} requires {min}..{max} arguments.");
                        if(kind==StoryOp.Unsupported)unsupported.Add(new("opcode_unsupported",$"{file.Path}:{lineNumber}",opcode));
                        instructions.Add(new(kind,opcode,operands,new(file.Path,label,lineNumber)));
                    }
                    // Literal expansion of pinned event.inc convenience macros. Original labels,
                    // standard subroutines and call-site provenance remain intact in the IR.
                    if(op is "giveitem" or "finditem")
                    {
                        if(args.Length is <1 or >2)throw ContentJson.Error("story_opcode_arity",$"{file.Path}:{lineNumber}",op);
                        Emit("setorcopyvar",["VAR_0x8000",args[0]]);Emit("setorcopyvar",["VAR_0x8001",args.Length==2?args[1]:"1"]);Emit("call",[op=="finditem"?"Std_FindItem":"Std_ObtainItem"]);
                    }
                    else if(op is "giveitem_msg" or "msgreceiveditem")
                    {
                        if(args.Length is <2 or >4)throw ContentJson.Error("story_opcode_arity",$"{file.Path}:{lineNumber}",op);
                        string quantity=args.Length>2?args[2]:"1",fanfare=args.Length>3?args[3]:"MUS_LEVEL_UP";
                        if(op=="giveitem_msg")Emit("additem",[args[1],quantity]);
                        Emit("loadword",["0",args[0]]);Emit("setorcopyvar",["VAR_0x8000",args[1]]);Emit("setorcopyvar",["VAR_0x8001",quantity]);Emit("setorcopyvar",["VAR_0x8002",fanfare]);Emit("call",["Std_ReceivedItem"]);
                    }
                    else if(op=="set_gym_trainers")
                    {
                        if(args.Length!=1)throw ContentJson.Error("story_opcode_arity",$"{file.Path}:{lineNumber}",op);
                        Emit("setvar",["VAR_0x8008",args[0]]);Emit("call",["EventScript_SetGymTrainers"]);
                    }
                    else if(op=="putitemaway")
                    {
                        if(args.Length is <1 or >2)throw ContentJson.Error("story_opcode_arity",$"{file.Path}:{lineNumber}",op);
                        Emit("setorcopyvar",["VAR_0x8000",args[0]]);Emit("setorcopyvar",["VAR_0x8001",args.Length>1?args[1]:"1"]);Emit("call",["Std_PutItemAway"]);
                    }
                    else if(op=="switch")
                    {if(args.Length!=1)throw ContentJson.Error("story_opcode_arity",$"{file.Path}:{lineNumber}",op);Emit("copyvar",["VAR_0x8000",args[0]]);}
                    else if(op=="case")
                    {if(args.Length!=2)throw ContentJson.Error("story_opcode_arity",$"{file.Path}:{lineNumber}",op);Emit("goto_if_eq",["VAR_0x8000",args[0],args[1]]);}
                    else Emit(op,args);
                }
                if(!programs.TryAdd(label,new(label,instructions.ToImmutable())))throw ContentJson.Error("duplicate_global_label",file.Path,label);
            }
        }
        return new(programs.ToImmutable(),headers.ToImmutable(),conditions.ToImmutable(),movements.ToImmutable(),unsupported.ToImmutable(), (constants ?? new Dictionary<string,int>()).ToImmutableDictionary(StringComparer.Ordinal)){AssemblyData=assemblyData.ToImmutable()};
    }
    private static bool IsSnorlaxEventSource(PretSourceScriptFile file,string label) =>
        file.SourceRef.Kind==SourceKind.Pret&&file.SourceRef.Repository=="pret/pokefirered"&&file.SourceRef.Commit==PretWorldMap.SourceCommit&&
        (file.Path,label,file.SourceRef.Sha256) is
            ("data/maps/Route12/scripts.inc","Route12_EventScript_Snorlax","ea70631fa17e3042cf08b32c94fbc26fbb128936b61c00e61127b79af780368e") or
            ("data/maps/Route16/scripts.inc","Route16_EventScript_Snorlax","680c86b2457428a348f8dd7ff006d13681c510cd5098c04b44f8f09a755a17a5");
    private static bool IsTowerWildBattleSource(PretSourceScriptFile file,string label) =>
        file.SourceRef.Kind==SourceKind.Pret&&file.SourceRef.Repository=="pret/pokefirered"&&file.SourceRef.Commit==PretWorldMap.SourceCommit&&
        file.Path=="data/maps/PokemonTower_6F/scripts.inc"&&label=="PokemonTower_6F_EventScript_MarowakGhost"&&
        file.SourceRef.Sha256=="9b761b8470ee034f3c3fea2c2e293ef631e75c2b848e3bf81daedc0ff30b2a90";
    private static (StoryOp,int,int) Shape(string op,bool includeGiftOpcodes,bool includeRoute3Opcodes,bool includeMtMoonOpcodes,bool includeBillOpcodes,bool includeSsAnneOpcodes,bool includeVermilionGymOpcodes,bool includeRockTunnelOpcodes,bool includeRocketHideoutOpcodes,bool includePokemonTowerOpcodes,bool includeSnorlaxOpcodes)
    {
        // TrainerBattle is the existing typed IR operation which yields a Battle wait.
        // Keep the original opcode so Application dispatches the static battle separately.
        if(includeSnorlaxOpcodes&&op=="dowildbattle")return(StoryOp.TrainerBattle,0,0);
        if(includeSnorlaxOpcodes&&op=="setwildbattle")return(StoryOp.Special,2,2);
        if(includePokemonTowerOpcodes&&op=="setwildbattle")return(StoryOp.Special,2,3);
        if(includeRocketHideoutOpcodes&&op=="multichoicedefault")return(StoryOp.Multichoice,5,5);
        if(includeRocketHideoutOpcodes&&op is "call_if_defeated" or "call_if_not_defeated" or "addvar")return(StoryOp.Special,2,2);
        if(includeRocketHideoutOpcodes&&op=="setdynamicwarp")return(StoryOp.Special,4,4);
        // Only the Rock Tunnel successor admits the source Flash script.
        if(includeRockTunnelOpcodes&&op=="animateflash")return(StoryOp.ScreenFade,1,1);
        if(includeRockTunnelOpcodes&&op=="setflashlevel")return(StoryOp.Special,1,1);
        if(includeVermilionGymOpcodes&&op=="checkpartymove")return(StoryOp.Special,1,1);
        if(includeVermilionGymOpcodes&&op is "setfieldeffectargument" or "bufferpartymonnick" or "buffermovename")return(StoryOp.Special,2,2);
        if(includeSsAnneOpcodes&&op=="getplayerxy")return(StoryOp.Special,2,2);
        if(includeSsAnneOpcodes&&op=="fadenewbgm")return(StoryOp.Music,1,1);
        // ScrCmd_fadeinbgm is asynchronous. Older content profiles still reject this opcode.
        if(includeBillOpcodes&&op=="fadeinbgm")return(StoryOp.Music,1,1);
        if(op is "goto_if_eq" or "goto_if_ne" or "goto_if_lt" or "goto_if_le" or "goto_if_gt" or "goto_if_ge")return(StoryOp.ConditionalGoto,3,3);
        if(op is "call_if_eq" or "call_if_ne" or "call_if_lt" or "call_if_le" or "call_if_gt" or "call_if_ge")return(StoryOp.ConditionalCall,3,3);
        if(includeMtMoonOpcodes&&op=="trainerbattle_no_intro")return(StoryOp.TrainerBattle,2,2);
        if(includeMtMoonOpcodes&&op is "goto_if_defeated" or "goto_if_not_defeated")return(StoryOp.Special,2,2);
        if(includeMtMoonOpcodes&&op=="copyobjectxytoperm")return(StoryOp.Special,1,1);
        if(includeRoute3Opcodes&&op=="trainerbattle_rematch")return(StoryOp.TrainerBattle,3,3);
        if(includeGiftOpcodes)
        {
            var gift=op switch
            {
                "showmoneybox" or "bufferboxname" or "subvar" => (StoryOp.Special,2,2),
                "hidemoneybox" or "updatemoneybox" or "checkplayergender" or "getpartysize" => (StoryOp.Special,0,0),
                "checkmoney" or "removemoney" => (StoryOp.Special,1,1),
                _ => (StoryOp.Unsupported,0,0)
            };
            if(gift.Item1!=StoryOp.Unsupported)return gift;
        }
        return op switch
        {
            "goto_if_questlog"=>(StoryOp.QuestLogJump,1,1),
            "end"=>(StoryOp.End,0,0),"return"=>(StoryOp.Return,0,0),"goto"=>(StoryOp.Goto,1,1),"call"=>(StoryOp.Call,1,1),
            "setflag" or "setworldmapflag"=>(StoryOp.SetFlag,1,1),"clearflag"=>(StoryOp.ClearFlag,1,1),"setvar" or "setorcopyvar"=>(StoryOp.SetVar,2,2),"copyvar"=>(StoryOp.CopyVar,2,2),
            "goto_if_set" or "goto_if_unset"=>(StoryOp.FlagGoto,2,2),"call_if_set" or "call_if_unset"=>(StoryOp.FlagCall,2,2),
            "lock" or "lockall"=>(StoryOp.Lock,0,0),"release" or "releaseall"=>(StoryOp.Release,0,0),
            "multichoice"=>(StoryOp.Multichoice,4,4),"msgbox"=>(StoryOp.Dialog,1,2),"message"=>(StoryOp.Message,1,1),"waitmessage"=>(StoryOp.WaitMessage,0,0),"waitbuttonpress"=>(StoryOp.WaitButtonPress,0,0),"closemessage"=>(StoryOp.CloseMessage,0,0),"delay"=>(StoryOp.Delay,1,1),
            "applymovement"=>(StoryOp.ApplyMovement,2,3),"waitmovement"=>(StoryOp.WaitMovement,1,1),"warp"=>(StoryOp.Warp,3,4),"waitstate"=>(StoryOp.WaitState,0,0),
            "setobjectxyperm"=>(StoryOp.ObjectPosition,3,3),"setobjectmovementtype"=>(StoryOp.ObjectMovementType,2,2),"addobject" or "removeobject"=>(StoryOp.ObjectVisible,1,2),"turnobject"=>(StoryOp.TurnObject,2,2),
            "setrespawn"=>(StoryOp.Respawn,1,1),"faceplayer"=>(StoryOp.FacePlayer,0,0),"textcolor"=>(StoryOp.TextColor,1,1),"playbgm"=>(StoryOp.Music,2,2),"savebgm"=>(StoryOp.Music,1,1),"fadedefaultbgm"=>(StoryOp.Music,0,0),
            "dofieldeffect" or "incrementgamestat"=>(StoryOp.Special,1,1),"specialvar"=>(StoryOp.Special,2,2),"waitfieldeffect"=>(StoryOp.Sound,1,1),
            "playse" or "playfanfare"=>(StoryOp.Sound,1,1),"waitfanfare" or "waitse"=>(StoryOp.Sound,0,0),
            "playmoncry"=>(StoryOp.Sound,2,2),"waitmoncry"=>(StoryOp.Sound,0,0),"fadeoutbgm"=>(StoryOp.Music,1,1),
            "opendoor" or "closedoor"=>(StoryOp.Door,2,2),"waitdooranim"=>(StoryOp.Door,0,0),"famechecker"=>(StoryOp.FameChecker,2,3),
            "showmonpic"=>(StoryOp.PokemonPicture,3,3),"hidemonpic"=>(StoryOp.PokemonPicture,0,0),"givemon"=>(StoryOp.GivePokemon,2,6),"bufferspeciesname"=>(StoryOp.BufferSpecies,2,2),"fadescreen"=>(StoryOp.ScreenFade,1,1),"special"=>(StoryOp.Special,1,1),
            // These source opcodes change message state. They yield a native effect and retain
            // their own opcode, so an absent application handler faults instead of skipping them.
            "signmsg" or "normalmsg"=>(StoryOp.Special,0,0),
            // Original default quantity is one. Keep operands/source opcode intact; the
            // bounded application handler owns inventory validation and VAR_RESULT.
            "checkitem" or "checkitemspace" or "additem" or "removeitem"=>(StoryOp.Special,1,2),
            "buffernumberstring" or "bufferitemname" or "bufferstdstring" or "loadword"=>(StoryOp.Special,2,2),
            "bufferitemnameplural"=>(StoryOp.Special,3,3),"checkitemtype"=>(StoryOp.Special,1,1),"setmetatile"=>(StoryOp.Special,4,4),
            "pokemart"=>(StoryOp.Special,1,1),
            "trainerbattle_earlyrival"=>(StoryOp.TrainerBattle,4,4),
            "trainerbattle_single"=>(StoryOp.TrainerBattle,3,5),

            "settrainerflag"=>(StoryOp.Special,1,1),
            _=>(StoryOp.Unsupported,0,int.MaxValue)
        };
    }
    [GeneratedRegex(@"^([A-Za-z_][A-Za-z0-9_]*):{1,2}$",RegexOptions.CultureInvariant)]private static partial Regex Label();
    [GeneratedRegex(@"^\.equ\s+([A-Za-z_][A-Za-z0-9_]*),\s*([A-Za-z_][A-Za-z0-9_]*|0x[0-9a-fA-F]+|[0-9]+)$",RegexOptions.CultureInvariant)]private static partial Regex Equ();
}
