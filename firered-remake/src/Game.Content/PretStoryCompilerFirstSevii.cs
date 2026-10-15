using System.Collections.Immutable;
using Game.Domain.Events;

namespace Game.Content;

public static partial class PretStoryCompiler
{
    private static bool FirstSeviiSource(PretSourceScriptFile file,string path)=>
        file.SourceRef.Kind==SourceKind.Pret&&file.Path==path;

    private static (StoryOp,int,int)? FirstSeviiInstructionShape(PretSourceScriptFile file,string label,string opcode,ImmutableArray<string> operands)
    {
        if(FirstSeviiSource(file,"data/maps/ThreeIsland_BerryForest/scripts.inc"))
        {
            if(label=="ThreeIsland_BerryForest_OnTransition"&&opcode=="setescapewarp")
            {
                if(!operands.SequenceEqual(new[]{"MAP_THREE_ISLAND_BOND_BRIDGE","12","6"}))throw new InvalidDataException("first_sevii_escape_operands");
                return(StoryOp.Special,3,3);
            }
            if(label=="ThreeIsland_BerryForest_EventScript_Lostelle")
            {
                if(opcode=="setwildbattle")
                {
                    if(!operands.SequenceEqual(new[]{"SPECIES_HYPNO","30"}))throw new InvalidDataException("first_sevii_hypno_operands");
                    return(StoryOp.Special,2,2);
                }
                if(opcode=="dowildbattle")return(StoryOp.TrainerBattle,0,0);
            }
        }
        if(FirstSeviiSource(file,"data/scripts/trainers.inc")&&
            (label,opcode) is
                ("ThreeIsland_BondBridge_EventScript_Joy","trainerbattle_double") or
                ("ThreeIsland_BondBridge_EventScript_Meg","trainerbattle_double") or
                ("ThreeIsland_BondBridge_EventScript_JoyRematch","trainerbattle_rematch_double") or
                ("ThreeIsland_BondBridge_EventScript_MegRematch","trainerbattle_rematch_double"))
        {
            if(operands.Length!=4||operands[0]!="TRAINER_TWINS_JOY_MEG")throw new InvalidDataException("first_sevii_double_operands");
            return(StoryOp.TrainerBattle,4,4);
        }
        return null;
    }

    private static bool IsFirstSeviiAssemblyData(PretSourceScriptFile file,string label,string[] body)
    {
        var mapHeader=(file.Path,label) is
            ("data/maps/TwoIsland_House/scripts.inc","TwoIsland_House_MapScripts") or
            ("data/maps/TwoIsland_Harbor/scripts.inc","TwoIsland_Harbor_MapScripts") or
            ("data/maps/ThreeIsland_Harbor/scripts.inc","ThreeIsland_Harbor_MapScripts") or
            ("data/maps/ThreeIsland_Mart/scripts.inc","ThreeIsland_Mart_MapScripts");
        if(file.SourceRef.Kind!=SourceKind.Pret)return false;
        if(mapHeader)return body.SequenceEqual(new[]{".byte 0"});
        if(FirstSeviiSource(file,"data/maps/TwoIsland/scripts.inc"))
        {
            if(label=="TwoIsland_Items_ShopInitial")return body.SequenceEqual(new[]{
                ".2byte ITEM_GREAT_BALL",".2byte ITEM_FRESH_WATER",".2byte ITEM_NONE","release","end"});
            if(label=="TwoIsland_Items_ShopExpanded1")return body.SequenceEqual(new[]{
                ".2byte ITEM_ULTRA_BALL",".2byte ITEM_GREAT_BALL",".2byte ITEM_SODA_POP",".2byte ITEM_FRESH_WATER",".2byte ITEM_NONE","release","end"});
        }
        return FirstSeviiSource(file,"data/maps/ThreeIsland_Mart/scripts.inc")&&
            label=="ThreeIsland_Mart_Items"&&body.SequenceEqual(new[]{".2byte ITEM_ULTRA_BALL",".2byte ITEM_HYPER_POTION",".2byte ITEM_REVIVE",".2byte ITEM_FULL_HEAL",".2byte ITEM_ESCAPE_ROPE",".2byte ITEM_MAX_REPEL",".2byte ITEM_NONE","release","end"});
    }
}
