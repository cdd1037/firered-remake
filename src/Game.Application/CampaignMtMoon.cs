using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Items;
namespace Game.Application;

public sealed partial class CampaignSession
{
    private bool IsMtMoonButton(CampaignState state)=>state.World.Player.MapId.Value=="map.mt-moon-b2f"&&state.Script.Wait?.Kind==StoryWaitKind.ButtonPress&&!state.Script.Stack.IsEmpty&&state.Script.Stack[^1].ProgramId=="MtMoon_B2F_EventScript_BattleMiguel";
    private Decision<CampaignState> DecideMtMoonButton(CampaignState state,CampaignCommand command)
    {
        if(!IsMtMoonButton(state)||command.Text is not("A" or "B"))return new(CommandStatus.Rejected,state,"mtmoon_button_invalid");
        var next=state with{PresentationEffects=[],PresentationTicks=0,Motions=[]};
        return new(CommandStatus.Accepted,Pump(Advance(next,StoryRules.Continue(next.Script,next.Script.Wait!.CorrelationId,_content))));
    }
    private bool TryApplyMtMoonEffect(CampaignState state,StoryEffect effect,out CampaignState next)
    {
        next=state;        var a=effect.Args;
        if(effect.Opcode=="waitbuttonpress"&&effect.Source.Path=="data/maps/MtMoon_B2F/scripts.inc")
        {
            if(!IsMtMoonButton(state)||state.Dialogue?.TextSymbol!="MtMoon_B2F_Text_MiguelIntro")throw new InvalidDataException("mtmoon_button_context_invalid");
            return true;
        }
        if(effect.Opcode is "additem" or "checkitemtype" or "bufferitemname" or "bufferitemnameplural")
        {
            int Value(string token)=>token.StartsWith("VAR_",StringComparison.Ordinal)?state.Script.Variables.GetValueOrDefault(token):int.TryParse(token,out int n)?n:_content.TryConstant(token,out n)?n:throw new InvalidDataException("mtmoon_item_value_missing");
            bool buffer=effect.Opcode.StartsWith("bufferitem",StringComparison.Ordinal);
            int number=Value(a[buffer?1:0]);
            var fossil=ShopCatalog.Items.FirstOrDefault(i=>i.SourceItemId==number&&i.UseSupport==EarlyShopUseSupport.Fossil);
            if(fossil is not null)
            {
                if(state.World.Player.MapId.Value!="map.mt-moon-b2f"||effect.Source.Path is not("data/maps/MtMoon_B2F/scripts.inc" or "data/scripts/obtain_item.inc" or "data/event_scripts.s"))throw new InvalidDataException("mtmoon_fossil_source_invalid");
                if(buffer){next=state with{TextBuffers=state.TextBuffers.SetItem(a[0],fossil.Name)};return true;}
                if(effect.Opcode=="checkitemtype"){next=state with{Script=state.Script with{Variables=state.Script.Variables.SetItem("VAR_RESULT",Value("POCKET_KEY_ITEMS"))}};return true;}
                if(effect.Source.Path!="data/maps/MtMoon_B2F/scripts.inc"||a.Length!=2||Value(a[1])!=1||!state.BattledTrainers.Contains("pret:TRAINER_SUPER_NERD_MIGUEL")||state.Script.Variables.GetValueOrDefault("VAR_MAP_SCENE_MT_MOON_B2F")!=1||state.Script.Flags.GetValueOrDefault("FLAG_GOT_FOSSIL_FROM_MT_MOON")||state.Inventory is not {} inventory)
                    throw new InvalidDataException("mtmoon_fossil_reward_context_invalid");
                var keys=inventory.KeyItems??[];
                if(keys.Any(k=>k.Item==MtMoonItemRules.HelixFossilId||k.Item==MtMoonItemRules.DomeFossilId)||keys.Length>=ShopCatalog.QuestLimits.KeyItemSlots)throw new InvalidDataException("mtmoon_fossil_capacity_or_duplicate");
                var check=EarlyInventoryAddRules.Add(ShopCatalog,inventory,fossil.Id,1);
                if(!check.Succeeded)throw new InvalidDataException(check.Error);
                next=state with{Inventory=inventory with{KeyItems=keys.Add(new(fossil.Id,1))},Script=state.Script with{Variables=state.Script.Variables.SetItem("VAR_RESULT",1)}};return true;
            }
        }

        if(effect.Opcode=="goto_if_defeated")
        {
            if(effect.Source.Path!="data/maps/MtMoon_B2F/scripts.inc"||effect.Source.Symbol!="MtMoon_B2F_EventScript_Miguel"||a.Length!=2||a[0]!="TRAINER_SUPER_NERD_MIGUEL"||a[1]!="MtMoon_B2F_EventScript_MiguelGoPickFossil")throw new InvalidDataException("mtmoon_defeated_branch_source_invalid");
            // The source macro uses ScriptContext.comparisonResult, never VAR_RESULT.
            if(state.BattledTrainers.Contains("pret:"+a[0]))next=state with{Script=state.Script with{Stack=state.Script.Stack.SetItem(state.Script.Stack.Length-1,new(a[1],0))}};
            return true;
        }
        if(effect.Opcode=="copyobjectxytoperm")
        {
            if(effect.Source.Path!="data/maps/MtMoon_B2F/scripts.inc"||a.Length!=1||a[0]!="LOCALID_MIGUEL"||!state.Actors.TryGetValue(a[0],out var actor)||!actor.Active)throw new InvalidDataException("mtmoon_copy_actor_context_invalid");
            next=state with{Actors=state.Actors.SetItem(actor.LocalId,actor with{TemplateX=actor.X,TemplateY=actor.Y})};return true;
        }
        if(effect.Opcode=="special"&&a.Length==1&&a[0]=="QuestLog_CutRecording"&&effect.Source.Path=="data/maps/MtMoon_B2F/scripts.inc")
        {
            if(state.Script.QuestLogState!=0)throw new InvalidDataException("mtmoon_quest_log_unsupported");
            return true;
        }
        return false;
    }
    private string? ValidateMtMoonProgress(CampaignState state)
    {
        bool won=state.BattledTrainers.Contains("pret:TRAINER_SUPER_NERD_MIGUEL");
        int scene=state.Script.Variables.GetValueOrDefault("VAR_MAP_SCENE_MT_MOON_B2F");
        if(scene is <0 or >1||won!=(scene==1))return "save_mtmoon_miguel_scene_invalid";
        bool dome=state.Script.Flags.GetValueOrDefault("FLAG_GOT_DOME_FOSSIL"),helix=state.Script.Flags.GetValueOrDefault("FLAG_GOT_HELIX_FOSSIL"),got=state.Script.Flags.GetValueOrDefault("FLAG_GOT_FOSSIL_FROM_MT_MOON");
        var keys=state.Inventory?.KeyItems??[];
        if(dome&&helix||got!=(dome||helix)||got&&!won||
            OpeningInventoryRules.Quantity(keys,MtMoonItemRules.DomeFossilId)+DepositedOrRevivedFossils(state,2)!=(dome?1:0)||
            OpeningInventoryRules.Quantity(keys,MtMoonItemRules.HelixFossilId)+DepositedOrRevivedFossils(state,1)!=(helix?1:0))return "save_mtmoon_fossil_reward_invalid";
        if(got&&(!state.Script.Flags.GetValueOrDefault("FLAG_HIDE_DOME_FOSSIL")||!state.Script.Flags.GetValueOrDefault("FLAG_HIDE_HELIX_FOSSIL")))return "save_mtmoon_fossil_hide_invalid";
        if(state.World.Player.MapId.Value=="map.mt-moon-b2f")
        {
            foreach(var id in new[]{"LOCALID_DOME_FOSSIL","LOCALID_HELIX_FOSSIL"})
                if(!state.Actors.TryGetValue(id,out var actor)||got&&actor.Active)return "save_mtmoon_fossil_actor_invalid";
        }
        return null;
    }

}
