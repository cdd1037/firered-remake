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
    private bool IsGiftSpecies(CampaignState state,int species)=>GiftSourceSpecies(state).Contains(species);
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
        bool handles=effect.Opcode is "showmoneybox" or "hidemoneybox" or "updatemoneybox" or "checkmoney" or "removemoney" or "checkplayergender" ||
            effect.Opcode=="givemon"&&a[0]=="SPECIES_MAGIKARP";
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
            case "givemon":next=GiveMagikarp(state,Value(a[1]));break;
        }
        return true;
    }
    private CampaignState GiveMagikarp(CampaignState state,int level)
    {
        if(level!=5||state.MagikarpGift is not null||state.GiftDelivery is not null||state.Script.Flags.GetValueOrDefault("FLAG_BOUGHT_MAGIKARP")||state.NextPokemonId is <2 or long.MaxValue)throw new InvalidDataException("magikarp_gift_context_invalid");
        var result=GiveScriptGift(state,129,5,"MAPSEC_ROUTE_4");
        return result.Gift is {} mon?result.State with{MagikarpGift=new(mon.InstanceId,mon.Pokemon.Personality,mon.Pokemon.Ivs)}:result.State;
    }
}
