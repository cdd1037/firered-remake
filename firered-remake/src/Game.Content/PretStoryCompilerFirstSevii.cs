using System.Collections.Immutable;
using Game.Domain.Events;

namespace Game.Content;

public static partial class PretStoryCompiler
{
    private static bool FirstSeviiSource(PretSourceScriptFile file,string path,string hash)=>
        file.SourceRef.Kind==SourceKind.Pret&&file.SourceRef.Repository=="pret/pokefirered"&&
        file.SourceRef.Commit==PretWorldMap.SourceCommit&&file.Path==path&&file.SourceRef.Sha256==hash;

    private static (StoryOp,int,int)? FirstSeviiInstructionShape(PretSourceScriptFile file,string label,string opcode,
        ImmutableArray<string> operands)
    {
        if(FirstSeviiSource(file,"data/maps/ThreeIsland_BerryForest/scripts.inc","e716d5252078b670652939fe883fc8b6db45af0273e4183c82ee3939985a9342"))
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
        if(FirstSeviiSource(file,"data/scripts/trainers.inc","d145a6d45e4143e3b215fdff82de07b78fd361aa695855e4065c043ffba36945")&&
            label is "ThreeIsland_BondBridge_EventScript_Joy" or "ThreeIsland_BondBridge_EventScript_Meg"&&opcode=="trainerbattle_double")
        {
            if(operands.Length!=4||operands[0]!="TRAINER_TWINS_JOY_MEG")throw new InvalidDataException("first_sevii_double_operands");
            return(StoryOp.TrainerBattle,4,4);
        }
        return null;
    }

    private static bool IsFirstSeviiAssemblyData(PretSourceScriptFile file,string label,string[] body)
    {
        var mapHeader=(file.Path,file.SourceRef.Sha256,label) is
            ("data/maps/TwoIsland_Harbor/scripts.inc","db25781ed1af7789a32826b5402d8c197de26dcb19dbf8e8552274ad195c23ac","TwoIsland_Harbor_MapScripts") or
            ("data/maps/ThreeIsland_Harbor/scripts.inc","02e41f69d4f9f17320102f596a472402314b470b69a08390dad9b1f9133b4c4e","ThreeIsland_Harbor_MapScripts") or
            ("data/maps/ThreeIsland_Mart/scripts.inc","3d5ad7fc15eb6e3f879e875d36a7d461428f8f98811ad62322294d0bea38893d","ThreeIsland_Mart_MapScripts");
        if(file.SourceRef.Kind!=SourceKind.Pret||file.SourceRef.Repository!="pret/pokefirered"||file.SourceRef.Commit!=PretWorldMap.SourceCommit)return false;
        if(mapHeader)return body.SequenceEqual(new[]{".byte 0"});
        if(FirstSeviiSource(file,"data/maps/TwoIsland/scripts.inc","e2fddf4e3e169d9ca8107fe93daeb3b48a82d6e40bdef10d54de50e1dc2558c4"))
        {
            if(label=="TwoIsland_Items_ShopInitial")return body.SequenceEqual(new[]{
                ".2byte ITEM_GREAT_BALL",".2byte ITEM_FRESH_WATER",".2byte ITEM_NONE","release","end"});
            if(label=="TwoIsland_Items_ShopExpanded1")return body.SequenceEqual(new[]{
                ".2byte ITEM_ULTRA_BALL",".2byte ITEM_GREAT_BALL",".2byte ITEM_SODA_POP",".2byte ITEM_FRESH_WATER",".2byte ITEM_NONE","release","end"});
        }
        return FirstSeviiSource(file,"data/maps/ThreeIsland_Mart/scripts.inc","3d5ad7fc15eb6e3f879e875d36a7d461428f8f98811ad62322294d0bea38893d")&&
            label=="ThreeIsland_Mart_Items"&&body.SequenceEqual(new[]{".2byte ITEM_ULTRA_BALL",".2byte ITEM_HYPER_POTION",
                ".2byte ITEM_REVIVE",".2byte ITEM_FULL_HEAL",".2byte ITEM_ESCAPE_ROPE",".2byte ITEM_MAX_REPEL",".2byte ITEM_NONE","release","end"});
    }
}
