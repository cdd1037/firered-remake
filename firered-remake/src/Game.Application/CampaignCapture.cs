using System.Collections.Immutable;
using Game.Domain;
using Game.Domain.Items;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;
using System.Text.Json.Serialization;
namespace Game.Application;
public enum CampaignCaptureStage { DexNotice,DexEntry,NicknameChoice,Naming,SentToPc }
public enum CampaignReplacementStage { UseNext,Select }
public sealed record CampaignWildOrigin(string MetLocation,int MetLevel,string DefaultName);
public sealed record CampaignPendingCapture(PokemonInstance Pokemon,string HeldItem,uint Status,CampaignWildOrigin Origin,bool FirstCaught,CampaignCaptureStage Stage)
{
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public PokemonStorageAddress? Address{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public string? MessageSymbol{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingDefault)] public int MessagePage{get;init;}
}
public sealed record CampaignCapturedPokemon(long InstanceId,string BattleId,int SourceSpeciesId,uint Personality,SixStats Ivs,int MetLevel,string MetLocation);

public sealed partial class CampaignSession
{
    private bool HasWildReserves(CampaignState state,CampaignWildBattle battle)=>_content.CaptureEnabled&&state.Party.Where((p,i)=>i!=battle.ActivePartyIndex).Any(p=>!p.IsEgg&&p.Pokemon.CurrentHp>0);
    private CampaignState PenalizeWildFaint(CampaignState state)
    {
        var b=state.WildBattle!;
        if(!_content.CaptureEnabled||b.State.Player.Pokemon.CurrentHp!=0)return state;
        var owned=state.Party[b.ActivePartyIndex];var fainted=b.FaintedMembers??ImmutableSortedSet<long>.Empty;
        if(fainted.Contains(owned.InstanceId))return state;
        var mon=owned.Pokemon with{Friendship=Math.Max(0,owned.Pokemon.Friendship-1)};
        return state with{Party=state.Party.SetItem(b.ActivePartyIndex,owned with{Pokemon=mon}),WildBattle=b with{FaintedMembers=fainted.Add(owned.InstanceId),State=b.State with{Player=b.State.Player with{Pokemon=mon}}}};
    }
    private CampaignState BeginCapturePresentation(CampaignState state)
    {
        var b=state.WildBattle!;
        if(!_content.CaptureEnabled||b.Origin is null||state.Exploration!.Captured is null||CaptureCapacityError(state) is not null||b.Capture is not null)throw new InvalidDataException("capture_delivery_context_invalid");
        var opponent=b.State.Opponent;bool first=!state.CaughtSpecies.Contains(opponent.Pokemon.SourceSpeciesId);
        var pending=new CampaignPendingCapture(opponent.Pokemon,OwnedBattleHeldItem(opponent),opponent.Status,b.Origin,first,first?CampaignCaptureStage.DexNotice:CampaignCaptureStage.NicknameChoice);
        return state with{CaughtSpecies=state.CaughtSpecies.Add(opponent.Pokemon.SourceSpeciesId),WildBattle=b with{Capture=pending,Phase=CampaignBattlePhase.Results,Pages=[],PageIndex=0}};
    }
    private Decision<CampaignState> DecideCapture(CampaignState state,CampaignCommand command)
    {
        var b=state.WildBattle!;var capture=b.Capture!;
        if(command.Kind=="ack"&&capture.Stage==CampaignCaptureStage.SentToPc)
        {
            var pages=_content.TextPages(capture.MessageSymbol!);
            return new(CommandStatus.Accepted,capture.MessagePage+1<pages.Length
                ?state with{WildBattle=b with{Capture=capture with{MessagePage=capture.MessagePage+1}}}
                :ReturnFromWildBattle(state with{WildBattle=b with{Capture=null}}));
        }
        if(command.Kind=="ack"&&capture.Stage is CampaignCaptureStage.DexNotice or CampaignCaptureStage.DexEntry)
            return new(CommandStatus.Accepted,state with{WildBattle=b with{Capture=capture with{Stage=capture.Stage==CampaignCaptureStage.DexNotice?CampaignCaptureStage.DexEntry:CampaignCaptureStage.NicknameChoice}}});
        if(command.Kind=="capture_choice"&&capture.Stage==CampaignCaptureStage.NicknameChoice)
        {
            if(command.Choice==0)return new(CommandStatus.Accepted,DeliverCapturedPokemon(state,capture.Origin.DefaultName));
            if(command.Choice==1)return new(CommandStatus.Accepted,state with{WildBattle=b with{Capture=capture with{Stage=CampaignCaptureStage.Naming}}});
            return new(CommandStatus.Rejected,state,"capture_choice_required");
        }
        if(command.Kind=="nickname"&&capture.Stage==CampaignCaptureStage.Naming)
        {
            if(command.Text is null)return new(CommandStatus.Rejected,state,"nickname_text_required");
            var alphabet=_content.PokemonContent!.NicknameCharacters;var error=PokemonNicknameRules.Validate(command.Text,alphabet);
            if(error is not null)return new(CommandStatus.Rejected,state,error);
            return new(CommandStatus.Accepted,DeliverCapturedPokemon(state,PokemonNicknameRules.Apply(capture.Origin.DefaultName,command.Text,alphabet)));
        }
        return new(CommandStatus.Rejected,state,"capture_presentation_pending");
    }
    private CampaignState DeliverCapturedPokemon(CampaignState state,string nickname)
    {
        var b=state.WildBattle!;var c=b.Capture!;var captures=state.Exploration!.Captured!.Value;
        if(CaptureCapacityError(state) is not null||state.NextPokemonId is <2 or long.MaxValue||captures.Any(x=>x.BattleId==b.Id)||state.Exploration.Battles.Any(x=>x.Id==b.Id))throw new InvalidDataException("capture_delivery_already_committed_or_full");
        var pokemon=c.Pokemon with{OriginalTrainerId=state.TrainerId};
        var owned=new PartyPokemon(state.NextPokemonId,pokemon,nickname,state.PlayerName,state.PlayerGender,2,c.Origin.MetLocation,c.Origin.MetLevel,4,"ITEM_POKE_BALL",c.HeldItem){Status=c.Status};
        var error=PokemonStateValidator.ValidateParty(_content.PokemonContent,owned);if(error is not null)throw new InvalidDataException(error);
        var record=new CampaignCapturedPokemon(owned.InstanceId,b.Id,pokemon.SourceSpeciesId,pokemon.Personality,pokemon.Ivs,c.Origin.MetLevel,c.Origin.MetLocation);
        var history=new CampaignWildHistory(b.Id,pokemon.SourceSpeciesId,pokemon.Level,WildBattleOutcome.Caught,0);
        var received=_content.PokemonStorageEnabled?PokemonStorageRules.Receive(_content.PokemonContent,state.Party,state.Storage!,owned):null;
        if(received is {Succeeded:false})throw new InvalidDataException(received.Diagnostic!.Code);
        var next=state with{Party=received?.Party??state.Party.Add(owned),Storage=received?.Storage??state.Storage,NextPokemonId=checked(state.NextPokemonId+1),Exploration=state.Exploration with{Captured=captures.Add(record),Battles=state.Exploration.Battles.Add(history)},WildBattle=b with{Capture=null}};
        if(received?.Outcome==PokemonStorageOutcome.ReceivedInBox)
        {
            int oldBox=state.Script.Variables.GetValueOrDefault("VAR_PC_BOX_TO_SEND_MON"),box=received.Address!.BoxIndex;
            var flags=state.Script.Flags;
            if(box!=oldBox)flags=flags.SetItem("FLAG_SHOWN_BOX_WAS_FULL_MESSAGE",false);
            bool wasFull=state.Storage!.CurrentBox!=box&&!flags.GetValueOrDefault("FLAG_SHOWN_BOX_WAS_FULL_MESSAGE");
            if(wasFull)flags=flags.SetItem("FLAG_SHOWN_BOX_WAS_FULL_MESSAGE",true);
            bool bill=flags.GetValueOrDefault("FLAG_SYS_NOT_SOMEONES_PC");
            string symbol=wasFull?(bill?"Text_MonSentToBoxBillsBoxFull":"Text_MonSentToBoxSomeonesBoxFull")
                :(bill?"Text_MonSentToBoxInBillsPC":"Text_MonSentToBoxInSomeonesPC");
            return next with
            {
                Script=next.Script with{Flags=flags,Variables=next.Script.Variables.SetItem("VAR_PC_BOX_TO_SEND_MON",box)},
                TextBuffers=next.TextBuffers.SetItem("STR_VAR_1","BOX"+(box+1)).SetItem("STR_VAR_2",nickname).SetItem("STR_VAR_3","BOX"+(oldBox+1)),
                WildBattle=b with{Capture=c with{Stage=CampaignCaptureStage.SentToPc,Address=received.Address,MessageSymbol=symbol}}
            };
        }
        return ReturnFromWildBattle(next);
    }
    private Decision<CampaignState> DecideWildReplacement(CampaignState state,CampaignCommand command)
    {
        var b=state.WildBattle!;
        if(command.Kind=="battle_next"&&b.Replacement==CampaignReplacementStage.UseNext)
        {
            if(command.Choice==1)return new(CommandStatus.Accepted,state with{WildBattle=b with{Replacement=CampaignReplacementStage.Select}});
            if(command.Choice!=0)return new(CommandStatus.Rejected,state,"replacement_choice_required");
            var run=WildBattleRules.TryRunAfterFaint(BattlePokemon,b.State);
            if(!run.Succeeded)return new(CommandStatus.Fault,state,run.Fault);
            return new(CommandStatus.Accepted,PrepareWildPresentation(state with{WildBattle=b with{State=run.State!,Replacement=run.State!.AwaitingReplacement?CampaignReplacementStage.Select:null,Events=run.Events,EventIndex=0,Phase=CampaignBattlePhase.Events,RandomTrace=run.Trace},World=state.World with{Rng=run.State!.Rng}}));
        }
        if(command.Kind=="battle_switch"&&b.Replacement==CampaignReplacementStage.Select)
        {
            int index=command.PartyIndex;
            if(index<0||index>=state.Party.Length||index==b.ActivePartyIndex||state.Party[index].IsEgg||state.Party[index].Pokemon.CurrentHp==0)return new(CommandStatus.Rejected,state,"replacement_member_invalid");
            var owned=state.Party[index];bool reserves=state.Party.Where((p,i)=>i!=index).Any(p=>!p.IsEgg&&p.Pokemon.CurrentHp>0);
            var change=WildBattleRules.ReplaceFaintedPlayer(BattlePokemon,b.State,OwnedBattleMember(state,b,owned),_content.ForestEnabled&&reserves);
            if(!change.Succeeded)return new(CommandStatus.Fault,state,change.Fault);
            if(_content.PartyTrainingEnabled)b=b with{ParticipatingMembers=(b.ParticipatingMembers??throw new InvalidDataException("wild_participation_missing")).Add(owned.InstanceId),OpponentHpAtLastSwitch=b.State.Opponent.Pokemon.CurrentHp};
            return new(CommandStatus.Accepted,PrepareWildPresentation(PenalizeWildFaint(state with{WildBattle=b with{State=change.State!,ActivePartyIndex=index,OwnedBefore=owned,Replacement=null,Events=change.Events,EventIndex=0,Phase=CampaignBattlePhase.Events,RandomTrace=change.Trace,DisplayPlayer=owned.Pokemon},Party=state.Party.SetItem(index,owned with{Pokemon=change.State!.Player.Pokemon,Status=change.State.Player.Status,HeldItem=OwnedBattleHeldItem(change.State.Player)}),World=state.World with{Rng=change.State!.Rng}})));
        }
        return new(CommandStatus.Rejected,state,"replacement_selection_required");
    }
    private string? ValidateCapturedParty(CampaignState state)
    {
        if(!_content.CaptureEnabled)return state.Exploration?.Captured is null?null:"save_capture_profile_missing";
        var allOwned=OwnedPokemon(state);int gifts=_content.MagikarpEnabled&&state.MagikarpGift is not null?1:0;
        if(state.Exploration?.Captured is not {} captures||captures.IsDefault||captures.Length>(_content.PokemonStorageEnabled?PokemonStorageState.Capacity+5:5)||state.NextPokemonId!=(allOwned.IsEmpty?1:captures.Length+gifts+2L)||allOwned.Length!=(allOwned.IsEmpty?0:captures.Length+gifts+1))return "save_capture_party_count";
        if(captures.Length>0&&!state.Script.Flags.GetValueOrDefault("FLAG_SYS_POKEDEX_GET"))return "save_capture_before_pokedex";
        var ids=new HashSet<long>();var battles=new HashSet<string>(StringComparer.Ordinal);
        foreach(var capture in captures)
        {
            if(capture is null||capture.InstanceId<2||!ids.Add(capture.InstanceId)||!battles.Add(capture.BattleId)||!WildOriginSupported(capture.SourceSpeciesId,capture.MetLevel,capture.MetLocation))return "save_capture_provenance_invalid";
            var owned=allOwned.SingleOrDefault(p=>p.InstanceId==capture.InstanceId);
            if(owned is null||!EvolutionSpeciesMatchesOrigin(state,owned,capture.SourceSpeciesId)||owned.Pokemon.Personality!=capture.Personality||owned.Pokemon.Ivs!=capture.Ivs||owned.MetLevel!=capture.MetLevel||owned.MetLocation!=capture.MetLocation||owned.Ball!="ITEM_POKE_BALL")return "save_capture_instance_mismatch";
            if(!state.Exploration.Battles.Any(b=>b.Id==capture.BattleId&&b.SourceSpeciesId==capture.SourceSpeciesId&&b.Level==capture.MetLevel&&b.Outcome==WildBattleOutcome.Caught&&b.ExperienceGained==0))return "save_capture_history_missing";
        }
        if(state.Exploration.Battles.Any(b=>b.Outcome==WildBattleOutcome.Caught&&!battles.Contains(b.Id)))return "save_capture_delivery_missing";
        return null;
    }

    public string? VisibleCaptureMessage
    {
        get
        {
            if(State.WildBattle?.Capture is not {Stage:CampaignCaptureStage.SentToPc,MessageSymbol:{} symbol} capture)return null;
            var text=_content.TextPages(symbol)[capture.MessagePage];
            foreach(var buffer in State.TextBuffers)text=text.Replace("{"+buffer.Key+"}",buffer.Value,StringComparison.Ordinal);
            return text;
        }
    }
}
