using System.Globalization;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Items;
namespace Game.Application;

public sealed partial class CampaignSession
{
    public const int MaximumBerryPowder=99999;
    private static readonly ItemId PowderJarId=new("pret:ITEM_POWDER_JAR");
    private const string PowderJarFlag="FLAG_GOT_POWDER_JAR";
    private bool InPowderManScript(CampaignState state)=>state.Script.Stack.Any(f=>f.ProgramId.StartsWith("CeruleanCity_House5_EventScript_",StringComparison.Ordinal));
    private bool TryApplyBerryPowderEffect(CampaignState state,StoryEffect effect,out CampaignState next)
    {
        next=state;
        if(effect.Source.Path!="data/maps/CeruleanCity_House5/scripts.inc"&&!InPowderManScript(state))return false;
        var args=effect.Args;
        int Value(string token)=>token.StartsWith("VAR_",StringComparison.Ordinal)?state.Script.Variables.GetValueOrDefault(token):
            int.TryParse(token,out int n)?n:_content.TryConstant(token,out n)?n:throw new InvalidDataException("berry_powder_operand");
        CampaignState Result(int value)=>state with{Script=state.Script with{Variables=state.Script.Variables.SetItem("VAR_RESULT",value)}};
        if(effect.Opcode is "special" or "specialvar")
        {
            string special=args[effect.Opcode=="special"?0:1];
            switch(special)
            {
                case "DisplayBerryPowderVendorMenu":next=state with{BerryPowderVendor=true};return true;
                case "RemoveBerryPowderVendorMenu":next=state with{BerryPowderVendor=false};return true;
                case "PrintPlayerBerryPowderAmount":return true;
                case "Script_HasEnoughBerryPowder":next=Result(state.BerryPowder>=Value("VAR_0x8004")?1:0);return true;
                case "Script_TakeBerryPowder":
                    int cost=Value("VAR_0x8004");next=state.BerryPowder>=cost?state with{BerryPowder=state.BerryPowder-cost}:state;return true;
                default:return false;
            }
        }
        return false;
    }

    private Decision<CampaignState> UsePowderJar(CampaignState state,CampaignCommand command,bool registered=false)
    {
        if((registered?state.RegisteredItem!=PowderJarId.Value||state.ItemMenu is not null||!IsStableFieldForParty(state):
            command.Text!=PowderJarId.Value||state.ItemMenu is not {Origin:"bag",View:"bag",TargetInstanceId:null})||
            OpeningInventoryRules.Quantity(state.Inventory?.KeyItems??[],PowderJarId)!=1)
            return new(CommandStatus.Rejected,state,"powder_jar_use_context");
        var menu=registered?new CampaignItemMenu("registered","message",ReturnView:"field"):state.ItemMenu!;
        return new(CommandStatus.Accepted,state with{TextBuffers=state.TextBuffers.SetItem("STR_VAR_1",state.BerryPowder.ToString(CultureInfo.InvariantCulture)),
            ItemMenu=menu with{View="message",MessageSymbol="gText_PowderQty",ReturnView=registered?"field":"bag"}});
    }

    private string? ValidateBerryPowder(CampaignState state)
    {
        if(state.BerryPowder is <0 or >MaximumBerryPowder)return "save_berry_powder_range";
        if(state.BerryPowderVendor)return "save_berry_powder_vendor_active";
        int jars=OpeningInventoryRules.Quantity(state.Inventory?.KeyItems??[],PowderJarId);
        // Original sets the gift flag BEFORE giveitem, including the Bag-full failure branch.
        if(jars>1||jars!=0&&!state.Script.Flags.GetValueOrDefault(PowderJarFlag))return "save_powder_jar_receipt";
        return null;
    }
}
