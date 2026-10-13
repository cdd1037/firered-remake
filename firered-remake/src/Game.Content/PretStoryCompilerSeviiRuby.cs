using System.Collections.Immutable;
using Game.Domain.Events;

namespace Game.Content;

public static partial class PretStoryCompiler
{
    private static readonly ImmutableDictionary<string,string> SeviiRubyMapSourceHashes = new Dictionary<string,string>(StringComparer.Ordinal)
    {
        ["data/maps/MtEmber_Exterior/scripts.inc"]="66a571dd4ccf75e9b83705bee82829fe5de1c31fcb7a8418ba2556812006bd5f",
        ["data/maps/MtEmber_RubyPath_1F/scripts.inc"]="c26c47521baf665f3de98b04dda529d569cd2f1e0bd4056d9b75a1b76f691559",
        ["data/maps/MtEmber_RubyPath_B1F/scripts.inc"]="109c377139f47732cd7c9ba9e65c7d50b1c502f7b6b466c8419852da18cfe92a",
        ["data/maps/MtEmber_RubyPath_B1F_Stairs/scripts.inc"]="e220495e4df8c3ff0cd0fa3f8985737aae11d385cce4164215c659ce6bc6382d",
        ["data/maps/MtEmber_RubyPath_B2F/scripts.inc"]="a4bc8ff4613c0f3c66210f9997193fe0fbe5e10400555916f8bf0d8e75093a7c",
        ["data/maps/MtEmber_RubyPath_B2F_Stairs/scripts.inc"]="877762eb750439c449ae7d6704aa3b2dcf0ec2b99aee4ac05cbe3b83bad475e4",
        ["data/maps/MtEmber_RubyPath_B3F/scripts.inc"]="a51fb10ffc4d03ce837d05b0cc2ce5a539d61f455b50b86d0901ba65a8bcf9b4",
        ["data/maps/MtEmber_RubyPath_B4F/scripts.inc"]="1fe709024c4015736596e78a245746406bae3020179996f592c667cac8b1e78f",
        ["data/maps/MtEmber_RubyPath_B5F/scripts.inc"]="d644c9147744f349171528784d91182f5f1a617fc6d0c535fac771eeec6daefc",
        ["data/maps/OneIsland_KindleRoad/scripts.inc"]="47ac1b834b424d36630ccef07c2d7818545f0864dc6b282bcb84910c18fbe849",
        ["data/maps/OneIsland_KindleRoad_EmberSpa/scripts.inc"]="5d2a1c3146b26a0accc57a7b8574e59e9afc539dd43eab4ff2993e31a6051720",
    }.ToImmutableDictionary(StringComparer.Ordinal);
    private static bool IsSeviiRubyAssemblyData(PretSourceScriptFile file,string label,string[] body) =>
        SeviiRubyMapSourceHashes.TryGetValue(file.Path,out var hash)&&FirstSeviiSource(file,file.Path,hash)&&
        label==file.Path.Split('/')[2]+"_MapScripts"&&body.SequenceEqual(new[]{".byte 0"});

    private static (StoryOp,int,int)? SeviiRubyInstructionShape(PretSourceScriptFile file,string label,string opcode,
        ImmutableArray<string> operands)
    {
        if(SeviiRubyMapSourceHashes.TryGetValue(file.Path,out var hash)&&FirstSeviiSource(file,file.Path,hash)&&
            (file.Path is "data/maps/MtEmber_RubyPath_B4F/scripts.inc" or "data/maps/MtEmber_RubyPath_B5F/scripts.inc")&&
            opcode is "braillemessage" or "getbraillestringwidth")
        {
            if(operands.Length!=1||!operands[0].StartsWith("Braille_Text_",StringComparison.Ordinal))
                throw new InvalidDataException("sevii_ruby_braille_operands");
            return(StoryOp.Special,1,1);
        }
        if(FirstSeviiSource(file,"data/scripts/trainers.inc","d145a6d45e4143e3b215fdff82de07b78fd361aa695855e4065c043ffba36945")&&
            (label,opcode) is
                ("OneIsland_KindleRoad_EventScript_Kia","trainerbattle_double") or
                ("OneIsland_KindleRoad_EventScript_Mik","trainerbattle_double") or
                ("OneIsland_KindleRoad_EventScript_KiaRematch","trainerbattle_rematch_double") or
                ("OneIsland_KindleRoad_EventScript_MikRematch","trainerbattle_rematch_double"))
        {
            if(operands.Length!=4||operands[0]!="TRAINER_CRUSH_KIN_MIK_KIA")
                throw new InvalidDataException("sevii_ruby_double_operands");
            return(StoryOp.TrainerBattle,4,4);
        }
        return null;
    }
}
