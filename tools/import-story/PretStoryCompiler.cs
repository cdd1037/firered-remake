using System.Collections.Immutable;
using System.Text;
using System.Text.RegularExpressions;
using Game.Domain.Events;
namespace Game.Content;

/// <summary>Compiles source syntax to finite typed IR. Unsupported instructions remain explicit fault nodes.</summary>
public static partial class PretStoryCompiler
{
    public static PretStoryCompilation Compile(IEnumerable<PretSourceScriptFile> files,IReadOnlyDictionary<string,int>? constants = null,bool includeGiftOpcodes=false,bool includeRoute3Opcodes=false,bool includeMtMoonOpcodes=false,bool includeBillOpcodes=false,bool includeSsAnneOpcodes=false,bool includeVermilionGymOpcodes=false,bool includeRockTunnelOpcodes=false,bool includeRocketHideoutOpcodes=false,bool includePokemonTowerOpcodes=false,bool includeSnorlaxOpcodes=false,bool includePresetDoubleOpcodes=false,bool includeSafariOpcodes=false,bool includeSilphOpcodes=false,bool includeSaffronGymData=false,bool includeCinnabarData=false,bool includeFirstSevii=false,bool includeViridianGym=false,bool includeVictoryRoad=false,bool includePokemonLeague=false,bool includeSeviiRuby=false,bool includeFourIsland=false,bool includeSixIsland=false,bool includeFiveIsland=false,bool includeCelio=false,bool includeSevenIsland=false,bool includeEventIslands=false,bool includePowerPlant=false,bool includeTrainerTower=false,bool includeSeafoam=false,bool includeGameCornerCoins=false,bool includeGameCornerSlots=false)
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
            var aliases=new Dictionary<string,string>(StringComparer.Ordinal);
            var blocks=new Dictionary<string,List<(int Line,string Text)>>(StringComparer.Ordinal);List<(int,string)>? current=null;
            var rawLines=file.Text.Replace("\r\n","\n",StringComparison.Ordinal).Split('\n');
            if(includeGameCornerSlots)rawLines=SelectOriginalSlotLines(file,rawLines);
            // Edition selection is source-gated; unknown files retain explicit unsupported directives.
            if (PretGameCornerPrizeRoom.IsOriginalScript(file))
                rawLines = PretAssemblySourceSelection.SelectFireRedLines(file.Path, rawLines);
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
                var equ=Equ().Match(line);
                if(!equ.Success&&includeSevenIsland&&IsSevenIslandDanceAlias(file,line))equ=Equ().Match(".equ DID_CHANSEY_DANCE, FLAG_TEMP_2");if(equ.Success){aliases[equ.Groups[1].Value]=equ.Groups[2].Value;continue;}
                var label=Label().Match(line);if(label.Success){current=[];if(!blocks.TryAdd(label.Groups[1].Value,current))throw ContentJson.Error("duplicate_label",file.Path,label.Groups[1].Value);continue;}
                if(line.StartsWith(".include",StringComparison.Ordinal)||line.StartsWith(".align",StringComparison.Ordinal))continue;
                current?.Add((sourceLine,line));
            }
            if (includeTrainerTower) CompleteTrainerTowerSourceBlocks(file, blocks);
            string Alias(string operand)
            {
                var seen=new HashSet<string>(StringComparer.Ordinal);
                while(aliases.TryGetValue(operand,out var next)){if(!seen.Add(operand))throw ContentJson.Error("alias_cycle",file.Path,operand);operand=next;}
                return operand;
            }
            foreach(var (label,body) in blocks)
            {
                if(body.Count==0)continue;
                if(IsEmptySourceMapHeader(file,label,body.Select(b=>b.Text).ToArray()))
                {headers.Add(label,[]);assemblyData.Add(label,[".byte 0"]);continue;}
                // Known declarations are data; other rows keep their script semantics.
                if(includeSaffronGymData&&
                    file.Path is
                        "data/maps/SaffronCity_Gym/scripts.inc" or
                        "data/maps/SaffronCity_Mart/scripts.inc")
                {
                    if(label is "SaffronCity_Gym_MapScripts" or "SaffronCity_Mart_MapScripts"&&body.Count==1&&body[0].Text==".byte 0")
                    {headers.Add(label,[]);assemblyData.Add(label,[".byte 0"]);continue;}
                    if(label=="SaffronCity_Mart_Items"&&body.Select(b=>b.Text).SequenceEqual(new[]{
                        ".2byte ITEM_GREAT_BALL",".2byte ITEM_HYPER_POTION",".2byte ITEM_REVIVE",".2byte ITEM_FULL_HEAL",".2byte ITEM_ESCAPE_ROPE",".2byte ITEM_MAX_REPEL",".2byte ITEM_NONE","release","end"}))
                    {assemblyData.Add(label,body.Take(7).Select(b=>b.Text).ToImmutableArray());continue;}
                }
                if(includeCinnabarData&&
                    file.Path is
                        "data/maps/CinnabarIsland_Mart/scripts.inc" or
                        "data/maps/Route21_North/scripts.inc" or
                        "data/maps/Route21_South/scripts.inc")
                {
                    if(label is "CinnabarIsland_Mart_MapScripts" or "Route21_North_MapScripts" or "Route21_South_MapScripts"&&body.Count==1&&body[0].Text==".byte 0")
                    {headers.Add(label,[]);assemblyData.Add(label,[".byte 0"]);continue;}
                    if(label=="CinnabarIsland_Mart_Items"&&body.Select(b=>b.Text).SequenceEqual(new[]{
                        ".2byte ITEM_ULTRA_BALL",".2byte ITEM_GREAT_BALL",".2byte ITEM_HYPER_POTION",".2byte ITEM_REVIVE",".2byte ITEM_FULL_HEAL",".2byte ITEM_ESCAPE_ROPE",".2byte ITEM_MAX_REPEL",".2byte ITEM_NONE","release","end"}))
                    {assemblyData.Add(label,body.Take(8).Select(b=>b.Text).ToImmutableArray());continue;}
                }
                if(includeViridianGym&&
                    file.Path=="data/maps/ViridianCity_Gym/scripts.inc"&&
                    label=="ViridianCity_Gym_MapScripts"&&body.Count==1&&body[0].Text==".byte 0")
                {headers.Add(label,[]);assemblyData.Add(label,[".byte 0"]);continue;}
                if(includeFirstSevii&&IsFirstSeviiAssemblyData(file,label,body.Select(b=>b.Text).ToArray()))
                {
                    if(body.Count==1&&body[0].Text==".byte 0")headers.Add(label,[]);
                    assemblyData.Add(label,body.Where(b=>b.Text.StartsWith(".",StringComparison.Ordinal)).Select(b=>b.Text).ToImmutableArray());continue;
                }
                if(includePokemonLeague&&IsPokemonLeagueAssemblyData(file,label,body.Select(b=>b.Text).ToArray()))
                {assemblyData.Add(label,body.Take(8).Select(b=>b.Text).ToImmutableArray());continue;}
                if(includeSeviiRuby&&IsSeviiRubyAssemblyData(file,label,body.Select(b=>b.Text).ToArray()))
                {headers.Add(label,[]);assemblyData.Add(label,[".byte 0"]);continue;}
                if(includeFourIsland&&IsFourIslandAssemblyData(file,label,body.Select(b=>b.Text).ToArray()))
                {
                    if(body.Count==1&&body[0].Text==".byte 0")headers.Add(label,[]);
                    assemblyData.Add(label,body.Where(b=>b.Text.StartsWith(".",StringComparison.Ordinal)).Select(b=>b.Text).ToImmutableArray());continue;
                }
                if(includeSixIsland&&IsSixIslandAssemblyData(file,label,body.Select(b=>b.Text).ToArray()))
                {
                    if(body.Count==1&&body[0].Text==".byte 0")headers.Add(label,[]);
                    assemblyData.Add(label,body.Where(b=>b.Text.StartsWith(".",StringComparison.Ordinal)).Select(b=>b.Text).ToImmutableArray());continue;
                }
                if(includeSevenIsland&&IsSevenIslandAssemblyData(file,label,body.Select(b=>b.Text).ToArray()))
                {
                    if(body.Count==1&&body[0].Text==".byte 0")headers.Add(label,[]);
                    assemblyData.Add(label,body.Where(b=>b.Text.StartsWith(".",StringComparison.Ordinal)).Select(b=>b.Text).ToImmutableArray());continue;
                }
                if(includeEventIslands&&IsEventIslandAssemblyData(file,label,body.Select(b=>b.Text).ToArray()))
                {headers.Add(label,[]);assemblyData.Add(label,[".byte 0"]);continue;}
                if(includeFiveIsland&&IsFiveIslandAssemblyData(file,label,body.Select(b=>b.Text).ToArray()))
                {headers.Add(label,[]);assemblyData.Add(label,[".byte 0"]);continue;}
                if(includeGiftOpcodes&&IsOriginalTmAcquisitionAssemblyData(file,label,body.Select(b=>b.Text).ToArray()))
                {headers.Add(label,[]);assemblyData.Add(label,[".byte 0"]);continue;}
                if (FirstSeviiSource(file,"data/maps/CeruleanCave_2F/scripts.inc") &&
                    label == "CeruleanCave_2F_MapScripts" && body.Count == 1 && body[0].Text == ".byte 0")
                { headers.Add(label, []); assemblyData.Add(label, [".byte 0"]); continue; }
                if (FirstSeviiSource(file,"data/maps/SaffronCity_Dojo/scripts.inc") &&
                    label == "SaffronCity_Dojo_MapScripts" && body.Count == 1 && body[0].Text == ".byte 0")
                { headers.Add(label, []); assemblyData.Add(label, [".byte 0"]); continue; }
                if (FirstSeviiSource(file,"data/maps/CinnabarIsland_PokemonLab_ExperimentRoom/scripts.inc") &&
                    label == "CinnabarIsland_PokemonLab_ExperimentRoom_MapScripts" && body.Count == 1 && body[0].Text == ".byte 0")
                { headers.Add(label, []); assemblyData.Add(label, [".byte 0"]); continue; }
                if (((FirstSeviiSource(file,"data/maps/TwoIsland_CapeBrink/scripts.inc") &&
                    label == "TwoIsland_CapeBrink_MapScripts") ||
                    (FirstSeviiSource(file,"data/maps/TwoIsland_CapeBrink_House/scripts.inc") &&
                    label == "TwoIsland_CapeBrink_House_MapScripts")) && body.Count == 1 && body[0].Text == ".byte 0")
                { headers.Add(label, []); assemblyData.Add(label, [".byte 0"]); continue; }
                if (((FirstSeviiSource(file,"data/maps/FourIsland_House1/scripts.inc") &&
                    label == "FourIsland_House1_MapScripts") ||
                    (FirstSeviiSource(file,"data/maps/CinnabarIsland_PokemonLab_ResearchRoom/scripts.inc") &&
                    label == "CinnabarIsland_PokemonLab_ResearchRoom_MapScripts")) && body.Count == 1 && body[0].Text == ".byte 0")
                { headers.Add(label, []); assemblyData.Add(label, [".byte 0"]); continue; }
                if (FirstSeviiSource(file,"data/maps/Route12_FishingHouse/scripts.inc") &&
                    label == "Route12_FishingHouse_MapScripts" && body.Count == 1 && body[0].Text == ".byte 0")
                { headers.Add(label, []); assemblyData.Add(label, [".byte 0"]); continue; }
                if (FirstSeviiSource(file,"data/maps/SaffronCity_CopycatsHouse_1F/scripts.inc") &&
                    label == "SaffronCity_CopycatsHouse_1F_MapScripts" && body.Count == 1 && body[0].Text == ".byte 0")
                { headers.Add(label, []); assemblyData.Add(label, [".byte 0"]); continue; }
                if (FirstSeviiSource(file,"data/maps/SaffronCity_CopycatsHouse_2F/scripts.inc") &&
                    label == "SaffronCity_CopycatsHouse_2F_MapScripts" && body.Count == 1 && body[0].Text == ".byte 0")
                { headers.Add(label, []); assemblyData.Add(label, [".byte 0"]); continue; }
                if (FirstSeviiSource(file,"data/maps/FourIsland_PokemonDayCare/scripts.inc") &&
                    label == "FourIsland_PokemonDayCare_MapScripts" && body.Count == 1 && body[0].Text == ".byte 0")
                { headers.Add(label, []); assemblyData.Add(label, [".byte 0"]); continue; }
                if (FirstSeviiSource(file,"data/maps/Route5_PokemonDayCare/scripts.inc") &&
                    label == "Route5_PokemonDayCare_MapScripts" && body.Count == 1 && body[0].Text == ".byte 0")
                { headers.Add(label, []); assemblyData.Add(label, [".byte 0"]); continue; }
                if (FirstSeviiSource(file,"data/maps/FuchsiaCity_House3/scripts.inc") &&
                    label == "FuchsiaCity_House3_MapScripts" && body.Count == 1 && body[0].Text == ".byte 0")
                { headers.Add(label, []); assemblyData.Add(label, [".byte 0"]); continue; }
                if(includeTrainerTower&&IsTrainerTowerAssemblyData(file,label,body.Select(b=>b.Text).ToArray()))
                {assemblyData.Add(label,body.Where(b=>b.Text.StartsWith(".",StringComparison.Ordinal)).Select(b=>b.Text).ToImmutableArray());continue;}
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
                        if (includeTrainerTower && TryCompileTrainerTowerInstruction(file, label, lineNumber, opcode, operands, out var towerInstruction))
                        {
                            // The native battle wait subsumes its immediate source waitstate.
                            if (towerInstruction is not null)
                            {
                                instructions.Add(towerInstruction);
                                if (towerInstruction.Op == StoryOp.Unsupported)
                                    unsupported.Add(new("opcode_unsupported", $"{file.Path}:{lineNumber}", opcode));
                            }
                            return;
                        }
                        bool snorlaxSource=includeSnorlaxOpcodes&&IsSnorlaxEventSource(file,label);
                        var (kind,min,max)=Shape(opcode,includeGiftOpcodes,includeRoute3Opcodes,includeMtMoonOpcodes,includeBillOpcodes,includeSsAnneOpcodes,includeVermilionGymOpcodes,includeRockTunnelOpcodes,includeRocketHideoutOpcodes,includePokemonTowerOpcodes,snorlaxSource);
                        if (opcode == "bufferleadmonspeciesname" &&
                            FirstSeviiSource(file,"data/scripts/move_tutors.inc") &&
                            label == "TwoIsland_CapeBrink_House_EventScript_StarterTutor")
                        {
                            if (!operands.SequenceEqual(new[] { "STR_VAR_1" }))
                                throw ContentJson.Error("cape_brink_lead_buffer_operands", $"{file.Path}:{lineNumber}", opcode);
                            (kind,min,max) = (StoryOp.Special,1,1);
                        }
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
                        if(includePresetDoubleOpcodes && opcode is "trainerbattle_double" or "trainerbattle_rematch_double" &&
                            file.Path=="data/scripts/trainers.inc" &&
                            label is "Route16_EventScript_Lea" or "Route16_EventScript_Jed" or "Route16_EventScript_LeaRematch" or "Route16_EventScript_JedRematch")
                        {
                            if(operands.Length!=4 || operands[0]!="TRAINER_YOUNG_COUPLE_LEA_JED")
                                throw ContentJson.Error("route16_double_script_operands",$"{file.Path}:{lineNumber}",opcode);
                            (kind,min,max)=(StoryOp.TrainerBattle,4,4);
                        }
                        if(includeCinnabarData&&includePresetDoubleOpcodes&&opcode is "trainerbattle_double" or "trainerbattle_rematch_double"&&
                            file.Path=="data/scripts/trainers.inc"&&
                            label is "Route21_North_EventScript_Lil" or "Route21_North_EventScript_Ian" or "Route21_North_EventScript_LilRematch" or "Route21_North_EventScript_IanRematch")
                        {
                            if(operands.Length!=4||operands[0]!="TRAINER_SIS_AND_BRO_LIL_IAN")throw ContentJson.Error("cinnabar_double_script_operands",$"{file.Path}:{lineNumber}",opcode);
                            (kind,min,max)=(StoryOp.TrainerBattle,4,4);
                        }
                        if(includeVictoryRoad&&includePresetDoubleOpcodes&&opcode=="trainerbattle_double"&&
                            FirstSeviiSource(file,"data/maps/VictoryRoad_3F/scripts.inc")&&
                            label is "VictoryRoad_3F_EventScript_Ray" or "VictoryRoad_3F_EventScript_Tyra")
                        {
                            if(operands.Length!=4||operands[0]!="TRAINER_COOL_COUPLE_RAY_TYRA")
                                throw ContentJson.Error("victory_road_double_operands",$"{file.Path}:{lineNumber}",opcode);
                            (kind,min,max)=(StoryOp.TrainerBattle,4,4);
                        }
                        // The successor only admits the two pinned static Snorlax call sites
                        // and the already-supported Tower preparation. Earlier profiles keep
                        // their original IR and diagnostics byte-for-byte.
                        if(includeSnorlaxOpcodes&&opcode=="setwildbattle"&&!snorlaxSource&&!IsTowerWildBattleSource(file,label))
                            (kind,min,max)=(StoryOp.Unsupported,0,int.MaxValue);
                        if(snorlaxSource&&opcode=="setwildbattle"&&!operands.SequenceEqual(new[]{"SPECIES_SNORLAX","30"}))
                            throw ContentJson.Error("snorlax_scripted_wild_operands",$"{file.Path}:{lineNumber}",opcode);
                        if (CeruleanCaveInstructionShape(file, label, opcode, operands) is { } caveShape)
                            (kind,min,max) = caveShape;
                        if(includeFirstSevii)
                        {
                            var admitted=FirstSeviiInstructionShape(file,label,opcode,operands);
                            if(admitted is {} shape)(kind,min,max)=shape;
                        }
                        if(includePokemonLeague)
                        {
                            var admitted=PokemonLeagueInstructionShape(file,label,opcode,operands);
                            if(admitted is {} shape)(kind,min,max)=shape;
                        }
                        if(includeSeviiRuby)
                        {
                            var admitted=SeviiRubyInstructionShape(file,label,opcode,operands);
                            if(admitted is {} shape)(kind,min,max)=shape;
                        }
                        if(file.Path=="data/maps/FourIsland_IcefallCave_Back/scripts.inc"&&opcode=="trainerbattle_no_intro")
                            (kind,min,max)=(StoryOp.Unsupported,0,int.MaxValue);
                        if(includeFourIsland)
                        {
                            var admitted=FourIslandInstructionShape(file,label,opcode,operands);
                            if(admitted is {} shape)(kind,min,max)=shape;
                        }
                        if(includeSixIsland)
                        {
                            var admitted=SixIslandInstructionShape(file,label,opcode,operands);
                            if(admitted is {} shape)(kind,min,max)=shape;
                        }
                        // An independently authenticated successor map uses the shared opcode shapes.
                        // Keep the older source-specific warehouse contracts and reject unmounted files.
                        if(includeFiveIsland && (IsFiveIslandMapSource(file) ||
                            !IsMapScriptSource(file)))
                        {
                            var admitted=FiveIslandInstructionShape(file,label,opcode,operands);
                            if(admitted is {} shape)(kind,min,max)=shape;
                        }
                        if(includeSevenIsland&&SevenIslandInstructionShape(file,label,opcode,operands) is {} sevenShape)
                            (kind,min,max)=sevenShape;
                        if(includeEventIslands&&EventIslandInstructionShape(file,label,opcode,operands) is {} eventIslandShape)
                            (kind,min,max)=eventIslandShape;
                        if(includePowerPlant&&PowerPlantInstructionShape(file,label,opcode,operands) is {} powerPlantShape)
                            (kind,min,max)=powerPlantShape;
                        if(includeSeafoam&&SeafoamInstructionShape(file,label,opcode,operands) is {} seafoamShape)
                            (kind,min,max)=seafoamShape;
                        if(includeGameCornerCoins&&GameCornerPrizesInstructionShape(file,label,opcode,operands) is {} prizeShape)
                            (kind,min,max)=prizeShape;
                        if(includeGameCornerSlots&&SlotMachineInstructionShape(file,label,opcode,operands) is {} slotShape)
                            (kind,min,max)=slotShape;
                        if(includeGameCornerCoins&&GameCornerCoinsInstructionShape(file,label,opcode,operands) is {} coinShape)
                            (kind,min,max)=coinShape;
                        if(includeCelio)
                        {
                            var admitted=CelioInstructionShape(file,label,opcode,operands);
                            if(admitted is {} shape)(kind,min,max)=shape;
                        }
                        if(includeGiftOpcodes&&OriginalTmAcquisitionInstructionShape(file,label,opcode,operands) is {} originalTmShape)
                            (kind,min,max)=originalTmShape;
                        if(kind!=StoryOp.Unsupported&&(operands.Length<min||operands.Length>max))throw ContentJson.Error("story_opcode_arity",$"{file.Path}:{lineNumber}",$"{opcode} requires {min}..{max} arguments.");
                        if(kind==StoryOp.Unsupported)unsupported.Add(new("opcode_unsupported",$"{file.Path}:{lineNumber}",opcode));
                        instructions.Add(new(kind,opcode,operands,new(file.Path,label,lineNumber)));
                    }
                    // Literal expansion of pinned event.inc convenience macros. Original labels,
                    // standard subroutines and call-site provenance remain intact in the IR.
                    if(includeSeviiRuby&&op=="braillemessage_wait"&&
                        SeviiRubyMapSources.Contains(file.Path)&&FirstSeviiSource(file,file.Path)&&
                        file.Path=="data/maps/MtEmber_RubyPath_B5F/scripts.inc"&&label=="MtEmber_RubyPath_B5F_EventScript_BrailleMessage")
                    {
                        if(args.Length!=1||!args[0].StartsWith("Braille_Text_",StringComparison.Ordinal))
                            throw ContentJson.Error("sevii_ruby_braille_operands",$"{file.Path}:{lineNumber}",op);
                        // Literal source macro from asm/macros/event.inc:1845-1850.
                        Emit("setvar",["VAR_0x8006","0"]);
                        Emit("braillemessage",args);
                        Emit("getbraillestringwidth",args);
                        Emit("call",["EventScript_BrailleCursorWaitButton"]);
                    }
                    else if(includeSixIsland&&op=="braillemessage_wait"&&IsSixIslandBrailleWait(file,label))
                    {
                        if(args.Length!=1||!args[0].StartsWith("Braille_Text_",StringComparison.Ordinal))
                            throw ContentJson.Error("six_island_braille_operands",$"{file.Path}:{lineNumber}",op);
                        // Exact asm/macros/event.inc:1845-1850, preserving each source line.
                        Emit("setvar",["VAR_0x8006","0"]);
                        Emit("braillemessage",args);
                        Emit("getbraillestringwidth",args);
                        Emit("call",["EventScript_BrailleCursorWaitButton"]);
                    }
                    else if(op is "giveitem" or "finditem")
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
        return new(programs.ToImmutable(),headers.ToImmutable(),conditions.ToImmutable(),movements.ToImmutable(),unsupported.ToImmutable(), (constants ?? new Dictionary<string,int>()).ToImmutableDictionary(StringComparer.Ordinal)){AssemblyData=assemblyData.ToImmutableDictionary(pair => pair.Key, pair => pair.Value.Select(line =>
        {
            var fields = line.Split((char[]?)null, 2, StringSplitOptions.RemoveEmptyEntries);
            return new PretStoryDataDeclaration(fields[0], fields.Length == 1 ? [] : fields[1].Split(',').Select(value => value.Trim()).ToImmutableArray());
        }).ToImmutableArray(), StringComparer.Ordinal)};
    }
    private static (StoryOp,int,int)? GameCornerPrizesInstructionShape(PretSourceScriptFile file,string label,string opcode,ImmutableArray<string> operands)
    {
        if (!PretGameCornerPrizeRoom.IsOriginalScript(file)) return null;
        const string prefix = "CeladonCity_GameCorner_PrizeRoom_EventScript_";
        string? suffix = label.StartsWith(prefix, StringComparison.Ordinal) ? label[prefix.Length..] : null;
        string[]? expected = (suffix,opcode) switch
        {
            ("PrizeClerkMons" or "PrizeClerkTMs" or "PrizeClerkItems","showcoinsbox") => ["0", "0"],
            ("EndPrizeExchange" or "PartyFull" or "BagFull","hidecoinsbox") => ["0", "0"],
            ("ConfirmPrizeMon" or "TryGivePrize","checkcoins") => ["VAR_RESULT"],
            ("ReceivedMonParty" or "ReceivedMonPC" or "TryGivePrize","removecoins") => ["VAR_TEMP_2"],
            ("ReceivedMonParty" or "ReceivedMonPC" or "TryGivePrize","updatecoinsbox") => ["0", "5"],
            _ => null
        };
        if (expected is null) return null;
        if (!operands.SequenceEqual(expected)) throw ContentJson.Error("game_corner_prize_operands", file.Path + ":" + label, opcode);
        return (StoryOp.Special,expected.Length,expected.Length);
    }
    private static (StoryOp,int,int)? GameCornerCoinsInstructionShape(PretSourceScriptFile file,string label,string opcode,ImmutableArray<string> operands)
    {
        if (FirstSeviiSource(file,PretGameCornerHiddenCoins.ScriptPath) &&
            label == "EventScript_TryPickUpHiddenCoins" && opcode is "checkcoins" or "addcoins")
        {
            string argument = opcode == "checkcoins" ? "VAR_RESULT" : "VAR_0x8006";
            if (!operands.SequenceEqual(new[] { argument }))
                throw ContentJson.Error("hidden_coins_operands", file.Path + ":" + label, opcode);
            return (StoryOp.Special,1,1);
        }
        if (!FirstSeviiSource(file,PretGameCornerCoins.ScriptPath)) return null;
        const string prefix = "CeladonCity_GameCorner_EventScript_";
        string? suffix = label.StartsWith(prefix, StringComparison.Ordinal) ? label[prefix.Length..] : null;
        string[]? expected = (suffix,opcode) switch
        {
            ("CoinsClerk","showcoinsbox") or ("BoughtCoins","updatecoinsbox") or ("ClerkEnd","hidecoinsbox") => ["0", "5"],
            ("Buy50Coins" or "Buy500Coins" or "Fisher" or "Scientist" or "Gentleman","checkcoins") => ["VAR_TEMP_1"],
            ("Buy50Coins","addcoins") => ["50"],
            ("Buy500Coins","addcoins") => ["500"],
            ("Fisher","addcoins") => ["10"],
            ("Scientist" or "Gentleman","addcoins") => ["20"],
            _ => null
        };
        if (expected is null) return null;
        if (!operands.SequenceEqual(expected))
            throw ContentJson.Error("game_corner_coins_operands", file.Path + ":" + label, opcode);
        return (StoryOp.Special,expected.Length,expected.Length);
    }
    private static bool IsSnorlaxEventSource(PretSourceScriptFile file,string label) =>
        file.SourceRef.Kind==SourceKind.Pret&&
        (file.Path,label) is
            ("data/maps/Route12/scripts.inc","Route12_EventScript_Snorlax") or
            ("data/maps/Route16/scripts.inc","Route16_EventScript_Snorlax");
    private static bool IsTowerWildBattleSource(PretSourceScriptFile file,string label) =>
        file.SourceRef.Kind==SourceKind.Pret&&
        file.Path=="data/maps/PokemonTower_6F/scripts.inc"&&label=="PokemonTower_6F_EventScript_MarowakGhost";
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
            "multichoicegrid"=>(StoryOp.Multichoice,5,5),"multichoice"=>(StoryOp.Multichoice,4,4),"msgbox"=>(StoryOp.Dialog,1,2),"message"=>(StoryOp.Message,1,1),"waitmessage"=>(StoryOp.WaitMessage,0,0),"waitbuttonpress"=>(StoryOp.WaitButtonPress,0,0),"closemessage"=>(StoryOp.CloseMessage,0,0),"delay"=>(StoryOp.Delay,1,1),
            "applymovement"=>(StoryOp.ApplyMovement,2,3),"waitmovement"=>(StoryOp.WaitMovement,1,1),"warp"=>(StoryOp.Warp,3,4),"waitstate"=>(StoryOp.WaitState,0,0),
            "setmaplayoutindex"=>(StoryOp.Special,1,1),
            "setobjectxyperm"=>(StoryOp.ObjectPosition,3,3),"setobjectmovementtype"=>(StoryOp.ObjectMovementType,2,2),"addobject" or "removeobject"=>(StoryOp.ObjectVisible,1,2),"turnobject"=>(StoryOp.TurnObject,2,2),
            "setrespawn"=>(StoryOp.Respawn,1,1),"faceplayer"=>(StoryOp.FacePlayer,0,0),"textcolor"=>(StoryOp.TextColor,1,1),"playbgm"=>(StoryOp.Music,2,2),"savebgm"=>(StoryOp.Music,1,1),"fadedefaultbgm"=>(StoryOp.Music,0,0),
            "dofieldeffect" or "incrementgamestat"=>(StoryOp.Special,1,1),"specialvar"=>(StoryOp.Special,2,2),"waitfieldeffect"=>(StoryOp.Sound,1,1),
            "playse" or "playfanfare"=>(StoryOp.Sound,1,1),"waitfanfare" or "waitse"=>(StoryOp.Sound,0,0),
            "playmoncry"=>(StoryOp.Sound,2,2),"waitmoncry"=>(StoryOp.Sound,0,0),"fadeoutbgm"=>(StoryOp.Music,1,1),
            "opendoor" or "closedoor"=>(StoryOp.Door,2,2),"waitdooranim"=>(StoryOp.Door,0,0),"famechecker"=>(StoryOp.FameChecker,2,3),
            "showmonpic"=>(StoryOp.PokemonPicture,3,3),"hidemonpic"=>(StoryOp.PokemonPicture,0,0),"givemon"=>(StoryOp.GivePokemon,2,6),"giveegg"=>(StoryOp.GivePokemon,1,1),"bufferspeciesname"=>(StoryOp.BufferSpecies,2,2),"fadescreen"=>(StoryOp.ScreenFade,1,1),"special"=>(StoryOp.Special,1,1),
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
