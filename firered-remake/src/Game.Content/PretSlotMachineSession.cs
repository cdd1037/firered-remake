using System.Collections.Immutable;
using Game.Domain.Events;
using Game.Domain.Slots;

namespace Game.Content;

public sealed partial class PretCampaignCatalog
{
    // Current production supplies the approved None/0 and circular21 safety policy.
    public SlotMachineSessionContract? GameCornerSlots { get; init; }
}

public static partial class PretStoryCompiler
{
    private static bool IsOriginalSlotScript(PretSourceScriptFile file)=>
        FirstSeviiSource(file,GameCornerCoinSource.Path);

    private static string[] SelectOriginalSlotLines(PretSourceScriptFile file,string[] lines)
    {
        // The authenticated source has one BUGFIX block, and this profile preserves its .else.
        if(!IsOriginalSlotScript(file))return lines;
        var selected=(string[])lines.Clone();bool conditional=false;bool original=false;int count=0;
        for(int i=0;i<selected.Length;i++)
        {
            string line=selected[i].Trim();
            if(line==".ifdef BUGFIX"){if(conditional)throw new InvalidDataException("slot_source_condition");conditional=true;original=false;count++;selected[i]="";}
            else if(conditional&&line==".else"){original=true;selected[i]="";}
            else if(conditional&&line==".endif"){conditional=false;selected[i]="";}
            else if(conditional&&!original)selected[i]="";
        }
        if(conditional||count!=1)throw new InvalidDataException("slot_source_condition");
        return selected;
    }

    private static (StoryOp,int,int)? SlotMachineInstructionShape(PretSourceScriptFile file,string label,string opcode,ImmutableArray<string> operands)
    {
        if(!IsOriginalSlotScript(file)||label!=SlotMachineSource.Common)return null;
        string[]? args=opcode switch
        {
            "specialvar"=>["VAR_RESULT","GetRandomSlotMachineId"],
            "playslotmachine"=>["VAR_RESULT"],
            "dofieldeffect" or "waitfieldeffect"=>["FLDEFF_SMILEY_FACE_ICON"],
            "setfieldeffectargument"=>operands.FirstOrDefault() switch
            {
                "0"=>["0","LOCALID_PLAYER"],
                "1"=>["1","MAP_GROUP(MAP_CELADON_CITY_GAME_CORNER)"],
                "2"=>["2","MAP_NUM(MAP_CELADON_CITY_GAME_CORNER)"],_=>null
            },_=>null
        };
        if(args is null)return null;
        if(!operands.SequenceEqual(args))throw ContentJson.Error("slot_operands",file.Path+":"+label,opcode);
        return(opcode=="waitfieldeffect"?StoryOp.WaitState:StoryOp.Special,args.Length,args.Length);
    }
}
