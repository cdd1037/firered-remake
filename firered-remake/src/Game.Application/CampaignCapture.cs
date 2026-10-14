using System.Collections.Immutable;
using Game.Domain;
using Game.Domain.Battle;
using Game.Domain.Items;
using Game.Domain.Capture;
using Game.Domain.Encounters;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;
using System.Text.Json.Serialization;
namespace Game.Application;
public enum CampaignCaptureStage { DexNotice,DexEntry,NicknameChoice,Naming,SentToPc }
public enum CampaignReplacementStage { UseNext,Select }
public sealed record CampaignWildOrigin(string MetLocation,int MetLevel,string DefaultName);
public sealed record CampaignPendingCapture(PokemonInstance Pokemon,string HeldItem,uint Status,CampaignWildOrigin Origin,bool FirstCaught,CampaignCaptureStage Stage)
{
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public string? Ball{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public PokemonStorageAddress? Address{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public string? MessageSymbol{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingDefault)] public int MessagePage{get;init;}
}
public sealed record CampaignCapturedPokemon(long InstanceId,string BattleId,int SourceSpeciesId,uint Personality,SixStats Ivs,int MetLevel,string MetLocation)
{
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public RockSmashWildOrigin? RockSmash{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public SourceTableWildOrigin? SourceTable{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public FishingEncounterOrigin? Fishing{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public SeafoamWildOrigin? Seafoam{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public PowerPlantWildOrigin? PowerPlant{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public EventIslandWildOrigin? EventIsland{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public CeruleanCaveWildOrigin? CeruleanCaveOrigin{get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public SevenIslandWildOrigin? SevenIsland { get; init; }
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public CampaignCelioRoamerEncounter? CelioRoamer{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public SixIslandWildOrigin? SixIsland{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public FiveIslandWildOrigin? FiveIsland{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public FourIslandWildOrigin? FourIsland{get;init;}
    // Null keeps every historical Poké Ball receipt byte-for-byte compatible.
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public string? Ball{get;init;}
}

public sealed partial class CampaignSession
{
    private bool AdditionalCaptureBallSupported(string? symbol)=>
        symbol=="ITEM_GREAT_BALL"&&_content.CeladonApproachEnabled||
        (symbol is "ITEM_MASTER_BALL" or "ITEM_ULTRA_BALL")&&_content.SilphEnabled;
    private static string? CaptureBallReceipt(CaptureBall ball)=>ball switch
    {
        CaptureBall.Poke=>null,
        CaptureBall.Great=>"ITEM_GREAT_BALL",
        CaptureBall.Ultra=>"ITEM_ULTRA_BALL",
        CaptureBall.Master=>"ITEM_MASTER_BALL",
        _=>throw new InvalidDataException("capture_ball_unsupported")
    };
    private CampaignState BeginCapturePresentation(CampaignState state)
    {
        var b=state.WildBattle!;
        if(!_content.CaptureEnabled||b.Origin is null||state.Exploration!.Captured is null||CaptureCapacityError(state) is not null||b.Capture is not null)throw new InvalidDataException("capture_delivery_context_invalid");
        var captured=SourceWildBattlePolicy.CapturedMember(b.Frame??throw new InvalidDataException("canonical_wild_frame_required"));var capturedPokemon=captured.Pokemon;bool first=!state.CaughtSpecies.Contains(capturedPokemon.SourceSpeciesId);
        var ball=b.Events.OfType<WildBattleCaptureResultEvent>().Single(e=>e.Outcome==CaptureOutcome.Caught).Ball;
        if(ball!=CaptureBall.Poke&&!AdditionalCaptureBallSupported(CaptureBallReceipt(ball)))throw new InvalidDataException("capture_ball_unsupported");
        var pending=new CampaignPendingCapture(capturedPokemon,WildBattleSourcePartyEffects.PersistentHeldItem(captured),captured.Status,b.Origin,first,first?CampaignCaptureStage.DexNotice:CampaignCaptureStage.NicknameChoice){Ball=CaptureBallReceipt(ball)};
        return state with{UnownPokedexPersonality=first&&capturedPokemon.SourceSpeciesId==201?capturedPokemon.Personality:state.UnownPokedexPersonality,CaughtSpecies=state.CaughtSpecies.Add(capturedPokemon.SourceSpeciesId),WildBattle=b with{Capture=pending,Phase=CampaignBattlePhase.Results,Pages=[],PageIndex=0}};
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
        var owned=new PartyPokemon(state.NextPokemonId,pokemon,nickname,state.PlayerName,state.PlayerGender,2,c.Origin.MetLocation,c.Origin.MetLevel,4,c.Ball??"ITEM_POKE_BALL",c.HeldItem){Status=c.Status};
        if(b.Frame.WildEncounter?.Seafoam is not null)owned=owned with{ModernFatefulEncounter=false};
        if(b.Frame.WildEncounter?.PowerPlant is {} plantOrigin)owned=PowerPlantPokemonFactory.PreserveCaughtMetadata(_content.PowerPlantOwnership!,plantOrigin,owned);
        if(b.Frame.WildEncounter?.EventIsland is {} eventOrigin)owned=EventIslandPokemonFactory.PreserveCaughtMetadata(_content.EventIslandOwnership!,eventOrigin,owned);
        var error=PokemonStateValidator.ValidateParty(_content.PokemonContent,owned);if(error is not null)throw new InvalidDataException(error);
        var record=new CampaignCapturedPokemon(owned.InstanceId,b.Id,pokemon.SourceSpeciesId,pokemon.Personality,pokemon.Ivs,c.Origin.MetLevel,c.Origin.MetLocation){RockSmash=b.Frame.WildEncounter?.RockSmash,SourceTable=b.Frame.WildEncounter?.SourceTable,Seafoam=b.Frame.WildEncounter?.Seafoam,Fishing=b.Frame.WildEncounter?.Fishing,PowerPlant=b.Frame.WildEncounter?.PowerPlant,EventIsland=b.Frame.WildEncounter?.EventIsland,SevenIsland=b.Frame!.WildEncounter?.SevenIsland,CeruleanCaveOrigin=b.Frame!.WildEncounter?.CeruleanCave,CelioRoamer=b.CelioRoamer,Ball=c.Ball,FiveIsland=b.Frame!.WildEncounter?.FiveIsland,SixIsland=b.Frame!.WildEncounter?.SixIsland,FourIsland=b.Frame!.WildEncounter?.FourIsland};
        var history=new CampaignWildHistory(b.Id,pokemon.SourceSpeciesId,pokemon.Level,WildBattleOutcome.Caught,0){RockSmashReceipt=b.RockSmashReceipt,SourceTableReceipt=b.SourceTableReceipt,SeafoamReceipt=b.SeafoamReceipt,Fishing=b.Frame.WildEncounter?.Fishing,PowerPlantReceipt=b.PowerPlantReceipt,EventIslandReceipt=b.EventIslandReceipt,SevenIsland=b.Frame!.WildEncounter?.SevenIsland,CeruleanCaveOrigin=b.Frame!.WildEncounter?.CeruleanCave,SevenIslandReceipt=b.SevenIslandReceipt,MewtwoReceipt=b.MewtwoReceipt,CelioRoamer=b.CelioRoamerSettlement,FiveIsland=b.Frame!.WildEncounter?.FiveIsland,SixIsland=b.Frame!.WildEncounter?.SixIsland,FourIsland=b.Frame!.WildEncounter?.FourIsland,SeviiRuby=b.Frame!.WildEncounter?.SeviiRuby,VictoryRoad=b.Frame!.WildEncounter?.VictoryRoad,LostelleHypno=b.LostelleHypno,ScriptedSnorlaxRoute=b.ScriptedSnorlaxRoute};
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
        var b=state.WildBattle!;var frame=b.Frame??throw new InvalidDataException("canonical_wild_frame_required");
        if(command.Kind=="battle_next"&&b.Replacement==CampaignReplacementStage.UseNext)
        {
            if(command.Choice==1)return new(CommandStatus.Accepted,state with{WildBattle=b with{Replacement=CampaignReplacementStage.Select}});
            if(command.Choice!=0)return new(CommandStatus.Rejected,state,"replacement_choice_required");
            return CommitCanonicalWildResult(state with{WildBattle=b with{Replacement=CampaignReplacementStage.Select}},
                SourceWildBattlePolicy.TryRunAfterFaint(CanonicalWildPokemon,frame,WildContinuation(frame)));
        }
        if(command.Kind=="battle_switch"&&b.Replacement==CampaignReplacementStage.Select)
        {
            int index=command.PartyIndex;
            if(index<0||index>=state.Party.Length||index==b.ActivePartyIndex||state.Party[index].IsEgg||state.Party[index].Pokemon.CurrentHp==0)return new(CommandStatus.Rejected,state,"replacement_member_invalid");
            var result=SourceFourActionTurns.ChooseReplacement(CanonicalWildPokemon,frame,WildContinuation(frame),new(0),new(BattleSide.Player,index));
            return CommitCanonicalWildResult(state with{WildBattle=b with{Replacement=null,LastSwitch=null}},result);
        }
        return new(CommandStatus.Rejected,state,"replacement_selection_required");
    }
    private string? ValidateCapturedParty(CampaignState state)
    {
        if(!_content.CaptureEnabled)return state.Exploration?.Captured is null?null:"save_capture_profile_missing";
        var allOwned=HistoricalPokemon(state);int gifts=GiftCount(state), trades=NpcTradeCount(state), eggs=DayCareEggCount(state);
        if(state.Exploration?.Captured is not {} captures||captures.IsDefault||captures.Length>(_content.PokemonStorageEnabled?PokemonStorageState.Capacity+5+(_content.Route5DayCareEnabled?1:0)+(_content.FourIslandDayCareEnabled?2:0):5)||state.NextPokemonId!=(allOwned.IsEmpty?1:captures.Length+gifts+trades+eggs+2L)||allOwned.Length!=(allOwned.IsEmpty?0:captures.Length+gifts+trades+eggs+1))return "save_capture_party_count";
        if(captures.Length>0&&!state.Script.Flags.GetValueOrDefault("FLAG_SYS_POKEDEX_GET"))return "save_capture_before_pokedex";
        var ids=new HashSet<long>();var battles=new HashSet<string>(StringComparer.Ordinal);
        foreach(var capture in captures)
        {
            if(capture is null||capture.InstanceId<2||!ids.Add(capture.InstanceId)||!battles.Add(capture.BattleId))return "save_capture_provenance_invalid";
            bool safari=capture.Ball=="ITEM_SAFARI_BALL"||capture.MetLocation=="MAPSEC_KANTO_SAFARI_ZONE"||state.Exploration.Battles.Any(b=>b.Id==capture.BattleId&&b.Safari);
            bool provenance=capture.RockSmash is not null?IsRockSmashCapture(state,capture):capture.SourceTable is not null?IsSourceTableCapture(state,capture):capture.Seafoam is not null?IsSeafoamCapture(state,capture):capture.Fishing is not null?IsTypedFishingCapture(state,capture):capture.PowerPlant is not null?IsPowerPlantCapture(state,capture):capture.EventIsland is not null?IsEventIslandCapture(state,capture):capture.SevenIsland is not null?IsSevenIslandCapture(state,capture):capture.CeruleanCaveOrigin is not null?IsCeruleanCaveCapture(state,capture):safari?IsSafariCapture(state,capture):
                capture.CelioRoamer is not null?IsCelioRoamerCapture(state,capture):capture.FiveIsland is not null?IsFiveIslandCapture(state,capture):
                capture.SixIsland is not null?IsSixIslandCapture(state,capture):capture.FourIsland is not null?IsFourIslandCapture(state,capture):
                IsSeviiRubyCapture(state,capture)||IsLostelleHypnoCapture(state,capture)||IsSnorlaxCapture(state,capture)||WildOriginSupported(capture.SourceSpeciesId,capture.MetLevel,capture.MetLocation);
            bool ballSupported=safari&&capture.Seafoam is null&&capture.PowerPlant is null&&capture.CeruleanCaveOrigin is null&&capture.EventIsland is null&&capture.SevenIsland is null||capture.Ball is null||AdditionalCaptureBallSupported(capture.Ball);
            if(!provenance||!ballSupported)return "save_capture_provenance_invalid";
            var owned=allOwned.SingleOrDefault(p=>p.InstanceId==capture.InstanceId);
            if(owned is null||!EvolutionSpeciesMatchesOrigin(state,owned,capture.SourceSpeciesId)||owned.Pokemon.Personality!=capture.Personality||owned.Pokemon.Ivs!=capture.Ivs||owned.MetLevel!=capture.MetLevel||owned.MetLocation!=capture.MetLocation||owned.Ball!=(capture.Ball??"ITEM_POKE_BALL"))return "save_capture_instance_mismatch";
            if(!state.Exploration.Battles.Any(b=>b.Id==capture.BattleId&&b.SourceSpeciesId==capture.SourceSpeciesId&&b.Level==capture.MetLevel&&b.Outcome==WildBattleOutcome.Caught&&b.ExperienceGained==0&&(b.RockSmashReceipt is null?capture.RockSmash is null:IsRockSmashCapture(state,capture))&&(b.SourceTableReceipt is null?capture.SourceTable is null:IsSourceTableCapture(state,capture))&&b.Fishing==capture.Fishing&&(b.SeafoamReceipt is null?capture.Seafoam is null:IsSeafoamCapture(state,capture))&&(b.PowerPlantReceipt is null?capture.PowerPlant is null:IsPowerPlantCapture(state,capture))&&(b.EventIslandReceipt is null?capture.EventIsland is null:IsEventIslandCapture(state,capture))&&b.CeruleanCaveOrigin==capture.CeruleanCaveOrigin&&(b.SevenIsland is null?capture.SevenIsland is null:IsSevenIslandCapture(state,capture))&&(b.CelioRoamer is null?capture.CelioRoamer is null:IsCelioRoamerCapture(state,capture))&&(b.FiveIsland is null?capture.FiveIsland is null:IsFiveIslandCapture(state,capture))&&(b.SixIsland is null?capture.SixIsland is null:IsSixIslandCapture(state,capture))&&(b.FourIsland is null?capture.FourIsland is null:IsFourIslandCapture(state,capture))&&(b.SeviiRuby is null||IsSeviiRubyCapture(state,capture))&&(b.VictoryRoad is null||_content.MapDefinition(b.VictoryRoad.Map).RegionMapSection==capture.MetLocation)))return "save_capture_history_missing";
        }
        if(state.Exploration.Battles.Any(b=>b.Outcome==WildBattleOutcome.Caught&&!battles.Contains(b.Id)))return "save_capture_delivery_missing";
        return null;
    }

    private bool IsCeruleanCaveCapture(CampaignState state,CampaignCapturedPokemon capture)
    {
        var history=state.Exploration?.Battles.SingleOrDefault(h=>h.Id==capture.BattleId);
        if(capture.CeruleanCaveOrigin is not {} origin||capture.SevenIsland is not null||capture.CelioRoamer is not null||capture.FiveIsland is not null||
            capture.SixIsland is not null||capture.FourIsland is not null||history is null||origin!=history.CeruleanCaveOrigin||
            !IsCeruleanCaveWildHistory(history)||history.Outcome!=WildBattleOutcome.Caught||history.ExperienceGained!=0||
            history.SourceSpeciesId!=capture.SourceSpeciesId||history.Level!=capture.MetLevel||
            _content.MapDefinition(origin.Map).RegionMapSection!=capture.MetLocation)return false;
        if(origin.Kind!=CeruleanCaveEncounterKind.Mewtwo)return true;
        var initial=history.MewtwoReceipt!.InitialPokemon;
        var owned=HistoricalPokemon(state).SingleOrDefault(p=>p.InstanceId==capture.InstanceId);
        return capture.Personality==initial.Personality&&capture.Ivs==initial.Ivs&&initial.OriginalTrainerId==state.TrainerId&&
            owned is not null&&SourceOwnedIdentity(owned.Pokemon,initial);
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
