// RECOVERED FROM VISIBLE TOOL OUTPUT, NOT ORIGINAL FILE RECOVERY.
// Original target: e646c7ae6aac95f0373221e834b92365d3d5c71e.
// No original file hash is available. Fresh recovery checks are documented in docs/recovery-magikarp.md.
using System.Collections.Immutable;
using Game.Domain;
using Game.Domain.Events;
using Game.Domain.Pokemon;

namespace Game.Application;

public sealed record CampaignMoneyBox(int Amount,int X,int Y);
public sealed record CampaignMagikarpGift(long InstanceId,uint Personality,SixStats Ivs);
public sealed record CampaignGiftDelivery(PokemonStorageAddress? BoxAddress,int PreviousBox);

public sealed partial class CampaignSession
{
    private bool IsGiftSpecies(CampaignState state,int species)=>_content.MagikarpEnabled&&state.MagikarpGift is not null&&species==129;
    private string? ValidateMagikarpGift(CampaignState state)
    {
        bool bought=state.Script.Flags.GetValueOrDefault("FLAG_BOUGHT_MAGIKARP");
        if(!_content.MagikarpEnabled)return state.MagikarpGift is not null||bought?"save_magikarp_profile_missing":null;
        if((state.MagikarpGift is not null)!=bought)return "save_magikarp_flag_inconsistent";
        if(state.MagikarpGift is not {} gift)return null;
        if(gift.InstanceId<2||gift.InstanceId>=state.NextPokemonId||gift.Ivs is null)return "save_magikarp_origin_invalid";
        var mon=OwnedPokemon(state).SingleOrDefault(p=>p.InstanceId==gift.InstanceId);
        if(mon is null||!EvolutionSpeciesMatchesOrigin(state,mon,129)||mon.Pokemon.Personality!=gift.Personality||mon.Pokemon.Ivs!=gift.Ivs||mon.MetLocation!="MAPSEC_ROUTE_4"||mon.MetLevel!=5||mon.Ball!="ITEM_POKE_BALL"||mon.HeldItem!="ITEM_NONE"||!state.CaughtSpecies.Contains(129)||!state.SeenSpecies.Contains(129)||state.Exploration?.Captured is not {} captures||captures.IsDefault||captures.Any(c=>c is null||c.InstanceId==gift.InstanceId))return "save_magikarp_instance_mismatch";
        return null;
    }
    private bool InMagikarpScript(CampaignState state)=>state.World.Player.MapId.Value=="map.route-4-pokemon-center-1f"&&
        state.LastTalked is {} actor&&state.Actors.TryGetValue(actor,out var value)&&value.ScriptId=="Route4_PokemonCenter_1F_EventScript_MagikarpSalesman";
    private bool TryApplyMagikarpEffect(CampaignState state,StoryEffect effect,out CampaignState next)
    {
        next=state;if(!_content.MagikarpEnabled)return false;
        var a=effect.Args;
        bool handles=effect.Opcode is "showmoneybox" or "hidemoneybox" or "updatemoneybox" or "checkmoney" or "removemoney" or "checkplayergender" or "getpartysize" or "bufferboxname" or "subvar" ||
            effect.Opcode=="givemon"&&a[0]=="SPECIES_MAGIKARP" ||
            effect.Opcode=="special"&&a[0]=="ChangeBoxPokemonNickname" ||
            effect.Opcode=="specialvar"&&a[1] is "ShouldShowBoxWasFullMessage" or "GetPCBoxToSendMon";
        if(!handles)return false;
        if(!InMagikarpScript(state))throw new InvalidDataException("magikarp_effect_context_invalid");
        int Value(string token)=>int.TryParse(token,out int n)?n:state.Script.Variables.TryGetValue(token,out int v)?v:_content.TryConstant(token,out int c)?c:throw new InvalidDataException("magikarp_operand_unknown: "+token);
        CampaignState SetResult(int value)=>state with{Script=state.Script with{Variables=state.Script.Variables.SetItem("VAR_RESULT",value)}};
        switch(effect.Opcode)
        {
            case "showmoneybox":
                if(Value(a[0])!=0||Value(a[1])!=0)throw new InvalidDataException("magikarp_moneybox_position");
                next=state with{MoneyBox=new(state.Money,0,0)};break;
            case "hidemoneybox":next=state with{MoneyBox=null};break;
            case "updatemoneybox":next=state with{MoneyBox=state.MoneyBox is {} box?box with{Amount=state.Money}:throw new InvalidDataException("magikarp_moneybox_missing")};break;
            case "checkplayergender":next=SetResult(state.PlayerGender);break;
            case "checkmoney":
                if(Value(a[0])!=500)throw new InvalidDataException("magikarp_price_invalid");
                next=SetResult(state.Money>=500?1:0);break;
            case "removemoney":
                if(Value(a[0])!=500||state.MagikarpGift is null||state.GiftDelivery is null||state.Money<500)throw new InvalidDataException("magikarp_payment_context_invalid");
                next=state with{Money=state.Money-500};break;
            case "getpartysize":next=SetResult(state.Party.Length);break;
            case "subvar":
                if(a[0]!="VAR_RESULT"||Value(a[1])!=1||state.GiftDelivery is not {BoxAddress:null})throw new InvalidDataException("magikarp_party_slot_context");
                next=SetResult((state.Script.Variables.GetValueOrDefault("VAR_RESULT")-1)&65535);break;
            case "bufferboxname":
                int index=Value(a[1]);if(a[0] is not ("STR_VAR_1" or "STR_VAR_3")||index is <0 or >=PokemonStorageState.BoxCount)throw new InvalidDataException("magikarp_box_name_invalid");
                next=state with{TextBuffers=state.TextBuffers.SetItem(a[0],"BOX"+(index+1))};break;
            case "givemon":next=GiveMagikarp(state,Value(a[1]));break;
            case "specialvar":
                if(a[0]!="VAR_RESULT"||state.GiftDelivery is not {BoxAddress:{} address} pending)throw new InvalidDataException("magikarp_pc_delivery_missing");
                if(a[1]=="GetPCBoxToSendMon")next=SetResult(pending.PreviousBox);
                else
                {
                    bool full=state.Storage!.CurrentBox!=address.BoxIndex&&!state.Script.Flags.GetValueOrDefault("FLAG_SHOWN_BOX_WAS_FULL_MESSAGE");
                    next=SetResult(full?1:0);
                    if(full)next=next with{Script=next.Script with{Flags=next.Script.Flags.SetItem("FLAG_SHOWN_BOX_WAS_FULL_MESSAGE",true)}};
                }
                break;
            case "special":
                if(state.GiftDelivery?.BoxAddress is not {} slot||state.Storage!.Boxes[slot.BoxIndex].Slots[slot.SlotIndex] is not {} mon||mon.InstanceId!=state.MagikarpGift?.InstanceId)throw new InvalidDataException("magikarp_box_nickname_context");
                next=state with{Nickname=new(-1,mon.Nickname,false){BoxAddress=slot},TextBuffers=state.TextBuffers.SetItem("STR_VAR_3",mon.Nickname).SetItem("STR_VAR_2",mon.Nickname)};break;
        }
        return true;
    }
    private CampaignState GiveMagikarp(CampaignState state,int level)
    {
        if(level!=5||state.MagikarpGift is not null||state.GiftDelivery is not null||state.Script.Flags.GetValueOrDefault("FLAG_BOUGHT_MAGIKARP")||state.NextPokemonId is <2 or long.MaxValue)throw new InvalidDataException("magikarp_gift_context_invalid");
        var species=RequireSpecies(129);
        // Original ScriptGiveMon constructs first, so even full party+PC consumes four draws.
        var created=PokemonFactory.Create(_content.PokemonContent!.Catalog,new(species.Definition.Id,5,state.TrainerId,IvPolicy.Random,PersonalityPolicy.Random),state.World.Rng);
        var owned=new PartyPokemon(state.NextPokemonId,created.Pokemon,species.Name,state.PlayerName,state.PlayerGender,2,"MAPSEC_ROUTE_4",5,4,"ITEM_POKE_BALL","ITEM_NONE");
        var receive=PokemonStorageRules.Receive(_content.PokemonContent,state.Party,state.Storage!,owned);
        var next=state with{World=state.World with{Rng=created.Rng}};
        if(!receive.Succeeded)
        {
            if(receive.Diagnostic?.Code!="storage_full")throw new InvalidDataException(receive.Diagnostic?.Code);
            return next with{Script=next.Script with{Variables=next.Script.Variables.SetItem("VAR_RESULT",2)}};
        }
        int previous=state.Script.Variables.GetValueOrDefault("VAR_PC_BOX_TO_SEND_MON");
        var variables=next.Script.Variables.SetItem("VAR_RESULT",receive.Address is null?0:1);var flags=next.Script.Flags;
        if(receive.Address is {} address)
        {
            if(previous!=address.BoxIndex)flags=flags.SetItem("FLAG_SHOWN_BOX_WAS_FULL_MESSAGE",false);
            variables=variables.SetItem("VAR_PC_BOX_TO_SEND_MON",address.BoxIndex);
        }
        return next with{Party=receive.Party,Storage=receive.Storage,NextPokemonId=checked(state.NextPokemonId+1),MagikarpGift=new(owned.InstanceId,created.Pokemon.Personality,created.Pokemon.Ivs),GiftDelivery=new(receive.Address,previous),SeenSpecies=next.SeenSpecies.Add(129),CaughtSpecies=next.CaughtSpecies.Add(129),Script=next.Script with{Variables=variables,Flags=flags}};
    }
    private Decision<CampaignState> SubmitBoxGiftNickname(CampaignState state,CampaignNickname naming,StoryWait wait,string name)
    {
        if(!_content.MagikarpEnabled||state.GiftDelivery?.BoxAddress!=naming.BoxAddress||state.Storage is not {} storage)return new(CommandStatus.Rejected,state,"gift_box_nickname_not_pending");
        var address=naming.BoxAddress!;var box=storage.Boxes[address.BoxIndex];var mon=box.Slots[address.SlotIndex];
        if(mon is null||mon.InstanceId!=state.MagikarpGift?.InstanceId)return new(CommandStatus.Rejected,state,"gift_box_nickname_instance_mismatch");
        return new(CommandStatus.Accepted,state with{Storage=storage with{Boxes=storage.Boxes.SetItem(address.BoxIndex,box with{Slots=box.Slots.SetItem(address.SlotIndex,mon with{Nickname=name})})},Nickname=naming with{Submitted=true},TextBuffers=state.TextBuffers.SetItem("STR_VAR_2",name),ScreenIsBlack=false,PresentationTicks=16,Script=state.Script with{Wait=wait with{Kind=StoryWaitKind.Presentation}}});
    }
}
